using UnityEngine;

public class GlueSpot : MonoBehaviour, ISlowable
{
    public enum LightPhase
    {
        Green,
        Red,
        Yellow
    }

    [Header("Target Platform")]
    [Tooltip("สายพานที่ต้องการให้หยุด")]
    public MovingSlidePlatform targetPlatform;
    [Tooltip("สายพานตัวอื่นๆ ที่ต้องการให้หยุดพร้อมกัน (ถ้ามี)")]
    public MovingSlidePlatform[] additionalPlatforms;

    [Header("Single Light Indicator (ดวงไฟสัญญาณเดี่ยว)")]
    [Tooltip("SpriteRenderer ของดวงไฟสัญญาณ")]
    public SpriteRenderer lightRenderer;

    [Space(5)]
    [Tooltip("Sprite ไฟเขียว (ถ้ามี)")]
    public Sprite greenSprite;
    [Tooltip("Sprite ไฟแดง (ถ้ามี)")]
    public Sprite redSprite;
    [Tooltip("Sprite ไฟเหลือง (ถ้ามี)")]
    public Sprite yellowSprite;

    [Header("Light Colors (สีไฟ 3 เฟส)")]
    public bool useColorFallback = true;
    [Tooltip("สีไฟเขียว (ปกติขณะสายพานเคลื่อนที่)")]
    public Color greenColor = new Color(0.1f, 1f, 0.1f, 1f);
    [Tooltip("สีไฟแดง (หยุดขณะโดนกาว)")]
    public Color redColor = new Color(1f, 0.15f, 0.15f, 1f);
    [Tooltip("สีไฟเหลือง (เตือนก่อนสายพานจะกลับมาขยับ)")]
    public Color yellowColor = new Color(1f, 0.85f, 0.1f, 1f);

    [Header("Warning Duration (ระยะเวลาเตือนไฟเหลือง)")]
    [Tooltip("เวลาก่อนที่สายพานจะกลับมาเดิน ที่จะเปลี่ยนเป็นสีเหลืองเตือน (วินาที)")]
    public float yellowWarningDuration = 2f;

    // --- ส่วนรองรับของเดิม (Backward Compatibility เพื่อไม่ให้ Reference ใน Inspector หลุด) ---
    [HideInInspector] public SpriteRenderer greenLightRenderer;
    [HideInInspector] public SpriteRenderer redLightRenderer;
    [HideInInspector] public Sprite greenLightOnSprite;
    [HideInInspector] public Sprite greenLightOffSprite;
    [HideInInspector] public Sprite redLightOnSprite;
    [HideInInspector] public Sprite redLightOffSprite;
    [HideInInspector] public Color greenOnColor = new Color(0.1f, 1f, 0.1f, 1f);
    [HideInInspector] public Color greenOffColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    [HideInInspector] public Color redOnColor = new Color(1f, 0.15f, 0.15f, 1f);
    [HideInInspector] public Color redOffColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    private bool isStopped = false;
    private LightPhase currentPhase = LightPhase.Green;
    private float internalSlowEndTime = 0f;

    void Awake()
    {
        ResolveRenderer();
    }

    void OnValidate()
    {
        ResolveRenderer();
    }

    private void ResolveRenderer()
    {
        if (lightRenderer == null)
        {
            if (greenLightRenderer != null) lightRenderer = greenLightRenderer;
            else if (redLightRenderer != null) lightRenderer = redLightRenderer;
            else lightRenderer = GetComponent<SpriteRenderer>();
        }

        if (greenSprite == null && greenLightOnSprite != null) greenSprite = greenLightOnSprite;
        if (redSprite == null && redLightOnSprite != null) redSprite = redLightOnSprite;

        // ถ้ามี redLightRenderer แยกออกมาคนละตัวกับ lightRenderer ให้ปิดการแสดงผลเพื่อไม่ให้มีไฟ 2 ดวง
        if (redLightRenderer != null && redLightRenderer != lightRenderer && Application.isPlaying)
        {
            redLightRenderer.enabled = false;
        }
    }

    void OnEnable()
    {
        if (targetPlatform != null)
        {
            targetPlatform.onStateChanged += OnPlatformStateChanged;
        }
    }

    void OnDisable()
    {
        if (targetPlatform != null)
        {
            targetPlatform.onStateChanged -= OnPlatformStateChanged;
        }
    }

    void Start()
    {
        ResolveRenderer();
        SyncStateWithPlatform();
        UpdateVisualIndicators();
    }

    void Update()
    {
        // ซิงค์สถานะไฟกับสายพานทุกเฟรม เพื่อให้ไฟตรงกับเวลาของสายพาน 100%
        SyncStateWithPlatform();
    }

    private void SyncStateWithPlatform()
    {
        bool anySlowed = false;

        if (targetPlatform != null && targetPlatform.IsSlowed)
        {
            anySlowed = true;
        }
        else if (additionalPlatforms != null)
        {
            for (int i = 0; i < additionalPlatforms.Length; i++)
            {
                if (additionalPlatforms[i] != null && additionalPlatforms[i].IsSlowed)
                {
                    anySlowed = true;
                    break;
                }
            }
        }

        isStopped = anySlowed;

        // คำนวณเฟสของไฟ 3 สถานะ:
        // 1. เขียว: สายพานเดินปกติ
        // 2. แดง: โดนกาวหยุด
        // 3. เหลือง: ใกล้จะหมดเวลาหยุด ก่อนจะกลับไปเป็นสีเขียว
        LightPhase newPhase = LightPhase.Green;

        if (isStopped)
        {
            float remaining = GetRemainingSlowTime();
            if (remaining > 0f && remaining <= yellowWarningDuration)
            {
                newPhase = LightPhase.Yellow;
            }
            else
            {
                newPhase = LightPhase.Red;
            }
        }
        else
        {
            newPhase = LightPhase.Green;
        }

        if (newPhase != currentPhase)
        {
            currentPhase = newPhase;
            UpdateVisualIndicators();
        }
    }

    private float GetRemainingSlowTime()
    {
        float maxRemaining = 0f;

        if (targetPlatform != null && targetPlatform.IsSlowed)
        {
            maxRemaining = Mathf.Max(maxRemaining, targetPlatform.RemainingSlowTime);
        }

        if (additionalPlatforms != null)
        {
            for (int i = 0; i < additionalPlatforms.Length; i++)
            {
                if (additionalPlatforms[i] != null && additionalPlatforms[i].IsSlowed)
                {
                    maxRemaining = Mathf.Max(maxRemaining, additionalPlatforms[i].RemainingSlowTime);
                }
            }
        }

        // Fallback กรณีไม่มี reference platform หรือคำนวณผ่าน internal timer
        if (maxRemaining <= 0f && isStopped)
        {
            maxRemaining = Mathf.Max(0f, internalSlowEndTime - Time.time);
        }

        return maxRemaining;
    }

    private void OnPlatformStateChanged(bool isMoving)
    {
        isStopped = !isMoving;
        SyncStateWithPlatform();
    }

    public void ApplySlow(float slowAmount, float duration)
    {
        isStopped = true;

        if (targetPlatform != null)
        {
            targetPlatform.ApplySlow(slowAmount, duration);
        }

        if (additionalPlatforms != null)
        {
            for (int i = 0; i < additionalPlatforms.Length; i++)
            {
                if (additionalPlatforms[i] != null)
                {
                    additionalPlatforms[i].ApplySlow(slowAmount, duration);
                }
            }
        }

        // MovingSlidePlatform บวกเพิ่ม 2 วินาทีใน ApplySlow
        internalSlowEndTime = Mathf.Max(internalSlowEndTime, Time.time) + duration + 2f;
        SyncStateWithPlatform();
    }

    public void ApplyGradualSlow(float targetSlowAmount, float duration, float lerpTime)
    {
        ApplySlow(targetSlowAmount, duration);
    }

    public void ResetSpot()
    {
        isStopped = false;
        internalSlowEndTime = 0f;
        SyncStateWithPlatform();
    }

    public void UpdateVisualIndicators()
    {
        if (lightRenderer == null)
        {
            ResolveRenderer();
        }

        if (lightRenderer == null) return;

        switch (currentPhase)
        {
            case LightPhase.Green:
                if (greenSprite != null) lightRenderer.sprite = greenSprite;
                lightRenderer.color = (greenSprite != null && !useColorFallback) ? Color.white : greenColor;
                break;

            case LightPhase.Yellow:
                if (yellowSprite != null) lightRenderer.sprite = yellowSprite;
                lightRenderer.color = (yellowSprite != null && !useColorFallback) ? Color.white : yellowColor;
                break;

            case LightPhase.Red:
                if (redSprite != null) lightRenderer.sprite = redSprite;
                lightRenderer.color = (redSprite != null && !useColorFallback) ? Color.white : redColor;
                break;
        }

        // ปิดไฟแดงเดิม (ถ้ามีและไม่ใช่ดวงเดียวกับ lightRenderer)
        if (redLightRenderer != null && redLightRenderer != lightRenderer && redLightRenderer.enabled)
        {
            redLightRenderer.enabled = false;
        }
    }
}
