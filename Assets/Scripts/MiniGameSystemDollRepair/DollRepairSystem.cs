using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Linq;

public class DollRepairSystem : MonoBehaviour
{
    [Header("Repair Settings")]
    public float interactionDistance = 3f;
    public LayerMask playerLayer = 1;

    [Header("Repair Identity")]
    public string repairId = "doll_repair_01";
    public string repairName = "Broken Doll";

    [Header("Required Repair Systems")]
    [SerializeField] private List<RepairSystemConfig> repairSystems = new List<RepairSystemConfig>();

    [Header("UI References")]
    public GameObject repairUIContainer;
    public Transform iconHolder;
    public GameObject glueIcon;
    public GameObject threadIcon;
    public GameObject interactPrompt;
    public Animator dollAnimator;

    [Header("Progress UI")]
    public GameObject progressPanel;
    public TextMeshProUGUI progressText;
    public UnityEngine.UI.Slider progressBar;

    [Header("Visual Effects")]
    public ParticleSystem repairEffect;
    public AudioSource repairSound;

    // GameManager reference
    [HideInInspector] public GameManager gameManager;

    private Transform player;
    private bool isPlayerNear = false;
    private bool isDollRepaired = false;
    private RepairTool currentTool = RepairTool.Glue;
    private Vector3 originalUIScale;
    private GameObject currentActiveSystem;

    // ติดตามความคืบหน้าของแต่ละระบบ
    private Dictionary<string, bool> systemCompletionStatus = new Dictionary<string, bool>();
    private int totalRequiredSystems = 0;
    private int completedSystems = 0;

    [System.Serializable]
    public class RepairSystemConfig
    {
        public string systemName;           // เช่น "Puzzle", "Sewing"
        public RepairTool requiredTool;     // เครื่องมือที่ต้องใช้
        public GameObject systemObject;     // GameObject ของระบบนั้น
        public bool isRequired = true;      // บังคับต้องทำหรือไม่
        [HideInInspector] public bool isCompleted = false;
    }

    public enum RepairTool
    {
        Glue,
        Thread
    }

    void Start()
    {
        InitializeRepairSystems();

        // หาผู้เล่นและ GameManager
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        gameManager = GameManager.Instance;

        // ตรวจสอบว่าตุ๊กตานี้ซ่อมแล้วหรือยัง
        if (gameManager != null && gameManager.IsRepairCompleted(repairId))
        {
            isDollRepaired = true;
            SetDollRepairedVisual();
            return;
        }

        // เตรียม UI
        SetupUI();
        CloseAllRepairSystems();
        UpdateToolSelection();

        // สมัครรับการแจ้งเตือนจาก GameManager
        if (gameManager != null)
        {
            GameManager.OnGameStateChanged += OnGameStateChanged;
        }
    }

    void InitializeRepairSystems()
    {
        // นับระบบที่จำเป็นต้องทำ
        totalRequiredSystems = repairSystems.Count(sys => sys.isRequired);

        // เตรียม dictionary สำหรับติดตาม status
        systemCompletionStatus.Clear();
        foreach (var system in repairSystems)
        {
            systemCompletionStatus[system.systemName] = false;
        }

        Debug.Log($"Initialized {repairSystems.Count} repair systems ({totalRequiredSystems} required)");
    }

    void SetupUI()
    {
        repairUIContainer.SetActive(false);
        interactPrompt.SetActive(false);
        if (progressPanel != null) progressPanel.SetActive(false);

        originalUIScale = repairUIContainer.transform.localScale;
    }

    void OnDestroy()
    {
        if (gameManager != null)
        {
            GameManager.OnGameStateChanged -= OnGameStateChanged;
        }
    }

    void Update()
    {
        if (isDollRepaired || gameManager == null) return;

        CheckPlayerDistance();
        UpdateProgressUI();

        // รับ Input เฉพาะเมื่ออยู่ในสถานะปกติ
        if (gameManager.currentState == GameState.Normal)
        {
            HandleNormalStateInput();
        }
        else if (IsInRepairState())
        {
            HandleRepairSystemInput();
        }
    }

    #region System Completion Tracking

    /// <summary>
    /// เรียกจากระบบย่อยเมื่อทำเสร็จแล้ว
    /// </summary>
    public void OnSubSystemCompleted(string systemName)
    {
        if (isDollRepaired) return;

        if (systemCompletionStatus.ContainsKey(systemName))
        {
            if (!systemCompletionStatus[systemName]) // ป้องกันการนับซ้ำ
            {
                systemCompletionStatus[systemName] = true;

                // อัปเดต config
                var systemConfig = repairSystems.FirstOrDefault(s => s.systemName == systemName);
                if (systemConfig != null)
                {
                    systemConfig.isCompleted = true;
                }

                completedSystems++;
                Debug.Log($"✅ System '{systemName}' completed! ({completedSystems}/{totalRequiredSystems})");
                gameManager.UseItemRepair(repairId, currentTool);
                // ตรวจสอบว่าทำครบทุกระบบแล้วหรือไม่
                CheckOverallCompletion();
            }
        }
        else
        {
            Debug.LogWarning($"Unknown system name: {systemName}");
        }
    }

    void CheckOverallCompletion()
    {
        // ตรวจสอบว่าระบบที่จำเป็นทั้งหมดเสร็จแล้วหรือไม่
        int requiredCompleted = repairSystems.Count(sys => sys.isRequired && sys.isCompleted);

        if (requiredCompleted >= totalRequiredSystems)
        {
            StartCoroutine(CompleteAllRepairs());
        }
    }

    IEnumerator CompleteAllRepairs()
    {
        Debug.Log("🎉 All required systems completed! Finishing doll repair...");

        // ปิดระบบที่เปิดอยู่
        CloseAllRepairSystems();

        // เอฟเฟกต์การซ่อมเสร็จ
        if (repairEffect) repairEffect.Play();
        if (repairSound) repairSound.Play();

        //dollAnimator?.SetTrigger("RepairComplete");

        yield return new WaitForSeconds(1f);

        // แจ้ง GameManager
        if (gameManager != null)
        {
            gameManager.CompleteRepair(repairId, currentTool);
        }

        //// ตั้งสถานะเป็นเสร็จสิ้น
        //isDollRepaired = true;
        //SetDollRepairedVisual();
        //ToggleRepairUI(false);

        //// แจ้ง Tutorial Manager
        //var tutorialManager = FindFirstObjectByType<TutorialManager>();
        //tutorialManager?.OnRepairComplete();

        Debug.Log($"🎊 Doll '{repairName}' repair completed!");
    }

    #endregion

    #region Progress UI

    void UpdateProgressUI()
    {
        if (progressPanel != null && isPlayerNear && !isDollRepaired)
        {
            progressPanel.SetActive(true);

            // อัปเดตข้อความความคืบหน้า
            if (progressText != null)
            {
                progressText.text = $"Progress: {completedSystems}/{totalRequiredSystems}";
            }

            // อัปเดต progress bar
            if (progressBar != null && totalRequiredSystems > 0)
            {
                progressBar.value = (float)completedSystems / totalRequiredSystems;
            }
        }
        else if (progressPanel != null)
        {
            progressPanel.SetActive(false);
        }
    }

    #endregion

    #region Game State Management

    void OnGameStateChanged(GameState newState)
    {
        switch (newState)
        {
            case GameState.Normal:
                if (isPlayerNear && !isDollRepaired)
                {
                    ToggleRepairUI(true);
                }
                break;

            case GameState.RepairingGlue:
            case GameState.RepairingThread:
                if (!IsMyRepairSystem())
                {
                    ToggleRepairUI(false);
                }
                break;

            default:
                ToggleRepairUI(false);
                break;
        }
    }

    bool IsInRepairState()
    {
        return gameManager.currentState == GameState.RepairingGlue ||
               gameManager.currentState == GameState.RepairingThread;
    }

    bool IsMyRepairSystem()
    {
        return currentActiveSystem != null && currentActiveSystem.activeInHierarchy;
    }

    #endregion

    #region Player Distance and UI Management

    void CheckPlayerDistance()
    {
        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.position);
        bool shouldShowUI = distance <= interactionDistance &&
                           gameManager.currentState == GameState.Normal;

        if (shouldShowUI != isPlayerNear)
        {
            isPlayerNear = shouldShowUI;
            ToggleRepairUI(shouldShowUI && !isDollRepaired);
        }

        if (isPlayerNear && repairUIContainer.activeInHierarchy)
        {
            UpdateUIPosition();
        }
    }

    void ToggleRepairUI(bool show)
    {
        if (isDollRepaired) return;

        if (show && gameManager.currentState == GameState.Normal)
        {
            repairUIContainer.SetActive(true);
            StartCoroutine(UIAppearAnimation());
            UpdateInteractPrompt();
        }
        else
        {
            StartCoroutine(UIDisappearAnimation());
        }
    }

    void UpdateUIPosition()
    {
        repairUIContainer.transform.position = transform.position + Vector3.up * 2f;
    }

    #endregion

    #region Input Handling

    void HandleNormalStateInput()
    {
        if (!isPlayerNear) return;

        // สลับเครื่องมือด้วย Mouse Scroll
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            SwitchTool(scroll > 0);
        }

        // เปิดระบบซ่อมแซมด้วยการกด E
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryOpenRepairSystem();
        }
    }

    void HandleRepairSystemInput()
    {
        if (IsMyRepairSystem() && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E)))
        {
            CloseRepairSystem();
        }
    }

    #endregion

    #region Repair System Management

    void TryOpenRepairSystem()
    {
        if (isDollRepaired || gameManager == null) return;

        // หาระบบที่ตรงกับเครื่องมือปัจจุบันและยังไม่เสร็จ
        var availableSystem = GetAvailableSystemForCurrentTool();
        if (availableSystem == null)
        {
            Debug.Log($"No available {currentTool} system or all {currentTool} systems completed");
            return;
        }

        // ตรวจสอบไอเทมที่จำเป็น - เช็คเฉพาะว่ามีพอใช้หรือไม่
        if (ItemManager.Instance != null)
        {
            ItemManager.ItemType requiredItem = currentTool == RepairTool.Glue
                ? ItemManager.ItemType.Glue
                : ItemManager.ItemType.Thread;

            int itemCount = ItemManager.Instance.GetItemCount(requiredItem);
            if (itemCount <= 0)
            {
                Debug.Log($"Cannot repair - no {requiredItem} available (remaining: {itemCount})");
                // อาจจะแสดง UI แจ้งเตือนที่นี่
                return;
            }
        }

        // ขอเปิดระบบซ่อมผ่าน GameManager (ไม่ใช้ไอเทมที่นี่แล้ว)
        if (gameManager.StartRepair(repairId, currentTool))
        {
            OpenRepairSystem(availableSystem);
        }
        else
        {
            Debug.LogWarning($"Cannot start repair {repairId} - GameManager denied request");
        }
    }
    RepairSystemConfig GetAvailableSystemForCurrentTool()
    {
        return repairSystems.FirstOrDefault(sys =>
            sys.requiredTool == currentTool &&
            !sys.isCompleted &&
            sys.systemObject != null
        );
    }

    void OpenRepairSystem(RepairSystemConfig systemConfig)
    {
        if (systemConfig.systemObject != null)
        {
            CloseAllRepairSystems();

            systemConfig.systemObject.SetActive(true);
            currentActiveSystem = systemConfig.systemObject;

            repairUIContainer.SetActive(false);
            interactPrompt.SetActive(false);

            var tutorialManager = FindFirstObjectByType<TutorialManager>();
            tutorialManager?.OnRepairSystemOpened(currentTool);

            Debug.Log($"Opened repair system: {systemConfig.systemName} with {currentTool}");
        }
    }

    void CloseRepairSystem()
    {
        if (currentActiveSystem == null) return;

        currentActiveSystem.SetActive(false);
        currentActiveSystem = null;

        if (isPlayerNear && !isDollRepaired && gameManager.currentState == GameState.Normal)
        {
            repairUIContainer.SetActive(true);
            UpdateInteractPrompt();
        }

        if (gameManager != null && IsInRepairState())
        {
            gameManager.ChangeState(GameState.Normal, "Repair system closed");
        }

        Debug.Log("Closed repair system");
    }

    void CloseAllRepairSystems()
    {
        foreach (var system in repairSystems)
        {
            if (system.systemObject != null)
            {
                system.systemObject.SetActive(false);
            }
        }
        currentActiveSystem = null;
    }

    #endregion

    #region Tool Management

    void SwitchTool(bool forward)
    {
        if (IsInRepairState()) return;

        currentTool = (RepairTool)(((int)currentTool + (forward ? 1 : -1) + 2) % 2);
        UpdateToolSelection();
        StartCoroutine(ToolSwitchAnimation());
    }

    void UpdateToolSelection()
    {
        if (glueIcon != null) glueIcon.SetActive(currentTool == RepairTool.Glue);
        if (threadIcon != null) threadIcon.SetActive(currentTool == RepairTool.Thread);

        // เอฟเฟกต์ Highlight
        Transform activeIcon = currentTool == RepairTool.Glue ?
            glueIcon?.transform : threadIcon?.transform;

        if (activeIcon != null)
        {
            activeIcon.localScale = Vector3.one * 1.2f;
        }

        UpdateInteractPrompt();
    }

    void UpdateInteractPrompt()
    {
        if (isPlayerNear && !isDollRepaired && gameManager.currentState == GameState.Normal)
        {
            interactPrompt.SetActive(true);

            var availableSystem = GetAvailableSystemForCurrentTool();
            string toolName = currentTool == RepairTool.Glue ? "Glue" : "Thread";

            var promptText = interactPrompt.GetComponent<TextMeshProUGUI>();
            if (promptText != null)
            {
                if (availableSystem != null)
                {
                    // ตรวจสอบไอเทม
                    if (ItemManager.Instance != null)
                    {
                        ItemManager.ItemType requiredItem = currentTool == RepairTool.Glue
                            ? ItemManager.ItemType.Glue
                            : ItemManager.ItemType.Thread;

                        int itemCount = ItemManager.Instance.GetItemCount(requiredItem);

                        if (itemCount > 0)
                        {
                            promptText.text = $"Press E to use {toolName} ({availableSystem.systemName}) [{itemCount}]";
                            promptText.color = Color.white;
                        }
                        else
                        {
                            promptText.text = $"Need {toolName} to repair [{itemCount}]";
                            promptText.color = Color.red;
                        }
                    }
                    else
                    {
                        promptText.text = $"Press E to use {toolName} ({availableSystem.systemName})";
                    }
                }
                else
                {
                    promptText.text = $"All {toolName} systems completed";
                    promptText.color = Color.gray;
                }
            }
        }
        else
        {
            interactPrompt.SetActive(false);
        }
    }
    #endregion

    #region Visual Effects

    void SetDollRepairedVisual()
    {
        dollAnimator?.SetBool("IsRepaired", true);
        CloseAllRepairSystems();
        repairUIContainer.SetActive(false);
        interactPrompt.SetActive(false);
        if (progressPanel != null) progressPanel.SetActive(false);
    }

    IEnumerator UIAppearAnimation()
    {
        if (repairUIContainer == null) yield break;

        repairUIContainer.transform.localScale = Vector3.zero;
        float time = 0;
        while (time < 0.3f)
        {
            time += Time.deltaTime;
            float progress = time / 0.3f;
            repairUIContainer.transform.localScale = Vector3.Lerp(Vector3.zero, originalUIScale,
                Mathf.Sin(progress * Mathf.PI * 0.5f));
            yield return null;
        }
        repairUIContainer.transform.localScale = originalUIScale;
    }

    IEnumerator UIDisappearAnimation()
    {
        if (repairUIContainer == null) yield break;

        float time = 0;
        Vector3 startScale = repairUIContainer.transform.localScale;
        while (time < 0.2f && repairUIContainer != null)
        {
            time += Time.deltaTime;
            float progress = time / 0.2f;
            repairUIContainer.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, progress);
            yield return null;
        }

        if (repairUIContainer != null)
        {
            repairUIContainer.SetActive(false);
        }
    }

    IEnumerator ToolSwitchAnimation()
    {
        if (iconHolder == null) yield break;

        float time = 0;
        while (time < 0.2f)
        {
            time += Time.deltaTime;
            iconHolder.rotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 20f) * 10f);
            yield return null;
        }
        iconHolder.rotation = Quaternion.identity;
    }

    #endregion

    #region Public Methods

    public bool IsRepaired()
    {
        return isDollRepaired;
    }

    public string GetRepairId()
    {
        return repairId;
    }

    public RepairTool GetCurrentTool()
    {
        return currentTool;
    }

    public bool IsPlayerInRange()
    {
        return isPlayerNear;
    }

    public float GetCompletionPercentage()
    {
        return totalRequiredSystems > 0 ? (float)completedSystems / totalRequiredSystems * 100f : 0f;
    }

    public List<string> GetCompletedSystems()
    {
        return systemCompletionStatus.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
    }

    public List<string> GetIncompleteSystems()
    {
        return systemCompletionStatus.Where(kvp => !kvp.Value).Select(kvp => kvp.Key).ToList();
    }

    /// <summary>
    /// สำหรับ Debug - บังคับให้ระบบเสร็จทันที
    /// </summary>
    [ContextMenu("Force Complete All Systems")]
    public void ForceCompleteAllSystems()
    {
        foreach (var systemName in systemCompletionStatus.Keys.ToList())
        {
            OnSubSystemCompleted(systemName);
        }
    }

    /// <summary>
    /// รีเซ็ตสถานะของระบบซ่อมแซม
    /// </summary>
    [ContextMenu("Reset Repair Progress")]
    public void ResetRepairProgress()
    {
        foreach (var system in repairSystems)
        {
            system.isCompleted = false;
        }

        foreach (var key in systemCompletionStatus.Keys.ToList())
        {
            systemCompletionStatus[key] = false;
        }

        completedSystems = 0;
        isDollRepaired = false;

        Debug.Log("Repair progress reset");
    }

    #endregion

    #region Debug

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isDollRepaired ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);

        if (isDollRepaired)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
        }

        // แสดงความคืบหน้าใน Scene View
        if (Application.isPlaying && !isDollRepaired)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 3f,
                $"Progress: {completedSystems}/{totalRequiredSystems}");
#endif
        }
    }

    #endregion
}