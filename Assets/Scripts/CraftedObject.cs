using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CraftedObject : MonoBehaviour
{
    [Header("Object Status")]
    public bool isPossessed = false;
    public int currentHitPoints = 3;
    public int maxHitPoints = 3;

    [Header("Debug")]
    public bool showDebugInfo = true;

    // Data References
    private TrashCraftingSystem.CraftableItem itemData;
    private TrashCraftingSystem parentCraftingSystem;

    // Coroutines
    private Coroutine lifetimeCoroutine;
    private Coroutine blinkWarningCoroutine;

    // --- UI Handling (ปรับปรุงใหม่) ---
    public GameObject interactPromptRef;
    private TextMeshProUGUI interactPromptText;
    private KeyCode returnKey = KeyCode.Q;
    private float interactionDistance = 3f;

    private Transform player;
    private bool isPlayerNear = false; // ตัวแปรเช็คสถานะเหมือน TrashCraftingSystem
    private Vector3 originalPromptScale = Vector3.one;
    private Coroutine uiAnimCoroutine;

    // Components
    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    // Visuals
    private Color originalColor;
    private bool isBlinking = false;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    // Possession Logic vars
    private StationaryEnemy possessionController;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    public void Initialize(TrashCraftingSystem.CraftableItem data, TrashCraftingSystem craftingSystem,
        GameObject uiContainerRef = null, GameObject progressPanel = null, GameObject interactPrompt = null,
        KeyCode returnKeyOverride = KeyCode.Q, float interactionDistanceOverride = 3f)
    {
        itemData = data;
        parentCraftingSystem = craftingSystem;
        currentHitPoints = data.maxHitPoints;

        returnKey = returnKeyOverride;
        interactionDistance = interactionDistanceOverride;

        if (interactPromptRef != null)
        {
            interactPromptText = interactPromptRef.GetComponent<TextMeshProUGUI>();
            // เริ่มต้นซ่อนไว้ก่อน
            TogglePromptUI(false);
        }

        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // เริ่มนับถอยหลัง
        if (itemData != null)
        {
            StartLifetimeCountdown();
        }

        if (showDebugInfo) Debug.Log($"CraftedObject initialized: {itemData.itemName}");
    }

    void Update()
    {
        // 1. ตรวจสอบระยะผู้เล่น
        CheckPlayerDistance();

        // 2. รับ Input (ถ้าผู้เล่นอยู่ใกล้)
        if (isPlayerNear)
        {
            HandleInput();
            UpdateUIPosition(); // อัปเดตตำแหน่ง UI ให้ตามวัตถุ
        }
    }

    // --- ส่วนที่ปรับปรุง: Logic การเช็คระยะและการแสดงผล UI ---

    void CheckPlayerDistance()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            return;
        }

        // ตรวจสอบระยะห่าง
        float distance = Vector2.Distance(transform.position, player.position);

        // เงื่อนไขการแสดง UI: ระยะถึง AND ไม่ได้ถูกสิง (ถ้าถูกสิงมักจะซ่อน UI)
        bool shouldShowUI = distance <= interactionDistance && !isPossessed;

        // สั่งเปิด/ปิดเฉพาะเมื่อสถานะเปลี่ยน (เพื่อประสิทธิภาพ)
        if (shouldShowUI != isPlayerNear)
        {
            isPlayerNear = shouldShowUI;
            TogglePromptUI(shouldShowUI);
        }
    }

    void TogglePromptUI(bool show)
    {
        if (interactPromptRef == null) return;

        // Use appear/disappear animation coroutines instead of instant SetActive to avoid
        // popping the UI. Record original scale on first use.
        if (originalPromptScale == Vector3.one && interactPromptRef.transform != null)
        {
            originalPromptScale = interactPromptRef.transform.localScale;
        }

        if (show)
        {
            // start appear animation
            if (uiAnimCoroutine != null) StopCoroutine(uiAnimCoroutine);
            uiAnimCoroutine = StartCoroutine(UIAppearAnimation());
            UpdatePromptText();
        }
        else
        {
            if (uiAnimCoroutine != null) StopCoroutine(uiAnimCoroutine);
            uiAnimCoroutine = StartCoroutine(UIDisappearAnimation());
        }
    }

    IEnumerator UIAppearAnimation()
    {
        if (interactPromptRef == null) yield break;

        interactPromptRef.SetActive(true);
        Transform t = interactPromptRef.transform;
        t.localScale = Vector3.zero;
        float time = 0f;
        while (time < 0.3f)
        {
            time += Time.deltaTime;
            float progress = time / 0.3f;
            t.localScale = Vector3.Lerp(Vector3.zero, originalPromptScale, Mathf.Sin(progress * Mathf.PI * 0.5f));
            yield return null;
        }
        t.localScale = originalPromptScale;
        uiAnimCoroutine = null;
    }

    IEnumerator UIDisappearAnimation()
    {
        if (interactPromptRef == null) yield break;

        Transform t = interactPromptRef.transform;
        float time = 0f;
        Vector3 startScale = t.localScale;
        while (time < 0.2f)
        {
            time += Time.deltaTime;
            float progress = time / 0.2f;
            t.localScale = Vector3.Lerp(startScale, Vector3.zero, progress);
            yield return null;
        }
        t.localScale = Vector3.zero;
        interactPromptRef.SetActive(false);
        uiAnimCoroutine = null;
    }

    void UpdatePromptText()
    {
        if (interactPromptText != null)
        {
            interactPromptText.text = $"Press {returnKey} to Break";
            interactPromptText.color = Color.white; // หรือสีตามต้องการ
        }
    }

    void UpdateUIPosition()
    {
        // ถ้า UI นี้เป็น World Space หรืออยากให้ลอยเหนือหัววัตถุ
        // หมายเหตุ: เนื่องจาก interactPromptRef นี้อาจถูกแชร์มาจาก TrashSystem 
        // การย้ายตำแหน่งอาจต้องระวังถ้า TrashSystem ใช้พร้อมกัน (แต่ปกติ TrashSystem จะถูกปิดไปแล้ว)
        if (interactPromptRef != null && interactPromptRef.activeInHierarchy)
        {
            // ปรับตำแหน่งให้ลอยเหนือวัตถุเล็กน้อย (Vector3.up * 2f คือความสูง)
            interactPromptRef.transform.position = transform.position + Vector3.up * 1.5f;
        }
    }

    void HandleInput()
    {
        // รับ Input ทำลายของ
        if (Input.GetKeyDown(returnKey))
        {
            ForceReturnToTrash();
        }
    }

    // --- จบส่วนปรับปรุง UI ---

    #region Lifetime & Visuals (คงเดิม)

    void StartLifetimeCountdown()
    {
        if (lifetimeCoroutine != null) StopCoroutine(lifetimeCoroutine);
        lifetimeCoroutine = StartCoroutine(LifetimeCountdown());
    }

    IEnumerator LifetimeCountdown()
    {
        float remainingTime = itemData.lifetime;
        float warningTime = Mathf.Min(1f, itemData.lifetime * 0.3f);

        yield return new WaitForSeconds(remainingTime - warningTime);
        StartBlinkWarning();
        yield return new WaitForSeconds(warningTime);
        ReturnToTrash();
    }

    void StartBlinkWarning()
    {
        if (blinkWarningCoroutine != null) StopCoroutine(blinkWarningCoroutine);
        blinkWarningCoroutine = StartCoroutine(BlinkWarning());
    }

    IEnumerator BlinkWarning()
    {
        isBlinking = true;
        while (isBlinking)
        {
            if (spriteRenderer != null) spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            if (spriteRenderer != null) spriteRenderer.color = originalColor;
            yield return new WaitForSeconds(0.1f);
        }
    }

    void StopBlinkWarning()
    {
        isBlinking = false;
        if (blinkWarningCoroutine != null)
        {
            StopCoroutine(blinkWarningCoroutine);
            blinkWarningCoroutine = null;
        }
        if (spriteRenderer != null) spriteRenderer.color = originalColor;
    }

    #endregion

    #region Possession System (คงเดิม - ตัดย่อเพื่อความกระชับ)

    public bool TryPossess(GameObject possessor)
    {
        if (!itemData.canBePossessed || isPossessed) return false;
        isPossessed = true;

        // เมื่อถูกสิง ให้ปิด UI ทันที
        if (isPlayerNear)
        {
            isPlayerNear = false;
            TogglePromptUI(false);
        }

        if (lifetimeCoroutine != null) StopCoroutine(lifetimeCoroutine);
        StopBlinkWarning();

        if (itemData.spawnEffect) Instantiate(itemData.spawnEffect, transform.position, transform.rotation);
        if (animator != null) animator.SetBool("IsPossessed", true);
        if (spriteRenderer != null) spriteRenderer.color = Color.red * 0.8f;

        return true;
    }

    public void TakeDamage(int damage = 1)
    {
        if (!isPossessed) return;
        currentHitPoints -= damage;
        if (currentHitPoints <= 0) ReleasePossession();
    }

    void ReleasePossession()
    {
        if (!isPossessed) return;
        isPossessed = false;
        if (possessionController != null) possessionController.ReleasePossession(transform.position);
        ReturnToTrash();
    }

    #endregion

    #region Destruction and Return (คงเดิม)

    public void ReturnToTrash()
    {
        // ซ่อน UI ก่อนทำลาย
        TogglePromptUI(false);

        if (itemData.destroyEffect) Instantiate(itemData.destroyEffect, transform.position, transform.rotation);
        if (itemData.destroySound && parentCraftingSystem != null)
            AudioSource.PlayClipAtPoint(itemData.destroySound, transform.position);

        if (parentCraftingSystem != null)
        {
            parentCraftingSystem.OnCraftedObjectDestroyed(initialPosition);
        }

        Destroy(gameObject);
    }

    public void ForceReturnToTrash()
    {
        StopAllCoroutines();
        ReturnToTrash();
    }

    #endregion

    #region Collision & Public Methods (คงเดิม)

    void OnTriggerEnter2D(Collider2D other)
    {
        if (itemData != null && itemData.canBePossessed && !isPossessed && other.CompareTag("Enemy"))
        {
            var enemyPossession = other.GetComponent<StationaryEnemy>();
            if (enemyPossession != null) enemyPossession.Possess(this);
        }
    }

    public bool IsPossessed() => isPossessed;
    public bool CanBePossessed() => itemData.canBePossessed && !isPossessed;
    public TrashCraftingSystem.CraftableItem GetItemData() => itemData;

    #endregion

    void OnDestroy()
    {
        StopAllCoroutines();
        // ป้องกัน UI ค้างถ้า object ถูกทำลาย
        if (interactPromptRef != null && isPlayerNear)
        {
            interactPromptRef.SetActive(false);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isPlayerNear ? Color.blue : Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}