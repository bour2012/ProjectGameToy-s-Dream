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
        public float lifetime = 2f; // ���ҷ�������� (�Թҷ�)

        [Header("Properties")]
        public bool canBePushed = true;
        public bool canDistractEnemies = false;
        public bool canBePossessed = false; // ����Ѻ��꡵�

        [Header("Combat (for possessed dolls)")]
        public int maxHitPoints = 3; // �ӹǹ���駷���ͧ�������ͷ����

        [Header("Visual Effects")]
        public ParticleSystem spawnEffect;
        public ParticleSystem destroyEffect;
        public AudioClip spawnSound;
        public AudioClip destroySound;
    }

    public enum CraftTool
    {
        Glue,   // ���ҧ���ͧ
        Thread  // ���ҧ��꡵�
    }

    void Start()
    {
        // �Ҽ�������� GameManager
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        gameManager = GameManager.Instance;

        // ����� UI
        SetupUI();
        UpdateToolSelection();

        // ��Ѥ��Ѻ�������͹�ҡ GameManager
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
        UpdateProgressUI(); // ������÷Ѵ���

        // �Ѻ Input ੾������������ʶҹл���
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

            // �ʴ� progress bar �����ҧ��ä�ҿ
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

        // ��Ѻ����ͧ��ʹ��� Mouse Scroll
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            SwitchTool(scroll > 0);
        }

        // �������û�д�ɰ���¡�á� E
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

        // ��Ǩ�ͺ����������
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

        // ���������û�д�ɰ��ҹ GameManager
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
        // �ʴ� Progress UI
        if (progressPanel != null)
        {
            progressPanel.SetActive(true);
            progressText.text = "Crafting...";
            Debug.Log("Crafting....");
        }

        // �Ϳ࿡���û�д�ɰ�
        if (craftingEffect) craftingEffect.Play();
        if (craftingSound) craftingSound.Play();

        // �����ҡ�û�д�ɰ�
        float elapsedTime = 0f;
        while (elapsedTime < craftingTime)
        {
            elapsedTime += Time.deltaTime;

            // �ѻവ progress bar
            if (progressBar != null)
            {
                progressBar.value = elapsedTime / craftingTime;
            }

            yield return null;
        }

        // ��д�ɰ��������
        CompleteCrafting();
    }

    void CompleteCrafting()
    {
        UseItemForCrafting();
        CreateCraftedObject();

        // ��͹��ǡͧ���
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

        // �Դ�Ϳ࿡��
        if (craftingEffect) craftingEffect.Stop();
        if (craftingSound) craftingSound.Stop();

        // ����ʶҹ�
        isCrafting = false;

        // �� GameManager
        if (gameManager != null && IsInCraftingState())
        {
            gameManager.ChangeState(GameState.Normal, "Crafting cancelled");
        }

        // �ʴ� UI ��Ѻ�Ҷ�Ҽ������ѧ�������
        if (isPlayerNear)
        {
            ToggleCraftingUI(true);
        }

        // Progress Panel �ж١�Ѵ����� UpdateProgressUI() ����

        Debug.Log("Crafting cancelled");
    }
    void CreateCraftedObject()
    {
        CraftableItem itemToCreate = currentTool == CraftTool.Glue ? boxItem : dollItem;

        if (itemToCreate.prefab != null)
        {
            // ���ҧ�ѵ��㹵��˹觢ͧ�ͧ���
            GameObject craftedObj = Instantiate(itemToCreate.prefab, transform.position, transform.rotation);

            // ���� CraftedObject component
            CraftedObject craftedComponent = craftedObj.GetComponent<CraftedObject>();
            if (craftedComponent == null)
            {
                craftedComponent = craftedObj.AddComponent<CraftedObject>();
            }

            // ��駤�� CraftedObject
            craftedComponent.Initialize(itemToCreate, this);

            // �� reference
            currentCraftedObject = craftedObj;

            // �Ϳ࿡�������ҧ
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

        // �Ϳ࿡�� Highlight
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
                // ��Ǩ�ͺ����
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
    /// ���¡�ҡ CraftedObject ������ѵ�ض١�������е�ͧ��Ѻ���繡ͧ���
    /// </summary>
    public void OnCraftedObjectDestroyed(Vector3 position)
    {
        transform.position = position;
        currentCraftedObject = null;

        // �Դ�ͧ��С�Ѻ������ҿ���ա
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