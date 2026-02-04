using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
//using static Unity.VisualScripting.Dependencies.Sqlite.SQLite3;

public enum GameState
{
    Normal,           // สถานะปกติ สามารถเดินและโต้ตอบได้
    RepairingGlue,    // กำลังซ่อมด้วยกาว
    RepairingThread,  // กำลังซ่อมด้วยด้าย
    Crafting,         // กำลังประดิษฐ์จากกองขยะ
    RopeSwinging,     // กำลังโหนเชือก
    PushingObject,    // กำลังดันของ
    Menu,             // เมนู/หยุดชั่วคราว
    Cutscene,           // ดูฉาก
    Dead,
    UsingLever,        // กำลังใช้งาน Lever
    ClimbingLadder,
    InDialog,
    GodMode
}

public class GameManager : MonoBehaviour

{
    [Header("Audio Settings")]
    public AudioSource bgmAudioSource; // ลาก AudioSource มาใส่ตรงนี้
    public AudioClip sceneBGM;
    [Header("God Mode Settings")]
    public KeyCode godModeKey = KeyCode.F10; // ปุ่มเปิด/ปิด God Mode
    public float godModeSpeed = 10f;

    [Header("Checkpoint Settings")]
    [Tooltip("ใส่ ID ของ Checkpoint ที่ต้องการให้เป็นจุดเริ่มต้น (เช่น '01' หรือ 'Start')")]
    public string defaultStartCheckpointID = "01"; 

    [Header("Checkpoint Settings")]
    [Tooltip("Checkpoint เริ่มต้น (ถ้าไม่มีจะใช้ตำแหน่งเริ่มต้นของ Player)")]
    public Checkpoint defaultCheckpoint;

    [Header("Checkpoint Debug")]
    public bool showCheckpointDebugInfo = true;
    //public bool enableCheckpointNavigation = true; // เปิด/ปิดการใช้ลูกศรสลับ Checkpoint
    [Tooltip("โหมดดีบัก: TRUE = เกิดที่จุดล่าสุด & ใช้ลูกศรซ้าย-ขวาได้ | FALSE = เกิดที่จุดเริ่มต้น & ปิดการใช้ลูกศร")]
    public bool resetToLastCheckpoint = true;
    public bool debugGameGM = false;

    private HashSet<string> collectedItemIDs = new HashSet<string>();
    private HashSet<string> triggeredBonusCheckpointIDs = new HashSet<string>();
    private Dictionary<ItemManager.ItemType, int> checkpointItemSnapshot;

    private Checkpoint currentActiveCheckpoint;
    private Vector3 defaultSpawnPosition;
    private PlayerDeathSystem playerDeath;

    private Checkpoint[] allCheckpoints;
    private int currentCheckpointIndex = 0;

    [Header("Game State")]
    public GameState currentState = GameState.Normal;

    [Header("Player Reference")]
    public Transform player;
    public PlayerMovement playerMovement;
    public PlayerController playerController;

    [Header("Repair Progress Tracking")]
    public List<RepairProgress> repairProgresses = new List<RepairProgress>();

    [Header("Crafting Progress Tracking")]
    public List<CraftingProgress> craftingProgresses = new List<CraftingProgress>();

    [Header("Dialog System")]
    [Tooltip("ตัวแปรสำหรับเก็บ Dialog ที่จะเล่นหลังจาก Respawn")]
    public string dialogIDToPlayOnRespawn = "";
    public HashSet<string> playedDialogIDs = new HashSet<string>();

    private const string PLAYED_DIALOGS_KEY = "PlayedDialogIDs"; // กุญแจสำหรับ PlayerPrefs
    private const string BONUS_TRIGGERED_KEY = "TriggeredBonusIDs";
    [Header("Debug")]
    public bool showDebugInfo = true;

    // Events
    public static System.Action<GameState> OnGameStateChanged;
    public static System.Action<string> OnRepairCompleted;
    public static System.Action<string> OnCraftingCompleted;
    public static System.Action OnAllRepairsCompleted;

    // Singleton
    public static GameManager Instance { get; private set; }

    public GameState previousState = GameState.Normal;
    private Dictionary<string, RepairProgress> repairDict = new Dictionary<string, RepairProgress>();
    private Dictionary<string, CraftingProgress> craftingDict = new Dictionary<string, CraftingProgress>();

    void Awake()
    {
        // Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (bgmAudioSource == null) bgmAudioSource = GetComponent<AudioSource>();
            PlayBGM(sceneBGM);
            LoadPlayedDialogs();
            InitializeManager();
            //InitializeCheckpointSystem();
            //SetupCheckpointSystem();
        }
        else
        {
            if (sceneBGM != null)
            {
                
                Instance.PlayBGM(sceneBGM);
            }
            Destroy(gameObject);
        }
    }


    void Start()
    {
        SetupReferences();
        SetupCheckpointSystem();
        BuildRepairDictionary();
        BuildCraftingDictionary();


        // โหลด Checkpoint และไอเทมหลัง reload
     //   ApplyCheckpointAndItemsAfterReload();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FindPlayerReferences();
        ApplyCheckpointAndItemsAfterReload();
    }

    void Update()
    {
        if(!debugGameGM)
        HandleGodModeInput();

        HandleGlobalInput();
        HandleCheckpointInput();
        HandleDebugResetInput();
       
        if (showDebugInfo) DisplayDebugInfo();
    }

    public void PlayBGM(AudioClip music)
    {
        // ถ้าไม่มี AudioSource หรือไม่มีเพลงส่งมา ให้จบการทำงาน
        if (bgmAudioSource == null || music == null) return;

        // "ถ้าเพลงใหม่ เหมือนกับ เพลงที่เล่นอยู่แล้ว ไม่ต้องทำอะไร (เล่นต่อเนื่องไปเลย)"
        if (bgmAudioSource.clip == music && bgmAudioSource.isPlaying) return;

        // ถ้าเพลงไม่เหมือนกัน หรือเพลงหยุดอยู่ ให้เปลี่ยนและเล่นใหม่
        bgmAudioSource.clip = music;
        bgmAudioSource.Play();
    }

    #region Played Dialogs Persistence
    private void FindPlayerReferences()
    {
        // ค้นหา GameObject ที่มี Tag "Player"
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
        {
            player = playerObj.transform;
            playerMovement = playerObj.GetComponent<PlayerMovement>();
            playerController = playerObj.GetComponent<PlayerController>();

            // หา PlayerDeathSystem ด้วย (เห็นคุณใช้ในโค้ดอื่น)
            playerDeath = playerObj.GetComponent<PlayerDeathSystem>();

            // อัปเดต defaultSpawnPosition ใหม่จาก Player ตัวใหม่
            defaultSpawnPosition = player.position;

            Debug.Log("[GameManager] Found new Player references.");
        }
        else
        {
            Debug.LogError("[GameManager] CRITICAL: Player with tag 'Player' not found in this scene!");
        }
    }
    /// <summary>
    /// โหลดรายชื่อ Dialog ID ที่เคยเล่นแล้วจาก PlayerPrefs
    /// </summary>
    private void LoadPlayedDialogs()
    {
        if (PlayerPrefs.HasKey(PLAYED_DIALOGS_KEY))
        {
            string savedData = PlayerPrefs.GetString(PLAYED_DIALOGS_KEY);
            // แยก string กลับมาเป็น Array แล้วสร้าง HashSet ใหม่
            string[] ids = savedData.Split(',');
            playedDialogIDs = new HashSet<string>(ids);
            Debug.Log($"[GameManager] Loaded {playedDialogIDs.Count} played dialog IDs.");
        }
    }

    /// <summary>
    /// บันทึกรายชื่อ Dialog ID ทั้งหมดลง PlayerPrefs
    /// </summary>
    private void SavePlayedDialogs()
    {
        // รวม HashSet เป็น string เดียวโดยใช้ ',' คั่นกลาง
        string dataToSave = string.Join(",", playedDialogIDs);
        PlayerPrefs.SetString(PLAYED_DIALOGS_KEY, dataToSave);
        PlayerPrefs.Save(); // สั่งให้บันทึกข้อมูลลงเครื่องจริงๆ
        Debug.Log($"[GameManager] Saved played dialog IDs: {dataToSave}");
    }

    /// <summary>
    /// ตรวจสอบว่า Dialog ID นี้เคยเล่นไปแล้วหรือยัง
    /// </summary>
    public bool HasAlreadyPlayed(string dialogID)
    {
        if (string.IsNullOrEmpty(dialogID)) return false;
        return playedDialogIDs.Contains(dialogID);
    }

    /// <summary>
    /// บันทึกว่า Dialog ID นี้ถูกเล่นไปแล้ว และสั่งเซฟลง PlayerPrefs
    /// </summary>
    public void MarkAsPlayed(string dialogID)
    {
        if (string.IsNullOrEmpty(dialogID) || playedDialogIDs.Contains(dialogID))
        {
            return;
        }

        playedDialogIDs.Add(dialogID);
        SavePlayedDialogs();
    }

    /// <summary>
    /// (สำหรับ Debug) ล้างความจำ Dialog ทั้งหมดที่เคยบันทึกไว้
    /// </summary>
    [ContextMenu("Clear All Played Dialogs History")]
    public void ClearPlayedDialogsHistory()
    {
        PlayerPrefs.DeleteKey(PLAYED_DIALOGS_KEY);
        playedDialogIDs.Clear();
        Debug.LogWarning("[GameManager] All played dialogs history has been cleared!");
    }

    #endregion

    #region Initialization

    void InitializeManager()
    {
        // ค้นหา Player อัตโนมัติถ้ายังไม่ได้ระบุ
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (playerMovement == null && player != null)
            playerMovement = player.GetComponent<PlayerMovement>();
    }

    void SetupReferences()
    {
        // ค้นหาและเชื่อมต่อกับระบบซ่อมตุ๊กตา
        var dollSystems = FindObjectsByType<DollRepairSystem>(FindObjectsSortMode.None);
        foreach (var system in dollSystems)
        {
            system.gameManager = this;

            string systemId = system.GetRepairId();
            bool found = false;

            foreach (var progress in repairProgresses)
            {
                if (progress.repairId == systemId)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                var newProgress = new RepairProgress(systemId, system.repairName);
                repairProgresses.Add(newProgress);
                Debug.Log($"Auto-added repair progress for: {system.repairName}");
            }
        }

        // ค้นหาและเชื่อมต่อกับระบบประดิษฐ์จากขยะ
        var trashSystems = FindObjectsByType<TrashCraftingSystem>(FindObjectsSortMode.None);
        foreach (var system in trashSystems)
        {
            system.gameManager = this;

            string systemId = system.GetTrashId();
            bool found = false;

            foreach (var progress in craftingProgresses)
            {
                if (progress.trashId == systemId)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                var newProgress = new CraftingProgress(systemId, system.trashName);
                craftingProgresses.Add(newProgress);
                Debug.Log($"Auto-added crafting progress for: {system.trashName}");
            }
        }
    }

    public bool CanInteractWithRepairSystem()
    {
        return currentState == GameState.Normal;
    }

    public bool CanInteractWithCraftingSystem()
    {
        return currentState == GameState.Normal;
    }

    public List<DollRepairSystem> GetAllRepairSystems()
    {
        return new List<DollRepairSystem>(FindObjectsByType<DollRepairSystem>(FindObjectsSortMode.None));
    }

    public List<TrashCraftingSystem> GetAllCraftingSystems()
    {
        return new List<TrashCraftingSystem>(FindObjectsByType<TrashCraftingSystem>(FindObjectsSortMode.None));
    }

    public DollRepairSystem GetRepairSystemById(string repairId)
    {
        var systems = FindObjectsByType<DollRepairSystem>(FindObjectsSortMode.None);
        foreach (var system in systems)
        {
            if (system.GetRepairId() == repairId)
                return system;
        }
        return null;
    }

    public TrashCraftingSystem GetCraftingSystemById(string trashId)
    {
        var systems = FindObjectsByType<TrashCraftingSystem>(FindObjectsSortMode.None);
        foreach (var system in systems)
        {
            if (system.GetTrashId() == trashId)
                return system;
        }
        return null;
    }

    void BuildRepairDictionary()
    {
        repairDict.Clear();
        foreach (var progress in repairProgresses)
        {
            if (!string.IsNullOrEmpty(progress.repairId))
            {
                repairDict[progress.repairId] = progress;
            }
        }
    }

    void BuildCraftingDictionary()
    {
        craftingDict.Clear();
        foreach (var progress in craftingProgresses)
        {
            if (!string.IsNullOrEmpty(progress.trashId))
            {
                craftingDict[progress.trashId] = progress;
            }
        }
    }

    #endregion

    #region State Management

    public bool ChangeState(GameState newState, string reason = "")
    {
        // ตรวจสอบว่าสามารถเปลี่ยนสถานะได้หรือไม่
        if (!CanChangeToState(newState))
        {
            if (showDebugInfo)
                Debug.LogWarning($"Cannot change to state {newState} from {currentState}. Reason: {reason}");
            return false;
        }

        previousState = currentState;
        currentState = newState;

        // แจ้งเตือนการเปลี่ยนสถานะ
        OnGameStateChanged?.Invoke(currentState);

        // จัดการผู้เล่น
        HandlePlayerStateChange();

        if (showDebugInfo)
            Debug.Log($"Game State Changed: {previousState} -> {currentState}" +
                     (string.IsNullOrEmpty(reason) ? "" : $" ({reason})"));

        return true;
    }

    public bool CanChangeToState(GameState targetState)
    {

        if (targetState == GameState.GodMode) return true;
        if (currentState == GameState.GodMode && targetState == GameState.Normal) return true;

        switch (currentState)
        {
            case GameState.Normal:
                return true; // สามารถเปลี่ยนไปสถานะใดก็ได้

            case GameState.UsingLever:
            case GameState.Crafting:
                // ออกจากสถานะได้เฉพาะกลับไป Normal
                return targetState == GameState.Normal;

            case GameState.RepairingGlue:
            case GameState.RepairingThread:
                // ออกจากระบบซ่อมได้เฉพาะไป Normal หรือ Menu
                return targetState == GameState.Normal || targetState == GameState.Menu;

            //case GameState.Crafting:
            //    // ออกจากระบบประดิษฐ์ได้เฉพาะไป Normal หรือ Menu
            //    return targetState == GameState.Normal || targetState == GameState.Menu;

            case GameState.RopeSwinging:
                // ระหว่างโหนสามารถหยุดได้
                return targetState == GameState.Normal || targetState == GameState.Menu;
            case GameState.InDialog:
                // เมื่ออยู่ใน Dialog สามารถกลับไปสถานะ Normal ได้เท่านั้น (เมื่อจบ)
                return targetState == previousState || targetState == GameState.Normal;

            case GameState.PushingObject:
                // ระหว่างดันของสามารถหยุดได้
                return targetState == GameState.Normal || targetState == GameState.Menu || targetState == GameState.InDialog;

            case GameState.ClimbingLadder:
                // ระหว่างปีนบันได สามารถกลับไปสถานะ Normal (เมื่อออก) หรือเปิดเมนูได้
                return targetState == GameState.Normal || targetState == GameState.Menu || targetState == GameState.InDialog;
           
            case GameState.Menu:
                // จากเมนูสามารถกลับไปสถานะเดิมได้
                return targetState == previousState || targetState == GameState.Normal;

            case GameState.Cutscene:
                // ระหว่างฉากไม่สามารถเปลี่ยนสถานะได้ จนกว่าจะจบ
                return targetState == GameState.Normal;

            case GameState.Dead:
                // ตอนตาย ห้ามเปลี่ยนสถานะ ยกเว้น Respawning
                return targetState == GameState.Normal;

            case GameState.GodMode:
                return targetState == GameState.Normal; // จาก GodMode กลับไป Normal ได้เท่านั้น


            default:
                return false;
        }
    }

    void HandlePlayerStateChange()
    {
        if (playerController == null) return;

        switch (currentState)
        {
            case GameState.Normal:
                playerMovement.SetGodMode(false);
                break;
            case GameState.GodMode:
                playerMovement.SetGodMode(true); // <--- เปิด God Mode ที่ตัวผู้เล่น
                break;
            case GameState.RepairingGlue:
            case GameState.RepairingThread:
            case GameState.Crafting:
                EnablePlayerControl(false);
                break;

            case GameState.RopeSwinging:
                EnablePlayerMovement(false);
                EnablePlayerInteraction(false);
                break;
            case GameState.InDialog:
                // ปิดการควบคุมทั้งหมดของผู้เล่น
                EnablePlayerControl(false);
                break;

            case GameState.PushingObject:
                EnablePlayerMovement(true, 0.5f); // ลดความเร็ว
                EnablePlayerInteraction(false);
                break;

            case GameState.Menu:
            case GameState.Cutscene:
                EnablePlayerControl(false);
                break;

            case GameState.Dead: // หยุดทุกอย่างเมื่อผู้เล่นตาย
                EnablePlayerControl(false);
                EnablePlayerMovement(false);
                EnablePlayerInteraction(false);
                break;
            case GameState.ClimbingLadder:
                // ปิดการควบคุมปกติ แต่ PlayerMovement จะยังจัดการการเคลื่อนที่บนบันไดเอง
                EnablePlayerInteraction(false);
                break;
          

        }
    }

    #endregion

    #region GodMode

    void HandleGodModeInput()
    {
        if (Input.GetKeyDown(godModeKey))
        {
            ToggleGodMode();
        }
    }

    public void ToggleGodMode()
    {
        if (currentState == GameState.GodMode)
        {
            // ปิด God Mode กลับสู่สถานะปกติ
            ChangeState(GameState.Normal, "Deactivated God Mode");
            playerMovement.SetGodMode(false);
        }
        else
        {
            currentState = GameState.GodMode;

            if (playerMovement != null)
            {
                playerMovement.SetGodMode(true); // <--- ต้องเรียกบรรทัดนี้ ไม่งั้น Player ไม่รู้เรื่องครับ
              
            }
        }
    }

    #endregion

    #region Persistence Public Methods

    public void MarkItemAsCollected(string itemID)
    {
        if (!string.IsNullOrEmpty(itemID) && !collectedItemIDs.Contains(itemID))
        {
            collectedItemIDs.Add(itemID);
        }
    }

    public bool HasItemBeenCollected(string itemID)
    {
        return !string.IsNullOrEmpty(itemID) && collectedItemIDs.Contains(itemID);
    }

    public void MarkBonusCheckpointTriggered(string checkpointID)
    {
        if (!string.IsNullOrEmpty(checkpointID) && !triggeredBonusCheckpointIDs.Contains(checkpointID))
        {
            triggeredBonusCheckpointIDs.Add(checkpointID);
        }
    }

    public bool HasBonusCheckpointBeenTriggered(string checkpointID)
    {
        return !string.IsNullOrEmpty(checkpointID) && triggeredBonusCheckpointIDs.Contains(checkpointID);
    }

    public void SaveItemSnapshot()
    {
        if (ItemManager.Instance == null) return;

        // 1. บันทึกจำนวนไอเทมลง PlayerPrefs โดยตรง
        PlayerPrefs.SetInt("Saved_Glue", ItemManager.Instance.GetItemCount(ItemManager.ItemType.Glue));
        PlayerPrefs.SetInt("Saved_Thread", ItemManager.Instance.GetItemCount(ItemManager.ItemType.Thread));

        // 2. บันทึกรายการ Checkpoint ที่เคยแจกของไปแล้ว (กันปั๊มของ)
        string dataToSave = string.Join(",", triggeredBonusCheckpointIDs);
        PlayerPrefs.SetString(BONUS_TRIGGERED_KEY, dataToSave);

        PlayerPrefs.Save();
        if (showDebugInfo)
        {
            Debug.Log($"<color=cyan>[GameManager] Saved Inventory & Bonus History to Disk.</color>");
        }
    }

    private void RestoreItemsFromSnapshot()
    {
        if (ItemManager.Instance == null) return;

        // 1. โหลดประวัติ Checkpoint ที่เคยได้โบนัส
        if (PlayerPrefs.HasKey(BONUS_TRIGGERED_KEY))
        {
            string savedData = PlayerPrefs.GetString(BONUS_TRIGGERED_KEY);
            string[] ids = savedData.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            triggeredBonusCheckpointIDs = new HashSet<string>(ids);
        }

        // 2. โหลดจำนวนไอเทม
        if (PlayerPrefs.HasKey("Saved_Glue") || PlayerPrefs.HasKey("Saved_Thread"))
        {
            int glue = PlayerPrefs.GetInt("Saved_Glue", 0); // ค่า Default 0 หรือค่าเริ่มต้นที่คุณต้องการ
            int thread = PlayerPrefs.GetInt("Saved_Thread", 0);

            ItemManager.Instance.SetItemCount(ItemManager.ItemType.Glue, glue);
            ItemManager.Instance.SetItemCount(ItemManager.ItemType.Thread, thread);

            Debug.Log($"<color=cyan>[GameManager] Restored Items: Glue={glue}, Thread={thread}</color>");
        }
    }

    #endregion

    #region Checkpoint Management

    private void ApplyCheckpointAndItemsAfterReload()
    {
        InitializeCheckpointSystem();
        CollectAllCheckpoints(); // เก็บ Checkpoint ทั้งหมดในฉากใหม่เข้า Array

        // ================================================================
        // [FIX] ส่วนที่เพิ่ม: ซ่อม defaultCheckpoint ที่หายไป (Missing)
        // ================================================================
        if (defaultCheckpoint == null && allCheckpoints.Length > 0)
        {
            // พยายามหา Checkpoint ที่มี ID ตรงกับที่เราตั้งไว้ใน defaultStartCheckpointID
            foreach (var cp in allCheckpoints)
            {
                if (cp.GetCheckpointID() == defaultStartCheckpointID)
                {
                    defaultCheckpoint = cp;
                    Debug.Log($"<color=cyan>[GameManager] Re-assigned Default Checkpoint to ID: {defaultStartCheckpointID}</color>");
                    break;
                }
            }

            // ถ้าหาไม่เจอจริงๆ ให้ใช้ตัวแรกสุดของฉากเป็น Default ไปเลย (กันเหนียว)
            if (defaultCheckpoint == null)
            {
                defaultCheckpoint = allCheckpoints[0];
                Debug.LogWarning($"[GameManager] Could not find ID '{defaultStartCheckpointID}'. Using first checkpoint ({allCheckpoints[0].GetCheckpointID()}) as default.");
            }
        }
        // ================================================================


        // 1. กู้คืนไอเทมและประวัติโบนัสก่อนเป็นอันดับแรก
        RestoreItemsFromSnapshot();

        bool checkpointIsSet = false;

        // 2. โหลดตำแหน่ง Checkpoint ล่าสุด (ถ้ามีเซฟ)
        if (PlayerPrefs.HasKey("LastCheckpoint"))
        {
            string checkpointID = PlayerPrefs.GetString("LastCheckpoint");
            foreach (Checkpoint checkpoint in allCheckpoints)
            {
                if (checkpoint != null && checkpoint.GetCheckpointID() == checkpointID)
                {
                    SetActiveCheckpoint(checkpoint);
                    checkpoint.ActivateCheckpoint();
                    checkpointIsSet = true;
                    Debug.Log($"<color=lime>Checkpoint loaded from save: {checkpointID}</color>");
                    break;
                }
            }
        }

        // 3. ถ้าไม่มีเซฟ (หรือหาไม่เจอ) -> ให้ใช้ Default Checkpoint ที่เราเพิ่งซ่อมไปข้างบน
        if (!checkpointIsSet)
        {
            // ถ้าอยู่ในโหมด ResetToLastCheckpoint (Debug) เราจะไม่ทำอะไร ให้มันโหลดเซฟ
            // แต่ถ้าไม่ (โหมดเล่นจริง หรือเพิ่งเริ่มเกมใหม่) ให้ใช้ Default

            // หมายเหตุ: Logic ตรงนี้ขึ้นอยู่กับว่าคุณอยากให้ 'กด R' แล้วกลับไปจุดเซฟล่าสุด หรือกลับไปจุดเริ่มเกม
            // ถ้าอยากให้กด R แล้วกลับไปจุดเซฟล่าสุดเสมอ ให้ปล่อยผ่าน
            // ถ้าอยากให้เริ่มใหม่ที่ Default ถ้าไม่มีเซฟ ให้ทำดังนี้:

            if (defaultCheckpoint != null)
            {
                SetActiveCheckpoint(defaultCheckpoint);
                defaultCheckpoint.ActivateCheckpoint();
                // ย้าย Player ไปจุด Default ทันทีถ้าจำเป็น
                if (player != null) player.position = defaultCheckpoint.GetSpawnPosition();

                Debug.Log($"<color=yellow>Starting at Default Checkpoint: {defaultCheckpoint.GetCheckpointID()}</color>");
            }
        }

        // จัดการตำแหน่ง Player ขั้นสุดท้าย
        if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (player != null)
        {
            // ถ้ามี Checkpoint ให้เกิดที่ Checkpoint
            if (currentActiveCheckpoint != null)
            {
                player.position = currentActiveCheckpoint.GetSpawnPosition();
                if (playerDeath != null) playerDeath.Respawn(currentActiveCheckpoint.GetSpawnPosition());
            }
            // ถ้าไม่มี ให้เกิดจุดเริ่มต้น scene (กรณีแย่สุดที่ไม่มี Checkpoint เลย)
            else
            {
                player.position = defaultSpawnPosition;
            }
        }

        if (ItemManager.Instance != null)
        {
            ItemManager.Instance.UpdateUI();
        }

        ChangeState(GameState.Normal, "Reset state after reload scene");
    }
 



    private void InitializeCheckpointSystem()
    {
        // หา PlayerDeath component
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerDeath = playerObj.GetComponent<PlayerDeathSystem>();
            defaultSpawnPosition = playerObj.transform.position;
        }
        else
        {
            Debug.LogError("ไม่พบ Player ใน Scene!");
        }
    }

    // เรียกใน Start() ของ GameManager
    private void SetupCheckpointSystem()
    {
        CollectAllCheckpoints();

        // ตั้งค่า Default Checkpoint
        if (defaultCheckpoint != null)
        {
            SetActiveCheckpoint(defaultCheckpoint);
            defaultCheckpoint.ActivateCheckpoint();
        }
        else
        {
            Debug.LogWarning("ไม่ได้กำหนด Default Checkpoint");
        }
    }


    private void CollectAllCheckpoints()
    {
        allCheckpoints = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);

        // เรียงตาม ID (String) แทนการเรียงตามระยะทาง X
        // ถ้า ID เป็น "01", "02", "03" มันจะเรียงถูกต้องแน่นอนไม่ว่าวางอยู่ตรงไหนของฉาก
        System.Array.Sort(allCheckpoints, (a, b) =>
            string.Compare(a.GetCheckpointID(), b.GetCheckpointID()));

        // (Debug) เช็คผลลัพธ์
        if (showCheckpointDebugInfo)
        {
            Debug.Log($"เรียง Checkpoint ตาม ID ({allCheckpoints.Length} จุด):");
            for (int i = 0; i < allCheckpoints.Length; i++)
            {
                Debug.Log($" [{i}] ID: {allCheckpoints[i].GetCheckpointID()}");
            }
        }

        UpdateCurrentCheckpointIndex();
    }

    //private void CollectAllCheckpoints()
    //{
    //    allCheckpoints = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);

    //    // เรียงตามตำแหน่ง X (จากซ้ายไปขวา)
    //    System.Array.Sort(allCheckpoints, (a, b) =>
    //        a.transform.position.x.CompareTo(b.transform.position.x));

    //    if (showCheckpointDebugInfo)
    //    {
    //        Debug.Log($"พบ Checkpoint ทั้งหมด {allCheckpoints.Length} จุด:");
    //        for (int i = 0; i < allCheckpoints.Length; i++)
    //        {
    //            //Debug.Log($"  [{i}] {allCheckpoints[i].GetCheckpointID()} - Pos: {allCheckpoints[i].transform.position}");
    //        }
    //    }

    //    // หา Index ของ Checkpoint ปัจจุบัน
    //    UpdateCurrentCheckpointIndex();
    //}

    private void NavigateToPreviousCheckpoint()
    {
        if (allCheckpoints == null || allCheckpoints.Length == 0) return;

        UpdateCurrentCheckpointIndex();

        currentCheckpointIndex--;
        if (currentCheckpointIndex < 0)
            currentCheckpointIndex = allCheckpoints.Length - 1;

        TeleportToCheckpoint(currentCheckpointIndex);
    }

    private void NavigateToNextCheckpoint()
    {
        if (allCheckpoints == null || allCheckpoints.Length == 0) return;
        UpdateCurrentCheckpointIndex();

        currentCheckpointIndex++;
        if (currentCheckpointIndex >= allCheckpoints.Length)
            currentCheckpointIndex = 0;

        TeleportToCheckpoint(currentCheckpointIndex);
    }

    private void TeleportToCheckpoint(int index)
    {
        if (allCheckpoints == null || index < 0 || index >= allCheckpoints.Length)
            return;

        Checkpoint targetCheckpoint = allCheckpoints[index];

        // เปลี่ยน Checkpoint
        SetActiveCheckpoint(targetCheckpoint);
        targetCheckpoint.ActivateCheckpoint();

        // ย้าย Player
        if (player != null)
        {
            player.position = targetCheckpoint.GetSpawnPosition();

            if (playerDeath != null)
                playerDeath.Respawn(targetCheckpoint.GetSpawnPosition());
        }

        if (showCheckpointDebugInfo)
        {
            Debug.Log($"🚩 Teleport to Checkpoint [{index}]: {targetCheckpoint.GetCheckpointID()}");
        }
    }

    private void UpdateCurrentCheckpointIndex()
    {
        if (allCheckpoints == null || currentActiveCheckpoint == null)
            return;

        for (int i = 0; i < allCheckpoints.Length; i++)
        {
            if (allCheckpoints[i] == currentActiveCheckpoint)
            {
                currentCheckpointIndex = i;
                break;
            }
        }
    }

    // เรียกใน Update() ของ GameManager
    private void HandleCheckpointInput()
    {
        // ปุ่มรีเซ็ตแมนนวล (R key)
        if (Input.GetKeyDown(KeyCode.R))
        {
            ManualReset();
        }

        // Debug Navigation (ลูกศรซ้าย-ขวา) - เพิ่มใหม่
        if (resetToLastCheckpoint &&!debugGameGM)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                NavigateToPreviousCheckpoint();
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                NavigateToNextCheckpoint();
            }
        }
        // Debug Info
        if (showCheckpointDebugInfo && Input.GetKeyDown(KeyCode.F1))
        {
            ShowCheckpointDebugInfo();
        }
    }

    public void SetActiveCheckpoint(Checkpoint checkpoint)
    {

        if (currentActiveCheckpoint == checkpoint)
        {
            return;
        }

        if (currentActiveCheckpoint != null && currentActiveCheckpoint != checkpoint)
        {
            currentActiveCheckpoint.DeactivateCheckpoint();
        }
        currentActiveCheckpoint = checkpoint;

        UpdateCurrentCheckpointIndex();

        // บันทึก ID Checkpoint
        if (checkpoint != null)
        {
            PlayerPrefs.SetString("LastCheckpoint", checkpoint.GetCheckpointID());
            // บันทึกตำแหน่ง XYZ ด้วยเผื่อจำเป็น
            PlayerPrefs.SetFloat("CheckpointX", checkpoint.transform.position.x);
            PlayerPrefs.SetFloat("CheckpointY", checkpoint.transform.position.y);
            PlayerPrefs.SetFloat("CheckpointZ", checkpoint.transform.position.z);
            SaveItemSnapshot();

            if (showCheckpointDebugInfo) // เช็คตัวแปร debug ก่อน Log
            {
                Debug.Log($"Active checkpoint set & saved: {checkpoint.GetCheckpointID()}");
            }
        }

        // *** สำคัญ: เรียก SaveItemSnapshot ตรงนี้ เพื่อบันทึก Item และ Bonus History ทันที ***
 



    }

    public Vector3 GetCurrentSpawnPosition()
    {
        if (currentActiveCheckpoint != null)
        {
            return currentActiveCheckpoint.GetSpawnPosition();
        }
        return defaultSpawnPosition;
    }

    public void RespawnPlayer()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (player != null)
            playerDeath = player.GetComponent<PlayerDeathSystem>();

        if (playerDeath != null)
        {
            playerDeath.Respawn(GetCurrentSpawnPosition());
        }
        else
        {
            Debug.LogError("ไม่พบ PlayerDeath component!");
        }
    }

    public void ManualReset()
    {

        if (showCheckpointDebugInfo) Debug.Log("Manual Reset triggered (R key)");

        // --- แก้ไข: เช็คว่าถ้ามี Checkpoint อยู่แล้ว ให้เซฟย้ำอีกทีก่อนรีเซ็ต ---
        if (currentActiveCheckpoint != null)
        {
            // บันทึก ID ลง PlayerPrefs อีกครั้งกันเหนียว
            PlayerPrefs.SetString("LastCheckpoint", currentActiveCheckpoint.GetCheckpointID());
            PlayerPrefs.SetFloat("CheckpointX", currentActiveCheckpoint.transform.position.x);
            PlayerPrefs.SetFloat("CheckpointY", currentActiveCheckpoint.transform.position.y);
            PlayerPrefs.SetFloat("CheckpointZ", currentActiveCheckpoint.transform.position.z);

            //// บันทึกจำนวนไอเทมด้วย
            //if (ItemManager.Instance != null)
            //{
            //    PlayerPrefs.SetInt("Saved_Glue", ItemManager.Instance.GetItemCount(ItemManager.ItemType.Glue));
            //    PlayerPrefs.SetInt("Saved_Thread", ItemManager.Instance.GetItemCount(ItemManager.ItemType.Thread));
            //}
        }
        // กรณีที่ currentActiveCheckpoint กลายเป็น "ซาก" (Missing) แต่อยากให้จำค่าเดิม
        // เราจะไม่ไปลบค่า PlayerPrefs ทิ้ง ปล่อยให้มันจำค่าเดิมที่มีใน Disk ไปเลย

        PlayerPrefs.Save(); // บันทึกข้อมูลลง Disk ทันที

        // Reload scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        //if (showCheckpointDebugInfo)
        //{
        //    Debug.Log("รีเซ็ตแมนนวลด้วยปุ่ม R");
        //}

        //// เซฟ checkpoint และไอเทมก่อน reload scene
        //if (currentActiveCheckpoint != null)
        //{
        //    PlayerPrefs.SetString("LastCheckpoint", currentActiveCheckpoint.GetCheckpointID());
        //    PlayerPrefs.SetFloat("CheckpointX", currentActiveCheckpoint.transform.position.x);
        //    PlayerPrefs.SetFloat("CheckpointY", currentActiveCheckpoint.transform.position.y);
        //    PlayerPrefs.SetFloat("CheckpointZ", currentActiveCheckpoint.transform.position.z);
        //}

        //if (ItemManager.Instance != null)
        //{
        //    PlayerPrefs.SetInt("GlueCount", ItemManager.Instance.GetItemCount(ItemManager.ItemType.Glue));
        //    PlayerPrefs.SetInt("ThreadCount", ItemManager.Instance.GetItemCount(ItemManager.ItemType.Thread));
        //}

        //PlayerPrefs.Save();

        //// Reload scene
        //SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadCheckpointFromSave()
    {
        if (PlayerPrefs.HasKey("LastCheckpoint"))
        {
            string checkpointID = PlayerPrefs.GetString("LastCheckpoint");
            Vector3 savedPosition = new Vector3(
                PlayerPrefs.GetFloat("CheckpointX"),
                PlayerPrefs.GetFloat("CheckpointY"),
                PlayerPrefs.GetFloat("CheckpointZ")
            );

            // หา Checkpoint ที่ตรงกับ ID
            Checkpoint[] allCheckpoints = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
            foreach (Checkpoint checkpoint in allCheckpoints)
            {
                if (checkpoint.GetCheckpointID() == checkpointID)
                {
                    SetActiveCheckpoint(checkpoint);
                    checkpoint.ActivateCheckpoint();

                    if (showCheckpointDebugInfo)
                    {
                        Debug.Log($"โหลด Checkpoint จากเซฟ: {checkpointID}");
                    }
                    return;
                }
            }

            Debug.LogWarning($"ไม่พบ Checkpoint ID: {checkpointID}");
        }
    }

    public void ClearCheckpointSaveData()
    {
        PlayerPrefs.DeleteKey("LastCheckpoint");
        PlayerPrefs.DeleteKey("CheckpointX");
        PlayerPrefs.DeleteKey("CheckpointY");
        PlayerPrefs.DeleteKey("CheckpointZ");
        PlayerPrefs.Save();

        Debug.Log("ลบข้อมูลเซฟ Checkpoint แล้ว");
    }

    private void ShowCheckpointDebugInfo()
    {
        if (currentActiveCheckpoint != null)
        {
            Debug.Log($"=== Checkpoint Debug Info ===");
            Debug.Log($"Active Checkpoint [{currentCheckpointIndex}/{allCheckpoints.Length - 1}]: {currentActiveCheckpoint.GetCheckpointID()}");
            Debug.Log($"Position: {currentActiveCheckpoint.transform.position}");
            Debug.Log($"Spawn Position: {GetCurrentSpawnPosition()}");
            Debug.Log($"");
            Debug.Log($"All Checkpoints:");
            for (int i = 0; i < allCheckpoints.Length; i++)
            {
                string marker = (i == currentCheckpointIndex) ? "→ " : "  ";
                Debug.Log($"{marker}[{i}] {allCheckpoints[i].GetCheckpointID()}");
            }
        }
        else
        {
            Debug.Log("ไม่มี Active Checkpoint");
        }

        Debug.Log($"");
        Debug.Log($"Controls:");
        Debug.Log($"  R = Manual Reset");
        Debug.Log($"  F1 = Debug Info");
        Debug.Log($"  ← = Previous Checkpoint");
        Debug.Log($"  → = Next Checkpoint");
    }

    // สำหรับเรียกจาก UI หรือ External Script
    public Checkpoint GetCurrentCheckpoint()
    {
        return currentActiveCheckpoint;
    }

    #endregion

    #region Player Control

    void EnablePlayerControl(bool enable)
    {
        if (playerController != null)
        {
            playerController.SetControlEnabled(enable);
        }
    }

    void EnablePlayerMovement(bool enable, float speedMultiplier = 1f)
    {
        if (playerController != null)
        {
            playerController.SetMovementEnabled(enable, speedMultiplier);
        }
    }

    void EnablePlayerInteraction(bool enable)
    {
        if (playerController != null)
        {
            playerController.SetInteractionEnabled(enable);
        }
    }

    #endregion

    #region Repair System Management

    public bool IsRepairCompleted(string repairId)
    {
        return repairDict.ContainsKey(repairId) && repairDict[repairId].isCompleted;
    }

    public bool CanStartRepair(string repairId, DollRepairSystem.RepairTool toolType)
    {
        // ตรวจสอบสถานะเกม
        if (currentState != GameState.Normal) return false;
        if (IsRepairCompleted(repairId)) return false;

        // ตรวจสอบไอเทมที่จำเป็น
        if (ItemManager.Instance != null)
        {
            ItemManager.ItemType requiredItem = toolType == DollRepairSystem.RepairTool.Glue
                ? ItemManager.ItemType.Glue
                : ItemManager.ItemType.Thread;

            int itemCount = ItemManager.Instance.GetItemCount(requiredItem);
            if (itemCount <= 0)
            {
                if (showDebugInfo)
                    Debug.LogWarning($"Cannot start repair - no {requiredItem} available (count: {itemCount})");
                return false;
            }
        }

        return true;
    }

    public bool StartRepair(string repairId, DollRepairSystem.RepairTool toolType)
    {
        if (!CanStartRepair(repairId, toolType)) return false;

        GameState repairState = toolType == DollRepairSystem.RepairTool.Glue
            ? GameState.RepairingGlue
            : GameState.RepairingThread;

        return ChangeState(repairState, $"Starting repair {repairId} with {toolType}");
    }

    public void CompleteRepair(string repairId, DollRepairSystem.RepairTool toolType)
    {
        if (repairDict.ContainsKey(repairId))
        {
            var progress = repairDict[repairId];
            progress.isCompleted = true;
            progress.completionTime = Time.time;
            progress.toolUsed = toolType;

            OnRepairCompleted?.Invoke(repairId);

            if (showDebugInfo)
                Debug.Log($"Repair completed: {repairId} using {toolType}");
        }

        ChangeState(GameState.Normal, "Repair completed");
        CheckAllRepairsCompleted();
    }

    void CheckAllRepairsCompleted()
    {
        bool allCompleted = true;
        foreach (var progress in repairProgresses)
        {
            if (!progress.isCompleted)
            {
                allCompleted = false;
                break;
            }
        }

        if (allCompleted && repairProgresses.Count > 0)
        {
            OnAllRepairsCompleted?.Invoke();
            if (showDebugInfo)
                Debug.Log("All repairs completed!");
        }
    }

    public RepairProgress GetRepairProgress(string repairId)
    {
        return repairDict.ContainsKey(repairId) ? repairDict[repairId] : null;
    }

    public List<RepairProgress> GetCompletedRepairs()
    {
        var completed = new List<RepairProgress>();
        foreach (var progress in repairProgresses)
        {
            if (progress.isCompleted) completed.Add(progress);
        }
        return completed;
    }

    public void UseItemRepair(string repairId, DollRepairSystem.RepairTool toolType)
    {
        if (ItemManager.Instance != null)
        {
            ItemManager.ItemType requiredItem = toolType == DollRepairSystem.RepairTool.Glue
                ? ItemManager.ItemType.Glue
                : ItemManager.ItemType.Thread;

            if (!ItemManager.Instance.UseItem(requiredItem))
            {
                Debug.LogWarning($"Warning: Failed to consume {requiredItem} after repair completion");
            }
            else
            {
                if (showDebugInfo)
                    Debug.Log($"Consumed 1 {requiredItem} for completed repair: {repairId}");
            }
        }
    }

    #endregion

    #region Crafting System Management

    public bool CanStartCrafting(string trashId, TrashCraftingSystem.CraftTool toolType)
    {
        // ตรวจสอบสถานะเกม
        if (currentState != GameState.Normal) return false;

        // ตรวจสอบไอเทมที่จำเป็น
        if (ItemManager.Instance != null)
        {
            ItemManager.ItemType requiredItem = toolType == TrashCraftingSystem.CraftTool.Glue
                ? ItemManager.ItemType.Glue
                : ItemManager.ItemType.Thread;

            int itemCount = ItemManager.Instance.GetItemCount(requiredItem);
            if (itemCount <= 0)
            {
                if (showDebugInfo)
                    Debug.LogWarning($"Cannot start crafting - no {requiredItem} available (count: {itemCount})");
                return false;
            }
        }

        return true;
    }

    public bool StartCrafting(string trashId, TrashCraftingSystem.CraftTool toolType)
    {
        if (!CanStartCrafting(trashId, toolType)) return false;

        return ChangeState(GameState.Crafting, $"Starting crafting {trashId} with {toolType}");
    }

    public void CompleteCrafting(string trashId, TrashCraftingSystem.CraftTool toolType)
    {
        if (craftingDict.ContainsKey(trashId))
        {
            var progress = craftingDict[trashId];
            progress.totalCrafted++;
            progress.lastCraftTime = Time.time;
            progress.lastToolUsed = toolType;

            OnCraftingCompleted?.Invoke(trashId);

            if (showDebugInfo)
                Debug.Log($"Crafting completed: {trashId} using {toolType} (Total: {progress.totalCrafted})");
        }

        ChangeState(GameState.Normal, "Crafting completed");
    }

    public CraftingProgress GetCraftingProgress(string trashId)
    {
        return craftingDict.ContainsKey(trashId) ? craftingDict[trashId] : null;
    }

    public List<CraftingProgress> GetAllCraftingProgresses()
    {
        return new List<CraftingProgress>(craftingProgresses);
    }

    #endregion

    #region Other Systems

    public bool StartRopeSwinging()
    {
        return ChangeState(GameState.RopeSwinging, "Starting rope swing");
    }

    public void EndRopeSwinging()
    {
        ChangeState(GameState.Normal, "Rope swinging ended");
    }

    public bool StartPushingObject()
    {
        return ChangeState(GameState.PushingObject, "Starting object push");
    }

    public void EndPushingObject()
    {
        ChangeState(GameState.Normal, "Object pushing ended");
    }

    public void OpenMenu()
    {
        ChangeState(GameState.Menu, "Menu opened");
    }

    public void CloseMenu()
    {
        ChangeState(previousState, "Menu closed");
    }

    public void StartCutscene()
    {
        ChangeState(GameState.Cutscene, "Cutscene started");
    }

    public void EndCutscene()
    {
        ChangeState(GameState.Normal, "Cutscene ended");
    }

    public bool StartClimbing()
    {
        return ChangeState(GameState.ClimbingLadder, "Player started climbing");
    }

    public void EndClimbing()
    {
        // กลับสู่สถานะ Normal เท่านั้น ถ้าหากสถานะปัจจุบันคือ ClimbingLadder
        if (currentState == GameState.ClimbingLadder)
        {
            ChangeState(GameState.Normal, "Player ended climbing");
        }
    }

    public bool StartDialogState()
    {
        return ChangeState(GameState.InDialog, "Player is in dialog");
    }

    public void EndDialogState()
    {
        // กลับสู่สถานะ Normal เท่านั้น ถ้าหากสถานะปัจจุบันคือ InDialog
        if (currentState == GameState.InDialog)
        {
            ChangeState(previousState, "Player finished dialog, returning to previous state");
        }
    }
    

    #endregion

    #region Input Handling

    void HandleGlobalInput()
    {
        // ESC - เปิด/ปิดเมนู
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentState == GameState.Menu)
                CloseMenu();
            else if (CanChangeToState(GameState.Menu))
                OpenMenu();
        }
    }

    #endregion

    #region Debug & UI

    void DisplayDebugInfo()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            Debug.Log("=== Game Manager Debug Info ===");
            Debug.Log($"Current State: {currentState}");
            Debug.Log($"Previous State: {previousState}");
            Debug.Log($"Completed Repairs: {GetCompletedRepairs().Count}/{repairProgresses.Count}");
            Debug.Log($"Total Crafting Sessions: {craftingProgresses.Count}");

            foreach (var progress in repairProgresses)
            {
                string status = progress.isCompleted ? "✓" : "✗";
                Debug.Log($"{status} {progress.repairId} - {progress.repairName}");
            }

            foreach (var progress in craftingProgresses)
            {
                Debug.Log($"🔨 {progress.trashId} - {progress.trashName}: {progress.totalCrafted} items crafted");
            }
        }
    }

    //void OnGUI()
    //{
    //    if (!showDebugInfo) return;

    //    GUILayout.BeginArea(new Rect(10, 10, 300, 250));
    //    GUILayout.Label($"Game State: {currentState}", GUI.skin.box);
    //    GUILayout.Label($"Repairs: {GetCompletedRepairs().Count}/{repairProgresses.Count}", GUI.skin.box);

    //    int totalCrafted = 0;
    //    foreach (var progress in craftingProgresses)
    //    {
    //        totalCrafted += progress.totalCrafted;
    //    }
    //    GUILayout.Label($"Items Crafted: {totalCrafted}", GUI.skin.box);

    //    if (GUILayout.Button("Toggle Debug"))
    //    {
    //        showDebugInfo = !showDebugInfo;
    //    }

    //    GUILayout.EndArea();
    //}

    #endregion

    #region Full Debug Reset

    private void HandleDebugResetInput()
    {
        if (Input.GetKeyDown(KeyCode.F2))
        {
            ClearPlayedDialogsHistory();
            DebugResetAll();
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            ClearPlayedDialogsHistory();
            // (Optional) เพิ่ม Debug Log เพื่อให้รู้ว่าทำงานแล้ว
            Debug.LogWarning("[GameManager] ประวัติ Dialog ถูกล้างด้วยปุ่มลัด (F3)!");
        }
    }

    public void DebugResetAll()
    {
        Debug.Log("=== Debug Reset All Triggered ===");

        // Reset Game State
        ChangeState(GameState.Normal, "Debug Reset");

        // Reset Checkpoint
        if (defaultCheckpoint != null)
        {
            SetActiveCheckpoint(defaultCheckpoint);
            defaultCheckpoint.ActivateCheckpoint();
            Debug.Log($"Checkpoint reset to default: {defaultCheckpoint.GetCheckpointID()}");
        }

        // Reset Player Position
        if (player != null)
        {
            player.position = defaultSpawnPosition;
            if (playerDeath != null)
                playerDeath.Respawn(defaultSpawnPosition);
            Debug.Log($"Player position reset to default: {defaultSpawnPosition}");
        }



        // ล้างรายการ Checkpoint ที่เคยได้โบนัสไปแล้ว
        triggeredBonusCheckpointIDs.Clear();

        // ล้างรายการ Item ในฉากที่เก็บไปแล้ว (ถ้ามีระบบเก็บของตามฉาก)
        collectedItemIDs.Clear();

        // ล้างข้อมูลในไฟล์เซฟ (Disk)
        ClearCheckpointSaveData();
        PlayerPrefs.DeleteKey("Saved_Glue");
        PlayerPrefs.DeleteKey("Saved_Thread");
        PlayerPrefs.DeleteKey(BONUS_TRIGGERED_KEY);
        PlayerPrefs.DeleteKey("CurrentBossPhase");
        // รีเซ็ตจำนวนไอเทมในตัวผู้เล่นให้เป็น 0 ทันที (ไม่ต้องรอโหลด)
        if (ItemManager.Instance != null)
        {
            ItemManager.Instance.SetItemCount(ItemManager.ItemType.Glue, 0);
            ItemManager.Instance.SetItemCount(ItemManager.ItemType.Thread, 0);
            ItemManager.Instance.UpdateUI();
        }

        PlayerPrefs.SetInt("GlueCount", 0);
        PlayerPrefs.SetInt("ThreadCount", 0);
        PlayerPrefs.Save();

        //// Clear PlayerPrefs
        //ClearCheckpointSaveData();
        //PlayerPrefs.DeleteKey("Saved_Glue");   // ลบตัวนี้
        //PlayerPrefs.DeleteKey("Saved_Thread"); // ลบตัวนี้
        //PlayerPrefs.DeleteKey(BONUS_TRIGGERED_KEY); // ลบประวัติโบนัส

        PlayerPrefs.Save();

        // Reset Repair Progress
        foreach (var progress in repairProgresses)
        {
            progress.isCompleted = false;
            progress.completionTime = 0f;
            progress.toolUsed = default;
        }
        Debug.Log("All repair progress reset.");

        // Reset Crafting Progress
        foreach (var progress in craftingProgresses)
        {
            progress.totalCrafted = 0;
            progress.lastCraftTime = 0f;
            progress.lastToolUsed = default;
        }
        Debug.Log("All crafting progress reset.");

        // Reset Items
        if (ItemManager.Instance != null)
        {
            ItemManager.Instance.UpdateUI();
            Debug.Log("All player items reset.");
        }

        // Clear PlayerPrefs
        ClearCheckpointSaveData();
        PlayerPrefs.SetInt("GlueCount", 0);
        PlayerPrefs.SetInt("ThreadCount", 0);
        PlayerPrefs.Save();
        Debug.Log("PlayerPrefs cleared for debug reset.");

        Debug.Log("=== Debug Reset Complete ===");
    }

    #endregion


    #region QuitGameClearSave
    private void OnApplicationQuit()
    {
        // เมื่อผู้เล่นกดกากบาทปิดเกม หรือ Alt+F4
        // ให้ลบเซฟเฟสบอสทิ้งทันที
        PlayerPrefs.DeleteKey("CurrentBossPhase");
        PlayerPrefs.Save();
        Debug.Log("Auto-Cleared Boss Phase Save on Quit.");
    }

    public void QuitToMainMenu()
    {
        // 1. ล้างข้อมูลเฟสบอสทิ้ง (เพื่อให้เริ่มใหม่เมื่อเข้าเล่นครั้งหน้า)
        PlayerPrefs.DeleteKey("CurrentBossPhase");
        PlayerPrefs.Save();
        Debug.Log("Cleared Boss Phase Save (User Quit).");

        // 2. ถ้ามีของอื่นๆ ที่อยากล้างตอนออกเกมก็ใส่ตรงนี้
        // ClearCheckpointSaveData(); // ถ้าอยากให้ Checkpoint หายด้วยก็เปิดบรรทัดนี้

        // 3. เปลี่ยนฉากกลับไปเมนู (ใส่ชื่อ Scene เมนูของคุณ)
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitDesktop()
    {
        // 1. ล้างข้อมูลก่อนปิดเกม
        PlayerPrefs.DeleteKey("CurrentBossPhase");
        PlayerPrefs.Save();

        // 2. ปิดโปรแกรม
        Debug.Log("Quitting Game...");
        Application.Quit();
    }

    #endregion




}




// ========================================
// Data Classes
// ========================================

[System.Serializable]
public class RepairProgress
{
    [Header("Repair Info")]
    public string repairId;
    public string repairName;
    public bool isCompleted = false;

    [Header("Progress Details")]
    public float completionTime;
    public DollRepairSystem.RepairTool toolUsed;

    [Header("Requirements (Optional)")]
    public List<string> prerequisiteRepairs = new List<string>();

    public RepairProgress(string id, string name)
    {
        repairId = id;
        repairName = name;
    }
}

[System.Serializable]
public class CraftingProgress
{
    [Header("Crafting Info")]
    public string trashId;            // ID เฉพาะของกองขยะ
    public string trashName;          // ชื่อที่แสดงผล
    public int totalCrafted = 0;      // จำนวนรวมที่ประดิษฐ์มาแล้ว

    [Header("Progress Details")]
    public float lastCraftTime;                         // เวลาล่าสุดที่ประดิษฐ์
    public TrashCraftingSystem.CraftTool lastToolUsed; // เครื่องมือที่ใช้ล่าสุด

    public CraftingProgress(string id, string name)
    {
        trashId = id;
        trashName = name;
    }
}

// ========================================
// Player Controller Interface
// ========================================

public class PlayerController : MonoBehaviour
{
    [Header("Control Settings")]
    public float moveSpeed = 5f;
    public bool controlEnabled = true;
    public bool movementEnabled = true;
    public bool interactionEnabled = true;

    private float originalMoveSpeed;
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        originalMoveSpeed = moveSpeed;
    }

    void Update()
    {
        if (!controlEnabled) return;

        HandleMovement();
        HandleInteraction();
    }

    void HandleMovement()
    {
        if (!movementEnabled) return;

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector2 movement = new Vector2(horizontal, vertical) * moveSpeed;

        if (rb != null)
        {
            rb.linearVelocity = movement;
        }
        else
        {
            transform.Translate(movement * Time.deltaTime);
        }
    }

    void HandleInteraction()
    {
        if (!interactionEnabled) return;

        // Handle interaction inputs here
        // This will be processed by individual systems
    }

    #region Public Control Methods

    public void SetControlEnabled(bool enabled)
    {
        controlEnabled = enabled;
        if (!enabled && rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    public void SetMovementEnabled(bool enabled, float speedMultiplier = 1f)
    {
        movementEnabled = enabled;
        moveSpeed = originalMoveSpeed * speedMultiplier;

        if (!enabled && rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
    }

    #endregion


}