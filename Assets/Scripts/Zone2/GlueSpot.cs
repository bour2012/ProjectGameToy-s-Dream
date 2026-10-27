using UnityEngine;

public class GlueSpot : MonoBehaviour, ISlowable
{
    [Header("Target Platform")]
    [Tooltip("สายพานที่ต้องการให้หยุด")]
    public MovingSlidePlatform targetPlatform;
    [Tooltip("สายพานตัวอื่นๆ ที่ต้องการให้หยุดพร้อมกัน (ถ้ามี)")]
    public MovingSlidePlatform[] additionalPlatforms;

    [Header("Traffic Light Indicators (สัญญาณไฟจราจร)")]
    [Tooltip("SpriteRenderer ของไฟเขียว")]
    public SpriteRenderer greenLightRenderer;
    [Tooltip("SpriteRenderer ของไฟแดง")]
    public SpriteRenderer redLightRenderer;

    [Space(5)]
    [Tooltip("Sprite ไฟเขียวตอนเปิด (ถ้ามี)")]
    public Sprite greenLightOnSprite;
    [Tooltip("Sprite ไฟเขียวตอนปิด (ถ้ามี ถ้าไม่มีจะใช้สีเทา)")]
    public Sprite greenLightOffSprite;

    [Tooltip("Sprite ไฟแดงตอนเปิด (ถ้ามี)")]
    public Sprite redLightOnSprite;
    [Tooltip("Sprite ไฟแดงตอนปิด (ถ้ามี ถ้าไม่มีจะใช้สีเทา)")]
    public Sprite redLightOffSprite;

    [Header("Traffic Light Colors (สีไฟจราจร)")]
    public bool useColorFallback = true;
    [Tooltip("สีไฟเขียวตอนติด (สว่าง)")]
    public Color greenOnColor = new Color(0.1f, 1f, 0.1f, 1f);
    [Tooltip("สีไฟเขียวตอนดับ (เทา)")]
    public Color greenOffColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    [Tooltip("สีไฟแดงตอนติด (สว่าง)")]
    public Color redOnColor = new Color(1f, 0.15f, 0.15f, 1f);
    [Tooltip("สีไฟแดงตอนดับ (เทา)")]
    public Color redOffColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    private bool isStopped = false;

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
        SyncStateWithPlatform();
        UpdateVisualIndicators();
    }

    void Update()
    {
        // ซิงค์สถานะไฟกับสายพานทุกเฟรม เพื่อให้ไฟตรงกับเวลาของสายพาน 100%
        // แม้จะมีการยิงกาวซ้ำเพื่อเพิ่มเวลาก็ตาม
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

        if (isStopped != anySlowed)
        {
            isStopped = anySlowed;
            UpdateVisualIndicators();
        }
    }

    private void OnPlatformStateChanged(bool isMoving)
    {
        isStopped = !isMoving;
        UpdateVisualIndicators();
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

        UpdateVisualIndicators();
    }

    public void ApplyGradualSlow(float targetSlowAmount, float duration, float lerpTime)
    {
        ApplySlow(targetSlowAmount, duration);
    }

    public void ResetSpot()
    {
        isStopped = false;
        UpdateVisualIndicators();
    }

    public void UpdateVisualIndicators()
    {
        // รูปแบบไฟจราจร: ติดสีเดียว อีกดวงดับเป็นสีเทา
        // ขยับ (ปกติ) -> เขียวติด (สว่าง) / แดงดับ (สีเทา)
        // หยุด (โดนกาว) -> เขียวดับ (สีเทา) / แดงติด (สว่าง)
        bool greenOn = !isStopped;
        bool redOn = isStopped;

        if (greenLightRenderer != null)
        {
            if (greenOn)
            {
                if (greenLightOnSprite != null) greenLightRenderer.sprite = greenLightOnSprite;
                greenLightRenderer.color = (greenLightOnSprite != null && !useColorFallback) ? Color.white : greenOnColor;
            }
            else
            {
                if (greenLightOffSprite != null) greenLightRenderer.sprite = greenLightOffSprite;
                greenLightRenderer.color = (greenLightOffSprite != null && !useColorFallback) ? Color.white : greenOffColor;
            }
        }

        if (redLightRenderer != null)
        {
            if (redOn)
            {
                if (redLightOnSprite != null) redLightRenderer.sprite = redLightOnSprite;
                redLightRenderer.color = (redLightOnSprite != null && !useColorFallback) ? Color.white : redOnColor;
            }
            else
            {
                if (redLightOffSprite != null) redLightRenderer.sprite = redLightOffSprite;
                redLightRenderer.color = (redLightOffSprite != null && !useColorFallback) ? Color.white : redOffColor;
            }
        }
    }
}
