using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
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
    public CraftableItem[] craftableItems = new CraftableItem[2];

    [Header("UI References")]
    public GameObject craftingUIContainer;
    public Transform iconHolder;
    public GameObject[] itemIcons; // Icons for each craftable item
    public GameObject interactPrompt;
    [Tooltip("Key used to immediately return a crafted item to a trash pile (only used by CraftedObject when active)")]
    public KeyCode returnKey = KeyCode.Q;

    [Header("Progress UI")]
    public GameObject progressPanel;
    public TextMeshProUGUI progressText;
    public UnityEngine.UI.Slider progressBar;

    [Header("Visual Effects")]
    public ParticleSystem craftingEffect;
    public AudioSource craftingSound;

    [Header("Audio")]
    [Tooltip("One-shot sound played when crafting completes (PlayClipAtPoint will be used).")]
    public AudioClip craftCompleteSfx;
    [Range(0f,1f)] public float craftCompleteVolume = 1f;

    public UnityEvent onPickup;

    // GameManager reference
    [HideInInspector] public GameManager gameManager;

    private Transform player;
    private bool isPlayerNear = false;
    private bool isCrafting = false;
    private int currentItemIndex = 0;
    private Vector3 originalUIScale;
    private GameObject currentCraftedObject;

    [System.Serializable]
    public class CraftableItem
    {
        [Header("Basic Info")]
        public string itemName;
        public GameObject prefab;
        public float lifetime = 2f; // เวลาที่อยู่ได้ (วินาที)

        [Header("Required Item")]
        public ItemManager.ItemType requiredItemType = ItemManager.ItemType.Glue;
        public int requiredAmount = 1;

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

    void Start()
    {
        // หาผู้เล่นและ GameManager
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        gameManager = GameManager.Instance;

        // เตรียม UI
        SetupUI();
        UpdateItemSelection();

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

        // Make sure we have enough icons for all craftable items
        if (itemIcons.Length < craftableItems.Length)
        {
            Debug.LogWarning($"Not enough icons ({itemIcons.Length}) for craftable items ({craftableItems.Length})");
        }
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
        UpdateProgressUI();

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
        if (this == null || !gameObject.activeInHierarchy)
        {
            return; // ถ้าไม่มีชีวิตแล้ว ก็ไม่ต้องทำอะไรต่อ
        }

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

        if (isPlayerNear && craftingUIContainer != null && craftingUIContainer.activeInHierarchy)
        {
            UpdateUIPosition();
        }
    }

    void ToggleCraftingUI(bool show)
    {
        if (isCrafting) return;

        if (show && gameManager.currentState == GameState.Normal)
        {
            if (craftingUIContainer != null)
            {
                craftingUIContainer.SetActive(true);
                StartCoroutine(UIAppearAnimation());
            }
            UpdateInteractPrompt();
        }
        else
        {
            StartCoroutine(UIDisappearAnimation());
        }
    }

    void UpdateUIPosition()
    {
        if (craftingUIContainer != null)
            craftingUIContainer.transform.position = transform.position + Vector3.up * 2f;
        if (progressPanel != null)
            progressPanel.transform.position = transform.position + Vector3.up;
    }

    #endregion

    #region Input Handling

    void HandleNormalStateInput()
    {
        if (!isPlayerNear) return;

        // สลับไอเทมด้วย Q
        if (Input.GetKeyDown(KeyCode.Q))
        {
            SwitchItem();
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

    // (Deprecated) Trash no longer handles immediate-return input; CraftedObject handles its own UI/input.

    #endregion

    #region Crafting System

    void TryStartCrafting()
    {
        if (isCrafting || gameManager == null || currentItemIndex >= craftableItems.Length) return;

        // ตรวจสอบสถานะเกม
        if (gameManager.currentState != GameState.Normal)
        {
            Debug.LogWarning("Cannot start crafting - Game is not in Normal state.");
            return;
        }

        CraftableItem currentItem = craftableItems[currentItemIndex];

        // ตรวจสอบไอเทมที่จำเป็น
        if (ItemManager.Instance != null)
        {
            int itemCount = ItemManager.Instance.GetItemCount(currentItem.requiredItemType);
            if (itemCount < currentItem.requiredAmount)
            {
                Debug.Log($"Cannot craft - need {currentItem.requiredAmount} {currentItem.requiredItemType} (have: {itemCount})");
                return;
            }
        }

        // ขอเริ่มการประดิษฐ์ผ่าน GameManager
        CraftTool tool = currentItemIndex == 0 ? CraftTool.Glue : CraftTool.Thread;
        if (gameManager.StartCrafting(trashId, tool))
        {
            onPickup.Invoke();
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

        Debug.Log($"Started crafting {craftableItems[currentItemIndex].itemName}");
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

        // ประดิษฐ์เสร็จสิ้น: ให้ progress bar แตะ 100% เพื่อให้ UI อัปเดต
        if (progressBar != null) progressBar.value = 1f;
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.05f);

        // เล่นเอฟเฟกต์การคราฟและเสียง หลังจาก progress เสร็จ
        if (craftingEffect)
        {
            ParticleSystem ps = Instantiate(craftingEffect, transform.position, transform.rotation);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ps.transform.SetParent(null);
            ps.Play();
            float maxLifetime = (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants) ? main.startLifetime.constantMax : main.startLifetime.constant;
            float destroyAfter = main.duration + maxLifetime + 0.25f;
            Destroy(ps.gameObject, destroyAfter);
        }
        if (craftingSound) craftingSound.Play();
        // Optional one-shot clip for craft completion (if assigned)
        if (craftCompleteSfx != null)
        {
            AudioSource.PlayClipAtPoint(craftCompleteSfx, transform.position, craftCompleteVolume);
        }

        CompleteCrafting();
    }

    void CompleteCrafting()
    {
        UseItemForCrafting();
        CreateCraftedObject();

        // Restore original behavior: deactivate the trash GameObject; the new crafted object
        // will manage its own UI/input for immediate return.
        isCrafting = false;

        if (gameManager != null)
        {
            CraftTool tool = currentItemIndex == 0 ? CraftTool.Glue : CraftTool.Thread;
            gameManager.CompleteCrafting(trashId, tool);
        }

        Debug.Log($"Crafting completed: {craftableItems[currentItemIndex].itemName}");
        gameObject.SetActive(false);
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
        if (gameManager != null && gameManager.currentState == GameState.Crafting)
        {
            gameManager.ChangeState(GameState.Normal, "Crafting cancelled");
        }

        // แสดง UI กลับมาถ้าผู้เล่นยังอยู่ใกล้
        if (isPlayerNear)
        {
            ToggleCraftingUI(true);
        }

        Debug.Log("Crafting cancelled");
    }

    void CreateCraftedObject()
    {
        CraftableItem itemToCreate = craftableItems[currentItemIndex];

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

            // ตั้งค่า CraftedObject (pass shared UI refs, but DO NOT reparent them)
            craftedComponent.Initialize(itemToCreate, this, craftingUIContainer, progressPanel, interactPrompt, returnKey, interactionDistance);

            // เก็บ reference
            currentCraftedObject = craftedObj;

            // We use the shared `craftingEffect` for completion VFX (so it's consistent when crafting
            // completes or when trash returns). If you still want per-item spawnEffect, enable here.

            Debug.Log($"Created {itemToCreate.itemName} at {transform.position}");
        }
    }

    void UseItemForCrafting()
    {
        if (ItemManager.Instance != null && currentItemIndex < craftableItems.Length)
        {
            CraftableItem currentItem = craftableItems[currentItemIndex];

            for (int i = 0; i < currentItem.requiredAmount; i++)
            {
                if (!ItemManager.Instance.UseItem(currentItem.requiredItemType))
                {
                    Debug.LogWarning($"Warning: Failed to consume {currentItem.requiredItemType} after crafting (attempt {i + 1}/{currentItem.requiredAmount})");
                }
            }

            Debug.Log($"Consumed {currentItem.requiredAmount} {currentItem.requiredItemType} for crafting");
        }
    }

    #endregion

    #region Item Management

    void SwitchItem()
    {
        if (IsInCraftingState() || craftableItems.Length == 0) return;

        currentItemIndex = (currentItemIndex + 1) % craftableItems.Length;
        UpdateItemSelection();
        StartCoroutine(ItemSwitchAnimation());
    }

    void UpdateItemSelection()
    {
        // อัปเดต UI Icons
        for (int i = 0; i < itemIcons.Length && i < craftableItems.Length; i++)
        {
            if (itemIcons[i] != null)
            {
                itemIcons[i].SetActive(i == currentItemIndex);

                // เอฟเฟกต์ Highlight
                if (i == currentItemIndex)
                {
                    itemIcons[i].transform.localScale = Vector3.one * 1.2f;
                }
                else
                {
                    itemIcons[i].transform.localScale = Vector3.one;
                }
            }
        }

        UpdateInteractPrompt();
    }

    void UpdateInteractPrompt()
    {
        if (isPlayerNear && !isCrafting && gameManager.currentState == GameState.Normal && currentItemIndex < craftableItems.Length)
        {
            if (interactPrompt != null)
            {
                interactPrompt.SetActive(true);

                CraftableItem currentItem = craftableItems[currentItemIndex];
                var promptText = interactPrompt.GetComponent<TextMeshProUGUI>();

                if (promptText != null)
                {
                    // ตรวจสอบไอเทม
                    if (ItemManager.Instance != null)
                    {
                        int itemCount = ItemManager.Instance.GetItemCount(currentItem.requiredItemType);

                        if (itemCount >= currentItem.requiredAmount)
                        {
                            promptText.text = $"Press E to craft {currentItem.itemName} (Need: {currentItem.requiredAmount} {currentItem.requiredItemType}) [{itemCount}]";
                            promptText.color = Color.black;
                        }
                        else
                        {
                            promptText.text = $"Need {currentItem.requiredAmount} {currentItem.requiredItemType} to craft {currentItem.itemName} [{itemCount}]";
                            promptText.color = Color.red;
                        }
                    }
                    else
                    {
                        promptText.text = $"Press E to craft {currentItem.itemName}";
                    }
                }
            }
        }
        else
        {
            if (interactPrompt != null) interactPrompt.SetActive(false);
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

    IEnumerator ItemSwitchAnimation()
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
        // Keep backward compatibility - return appropriate tool based on current item
        return currentItemIndex == 0 ? CraftTool.Glue : CraftTool.Thread;
    }

    public bool IsPlayerInRange()
    {
        return isPlayerNear;
    }

    public bool IsCraftingInProgress()
    {
        return isCrafting;
    }

    public CraftableItem GetCurrentItem()
    {
        return currentItemIndex < craftableItems.Length ? craftableItems[currentItemIndex] : null;
    }

    /// <summary>
    /// เรียกจาก CraftedObject เมื่อวัตถุถูกทำลายและต้องกลับมาเป็นกองขยะ
    /// </summary>
    public void OnCraftedObjectDestroyed(Vector3 position)
    {
        // The Trash GameObject may be inactive (it was hidden when crafting started).
        // Activate it first so we can start coroutines on this MonoBehaviour, then run the return routine.
        currentCraftedObject = null;
        // Ensure the trash GameObject is active so StartCoroutine works
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }

        // Reset progress bar UI before starting
        if (progressBar != null) progressBar.value = 0f;
        if (progressPanel != null) progressPanel.SetActive(false);

        if (craftingEffect)
        {
            ParticleSystem ps = Instantiate(craftingEffect, transform.position, transform.rotation);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ps.transform.SetParent(null);
            ps.Play();
            float maxLifetime = (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants) ? main.startLifetime.constantMax : main.startLifetime.constant;
            float destroyAfter = main.duration + maxLifetime + 0.25f;
            Destroy(ps.gameObject, destroyAfter);
        }
        StartCoroutine(ReturnToTrashRoutine(position));
    }

    IEnumerator ReturnToTrashRoutine(Vector3 position)
    {
        // move pile to destroyed position
        transform.position = position;

        // show progress UI
        if (progressPanel != null)
        {
            progressPanel.SetActive(true);
            if (progressText != null) progressText.text = "Returning...";
        }

        // start from zero
        if (progressBar != null) progressBar.value = 0f;

        float elapsed = 0f;
        while (elapsed < craftingTime)
        {
            elapsed += Time.deltaTime;
            if (progressBar != null) progressBar.value = Mathf.Clamp01(elapsed / craftingTime);
            yield return null;
        }

        if (progressBar != null) progressBar.value = 1f;
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.05f);

        // play the shared craftingEffect
       

        // reactivate trash object
        gameObject.SetActive(true);

        // hide progress UI
        if (progressPanel != null) progressPanel.SetActive(false);

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

    // Keep the CraftTool enum for backward compatibility
    public enum CraftTool
    {
        Glue,   // สร้างกล่อง
        Thread  // สร้างตุ๊กตา
    }
}