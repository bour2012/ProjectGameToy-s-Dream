using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.VisualScripting.Dependencies.Sqlite.SQLite3;

public enum GameState
{
    Normal,           // สถานะปกติ สามารถเดินและโต้ตอบได้
    RepairingGlue,    // กำลังซ่อมด้วยกาว
    RepairingThread,  // กำลังซ่อมด้วยด้าย
    RopeSwinging,     // กำลังโหนเชือก
    PushingObject,    // กำลังดันของ
    Menu,             // เมนู/หยุดชั่วคราว
    Cutscene          // ดูฉาก
}

public class GameManager : MonoBehaviour
{
    [Header("Game State")]
    public GameState currentState = GameState.Normal;

    [Header("Player Reference")]
    public Transform player;
    public PlayerController playerController;

    [Header("Repair Progress Tracking")]
    public List<RepairProgress> repairProgresses = new List<RepairProgress>();

    [Header("Debug")]
    public bool showDebugInfo = true;

    // Events
    public static System.Action<GameState> OnGameStateChanged;
    public static System.Action<string> OnRepairCompleted;
    public static System.Action OnAllRepairsCompleted;

    // Singleton
    public static GameManager Instance { get; private set; }

    private GameState previousState = GameState.Normal;
    private Dictionary<string, RepairProgress> repairDict = new Dictionary<string, RepairProgress>();

    void Awake()
    {
        // Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeManager();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        SetupReferences();
        BuildRepairDictionary();
    }

    void Update()
    {
        HandleGlobalInput();
        if (showDebugInfo) DisplayDebugInfo();
    }

    #region Initialization

    void InitializeManager()
    {
        // ค้นหา Player อัตโนมัติถ้ายังไม่ได้ระบุ
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (playerController == null && player != null)
            playerController = player.GetComponent<PlayerController>();
    }

    // เพิ่มใน GameManager.cs ในส่วน SetupReferences()

    void SetupReferences()
    {
        // ค้นหาและเชื่อมต่อกับระบบต่างๆ ในเกม
        var dollSystems = FindObjectsByType<DollRepairSystem>(FindObjectsSortMode.None);

        foreach (var system in dollSystems)
        {
            // ให้ DollRepairSystem อ้างอิงถึง GameManager
            system.gameManager = this;

            // เพิ่ม RepairProgress ใน List หากยังไม่มี
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
    }

    // เพิ่ม Method ใหม่สำหรับตรวจสอบการซ่อม
    public bool CanInteractWithRepairSystem()
    {
        return currentState == GameState.Normal;
    }

    public List<DollRepairSystem> GetAllRepairSystems()
    {
        return new List<DollRepairSystem>(FindObjectsByType<DollRepairSystem>(FindObjectsSortMode.None));
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
        switch (currentState)
        {
            case GameState.Normal:
                return true; // สามารถเปลี่ยนไปสถานะใดก็ได้

            case GameState.RepairingGlue:
            case GameState.RepairingThread:
                // ออกจากระบบซ่อมได้เฉพาะไป Normal หรือ Menu
                return targetState == GameState.Normal || targetState == GameState.Menu;

            case GameState.RopeSwinging:
                // ระหว่างโหนสามารถหยุดได้
                return targetState == GameState.Normal || targetState == GameState.Menu;

            case GameState.PushingObject:
                // ระหว่างดันของสามารถหยุดได้
                return targetState == GameState.Normal || targetState == GameState.Menu;

            case GameState.Menu:
                // จากเมนูสามารถกลับไปสถานะเดิมได้
                return targetState == previousState || targetState == GameState.Normal;

            case GameState.Cutscene:
                // ระหว่างฉากไม่สามารถเปลี่ยนสถานะได้ จนกว่าจะจบ
                return targetState == GameState.Normal;

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
                EnablePlayerControl(true);
                break;

            case GameState.RepairingGlue:
            case GameState.RepairingThread:
                EnablePlayerControl(false);
                break;

            case GameState.RopeSwinging:
                EnablePlayerMovement(false);
                EnablePlayerInteraction(false);
                break;

            case GameState.PushingObject:
                EnablePlayerMovement(true, 0.5f); // ลดความเร็ว
                EnablePlayerInteraction(false);
                break;

            case GameState.Menu:
            case GameState.Cutscene:
                EnablePlayerControl(false);
                break;
        }
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

        // ตรวจสอบไอเทมที่จำเป็น - เช็คว่ามีพอใช้หรือไม่
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

    // แก้ไข StartRepair - ไม่ใช้ไอเทมที่นี่แล้ว
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

            // แจ้งเหตุการณ์
            OnRepairCompleted?.Invoke(repairId);

            if (showDebugInfo)
                Debug.Log($"Repair completed: {repairId} using {toolType}");
        }

        // กลับสู่สถานะปกติ
        ChangeState(GameState.Normal, "Repair completed");

        // ตรวจสอบว่าซ่อมครบทุกตัวแล้วหรือยัง
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

    public void UseItemRepair (string repairId, DollRepairSystem.RepairTool toolType)
    {
        if (ItemManager.Instance != null)
        {
            ItemManager.ItemType requiredItem = toolType == DollRepairSystem.RepairTool.Glue
                ? ItemManager.ItemType.Glue
                : ItemManager.ItemType.Thread;

            if (!ItemManager.Instance.UseItem(requiredItem))
            {
                Debug.LogWarning($"Warning: Failed to consume {requiredItem} after repair completion");
                // ไม่ return เพราะการซ่อมเสร็จแล้ว แค่แจ้งเตือน
            }
            else
            {
                if (showDebugInfo)
                    Debug.Log($"Consumed 1 {requiredItem} for completed repair: {repairId}");
            }
        }
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
        // แสดงข้อมูล Debug บน Console หรือ UI
        if (Input.GetKeyDown(KeyCode.F1))
        {
            Debug.Log("=== Game Manager Debug Info ===");
            Debug.Log($"Current State: {currentState}");
            Debug.Log($"Previous State: {previousState}");
            Debug.Log($"Completed Repairs: {GetCompletedRepairs().Count}/{repairProgresses.Count}");

            foreach (var progress in repairProgresses)
            {
                string status = progress.isCompleted ? "✓" : "✗";
                Debug.Log($"{status} {progress.repairId} - {progress.repairName}");
            }
        }
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"Game State: {currentState}", GUI.skin.box);
        GUILayout.Label($"Repairs: {GetCompletedRepairs().Count}/{repairProgresses.Count}", GUI.skin.box);

        if (GUILayout.Button("Toggle Debug"))
        {
            showDebugInfo = !showDebugInfo;
        }

        GUILayout.EndArea();
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
    public string repairId;           // ID เฉพาะของการซ่อม
    public string repairName;         // ชื่อที่แสดงผล
    public bool isCompleted = false;  // สถานะการเสร็จสิ้น

    [Header("Progress Details")]
    public float completionTime;                    // เวลาที่เสร็จสิ้น
    public DollRepairSystem.RepairTool toolUsed;    // เครื่องมือที่ใช้ซ่อม

    [Header("Requirements (Optional)")]
    public List<string> prerequisiteRepairs = new List<string>(); // ต้องซ่อมอะไรก่อน

    public RepairProgress(string id, string name)
    {
        repairId = id;
        repairName = name;
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