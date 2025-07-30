using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TrashCraftingSystem : MonoBehaviour
{
    [Header("Crafting Settings")]
    public float interactionDistance = 3f;
    public LayerMask playerLayer = 1;
    public float craftingTime = 0.5f;

    [Header("Crafting Identity")]
    public string trashId = "trash_pile_01";
    public string trashName = "Trash Pile";

    [Header("Craftable Items")]
    public CraftableItem boxItem;
    public CraftableItem dollItem;

    [Header("UI References")]
    public GameObject craftingUIContainer;
    public Transform iconHolder;
    public GameObject glueIcon;
    public GameObject threadIcon;
    public GameObject interactPrompt;

    [Header("Progress UI")]
    public GameObject progressPanel;
    public TextMeshProUGUI progressText;
    public UnityEngine.UI.Slider progressBar;

    [Header("Visual Effects")]
    public ParticleSystem craftingEffect;
    public AudioSource craftingSound;

    // GameManager reference
    [HideInInspector] public GameManager gameManager;

    private Transform player;
    private bool isPlayerNear = false;
    private bool isCrafting = false;
    private CraftTool currentTool = CraftTool.Glue;
    private Vector3 originalUIScale;
    private GameObject currentCraftedObject;

    [System.Serializable]
    public class CraftableItem
    {
        [Header("Basic Info")]
        public string itemName;
        public GameObject prefab;
        public float lifetime = 2f; // เวลาที่อยู่ได้ (วินาที)

        [Header("Properties")]
        public bool canBePushed = true;
        public bool canDistractEnemies = false;
        public bool canBePossessed = false; // สำหรับตุ๊กตา

        [Header("Combat (for possessed dolls)")]
        public int maxHitPoints = 3; // จำนวนครั้งที่ต้องโจมตีเพื่อทำลาย

        [Header("Visual Effects")]
        public ParticleSystem spawnEffect;
        public ParticleSystem destroyEffect;
        public AudioClip spawnSound;
        public AudioClip destroySound;
    }

    public enum CraftTool
    {
        Glue,   // สร้างกล่อง
        Thread  // สร้างตุ๊กตา
    }

    void Start()
    {
        // หาผู้เล่นและ GameManager
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        gameManager = GameManager.Instance;

        // เตรียม UI
        SetupUI();
        UpdateToolSelection();

        // สมัครรับการแจ้งเตือนจาก GameManager
        if (gameManager != null)
        {
            GameManager.OnGameStateChanged += OnGameStateChanged;
        }
    }

    void SetupUI()
    {
        craftingUIContainer.SetActive(false);
        interactPrompt.SetActive(false);
        if (progressPanel != null) progressPanel.SetActive(false);

        originalUIScale = craftingUIContainer.transform.localScale;
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
        if (gameManager == null) return;

        CheckPlayerDistance();
        UpdateProgressUI(); // เพิ่มบรรทัดนี้

        // รับ Input เฉพาะเมื่ออยู่ในสถานะปกติ
        if (gameManager.currentState == GameState.Normal)
        {
            HandleNormalStateInput();
        }
        else if (IsInCraftingState())
        {
            HandleCraftingInput();
        }
    }

    #region Game State Management

    void OnGameStateChanged(GameState newState)
    {
        switch (newState)
        {
            case GameState.Normal:
                if (isPlayerNear && !isCrafting)
                {
                    ToggleCraftingUI(true);
                }
                break;

            case GameState.Crafting:
                if (!IsMyCraftingSystem())
                {
                    ToggleCraftingUI(false);
                }
                break;

            default:
                ToggleCraftingUI(false);
                break;
        }
    }

    bool IsInCraftingState()
    {
        return gameManager.currentState == GameState.Crafting;
    }

    bool IsMyCraftingSystem()
    {
        return isCrafting;
    }

    #endregion

    #region Progress UI

    void UpdateProgressUI()
    {
        if (progressPanel != null)
        {
            if (isCrafting)
            {
                progressPanel.SetActive(true);

                if (progressText != null)
                {
                    progressText.text = "Crafting...";
                }
            }
            else
            {
                progressPanel.SetActive(false);
            }

            // แสดง progress bar ระหว่างการคราฟ
            if (progressBar != null)
            {
                progressBar.value = isCrafting ? progressBar.value : 0f;
            }
        }
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
            ToggleCraftingUI(shouldShowUI && !isCrafting);
        }

        if (isPlayerNear && craftingUIContainer.activeInHierarchy)
        {
            UpdateUIPosition();
        }
    }

    void ToggleCraftingUI(bool show)
    {
        if (isCrafting) return;

        if (show && gameManager.currentState == GameState.Normal)
        {
            craftingUIContainer.SetActive(true);
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
        craftingUIContainer.transform.position = transform.position + Vector3.up * 2f;
        progressPanel.transform.position = transform.position + Vector3.up;
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

        // เริ่มการประดิษฐ์ด้วยการกด E
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryStartCrafting();
        }
    }

    void HandleCraftingInput()
    {
        if (IsMyCraftingSystem() && Input.GetKeyDown(KeyCode.Escape))
        {
            StopCrafting();
        }
    }

    #endregion

    #region Crafting System

    void TryStartCrafting()
    {
        if (isCrafting || gameManager == null) return;

        // ตรวจสอบไอเทมที่จำเป็น
        if (ItemManager.Instance != null)
        {
            ItemManager.ItemType requiredItem = currentTool == CraftTool.Glue
                ? ItemManager.ItemType.Glue
                : ItemManager.ItemType.Thread;

            int itemCount = ItemManager.Instance.GetItemCount(requiredItem);
            if (itemCount <= 0)
            {
                Debug.Log($"Cannot craft - no {requiredItem} available (remaining: {itemCount})");
                return;
            }
        }

        // ขอเริ่มการประดิษฐ์ผ่าน GameManager
        if (gameManager.StartCrafting(trashId, currentTool))
        {
            StartCrafting();
        }
        else
        {
            Debug.LogWarning($"Cannot start crafting {trashId} - GameManager denied request");
        }
    }

    void StartCrafting()
    {
        isCrafting = true;
        craftingUIContainer.SetActive(false);
        interactPrompt.SetActive(false);

        StartCoroutine(CraftingProcess());

        Debug.Log($"Started crafting with {currentTool}");
    }

    IEnumerator CraftingProcess()
    {
        // แสดง Progress UI
        if (progressPanel != null)
        {
            progressPanel.SetActive(true);
            progressText.text = "Crafting...";
            Debug.Log("Crafting....");
        }

        // เอฟเฟกต์การประดิษฐ์
        if (craftingEffect) craftingEffect.Play();
        if (craftingSound) craftingSound.Play();

        // รอเวลาการประดิษฐ์
        float elapsedTime = 0f;
        while (elapsedTime < craftingTime)
        {
            elapsedTime += Time.deltaTime;

            // อัปเดต progress bar
            if (progressBar != null)
            {
                progressBar.value = elapsedTime / craftingTime;
            }

            yield return null;
        }

        // ประดิษฐ์เสร็จสิ้น
        CompleteCrafting();
    }

    void CompleteCrafting()
    {
        UseItemForCrafting();
        CreateCraftedObject();

        // ซ่อนตัวกองขยะ
        gameObject.SetActive(false);

        isCrafting = false;

        if (gameManager != null)
        {
            gameManager.CompleteCrafting(trashId, currentTool);
        }

        Debug.Log($"Crafting completed with {currentTool}");
    }
    void StopCrafting()
    {
        if (!isCrafting) return;

        StopAllCoroutines();

        // ปิดเอฟเฟกต์
        if (craftingEffect) craftingEffect.Stop();
        if (craftingSound) craftingSound.Stop();

        // รีเซ็ตสถานะ
        isCrafting = false;

        // แจ้ง GameManager
        if (gameManager != null && IsInCraftingState())
        {
            gameManager.ChangeState(GameState.Normal, "Crafting cancelled");
        }

        // แสดง UI กลับมาถ้าผู้เล่นยังอยู่ใกล้
        if (isPlayerNear)
        {
            ToggleCraftingUI(true);
        }

        // Progress Panel จะถูกจัดการโดย UpdateProgressUI() แล้ว

        Debug.Log("Crafting cancelled");
    }
    void CreateCraftedObject()
    {
        CraftableItem itemToCreate = currentTool == CraftTool.Glue ? boxItem : dollItem;

        if (itemToCreate.prefab != null)
        {
            // สร้างวัตถุในตำแหน่งของกองขยะ
            GameObject craftedObj = Instantiate(itemToCreate.prefab, transform.position, transform.rotation);

            // เพิ่ม CraftedObject component
            CraftedObject craftedComponent = craftedObj.GetComponent<CraftedObject>();
            if (craftedComponent == null)
            {
                craftedComponent = craftedObj.AddComponent<CraftedObject>();
            }

            // ตั้งค่า CraftedObject
            craftedComponent.Initialize(itemToCreate, this);

            // เก็บ reference
            currentCraftedObject = craftedObj;

            // เอฟเฟกต์การสร้าง
            if (itemToCreate.spawnEffect)
            {
                Instantiate(itemToCreate.spawnEffect, transform.position, transform.rotation);
            }

            Debug.Log($"Created {itemToCreate.itemName} at {transform.position}");
        }
    }

    void UseItemForCrafting()
    {
        if (ItemManager.Instance != null)
        {
            ItemManager.ItemType requiredItem = currentTool == CraftTool.Glue
                ? ItemManager.ItemType.Glue
                : ItemManager.ItemType.Thread;

            if (!ItemManager.Instance.UseItem(requiredItem))
            {
                Debug.LogWarning($"Warning: Failed to consume {requiredItem} after crafting");
            }
            else
            {
                Debug.Log($"Consumed 1 {requiredItem} for crafting");
            }
        }
    }

    #endregion

    #region Tool Management

    void SwitchTool(bool forward)
    {
        if (IsInCraftingState()) return;

        currentTool = (CraftTool)(((int)currentTool + (forward ? 1 : -1) + 2) % 2);
        UpdateToolSelection();
        StartCoroutine(ToolSwitchAnimation());
    }

    void UpdateToolSelection()
    {
        if (glueIcon != null) glueIcon.SetActive(currentTool == CraftTool.Glue);
        if (threadIcon != null) threadIcon.SetActive(currentTool == CraftTool.Thread);

        // เอฟเฟกต์ Highlight
        Transform activeIcon = currentTool == CraftTool.Glue ?
            glueIcon?.transform : threadIcon?.transform;

        if (activeIcon != null)
        {
            activeIcon.localScale = Vector3.one * 1.2f;
        }

        UpdateInteractPrompt();
    }

    void UpdateInteractPrompt()
    {
        if (isPlayerNear && !isCrafting && gameManager.currentState == GameState.Normal)
        {
            interactPrompt.SetActive(true);

            string toolName = currentTool == CraftTool.Glue ? "Glue" : "Thread";
            string itemName = currentTool == CraftTool.Glue ? boxItem.itemName : dollItem.itemName;

            var promptText = interactPrompt.GetComponent<TextMeshProUGUI>();
            if (promptText != null)
            {
                // ตรวจสอบไอเทม
                if (ItemManager.Instance != null)
                {
                    ItemManager.ItemType requiredItem = currentTool == CraftTool.Glue
                        ? ItemManager.ItemType.Glue
                        : ItemManager.ItemType.Thread;

                    int itemCount = ItemManager.Instance.GetItemCount(requiredItem);

                    if (itemCount > 0)
                    {
                        promptText.text = $"Press E to craft {itemName} with {toolName} [{itemCount}]";
                        promptText.color = Color.white;
                    }
                    else
                    {
                        promptText.text = $"Need {toolName} to craft {itemName} [{itemCount}]";
                        promptText.color = Color.red;
                    }
                }
                else
                {
                    promptText.text = $"Press E to craft {itemName} with {toolName}";
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

    IEnumerator UIAppearAnimation()
    {
        if (craftingUIContainer == null) yield break;

        craftingUIContainer.transform.localScale = Vector3.zero;
        float time = 0;
        while (time < 0.3f)
        {
            time += Time.deltaTime;
            float progress = time / 0.3f;
            craftingUIContainer.transform.localScale = Vector3.Lerp(Vector3.zero, originalUIScale,
                Mathf.Sin(progress * Mathf.PI * 0.5f));
            yield return null;
        }
        craftingUIContainer.transform.localScale = originalUIScale;
    }

    IEnumerator UIDisappearAnimation()
    {
        if (craftingUIContainer == null) yield break;

        float time = 0;
        Vector3 startScale = craftingUIContainer.transform.localScale;
        while (time < 0.2f && craftingUIContainer != null)
        {
            time += Time.deltaTime;
            float progress = time / 0.2f;
            craftingUIContainer.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, progress);
            yield return null;
        }

        if (craftingUIContainer != null)
        {
            craftingUIContainer.SetActive(false);
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

    public string GetTrashId()
    {
        return trashId;
    }

    public CraftTool GetCurrentTool()
    {
        return currentTool;
    }

    public bool IsPlayerInRange()
    {
        return isPlayerNear;
    }

    public bool IsCraftingInProgress()
    {
        return isCrafting;
    }

    /// <summary>
    /// เรียกจาก CraftedObject เมื่อวัตถุถูกทำลายและต้องกลับมาเป็นกองขยะ
    /// </summary>
    public void OnCraftedObjectDestroyed(Vector3 position)
    {
        transform.position = position;
        currentCraftedObject = null;

        // เปิดกองขยะกลับมาให้คราฟได้อีก
        gameObject.SetActive(true);

        Debug.Log($"Trash pile returned to position: {position}");
    }

    #endregion

    #region Debug

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isCrafting ? Color.blue : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);

        if (isCrafting)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
        }
    }

    #endregion
}