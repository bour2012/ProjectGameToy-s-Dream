using UnityEngine;
using UnityEngine.UI; // จำเป็นสำหรับ Slider
using UnityEngine.Events;
using System.Collections;

public class GiantGlueTrigger : MonoBehaviour, IInteractable
{
    [Header("Glue Settings")]
    [Tooltip("จำนวนกาวที่ต้องยิงใส่ให้เต็ม")]
    public int glueRequired = 5;
    [Tooltip("Tag ของกระสุนกาว")]
    public string glueTag = "Glue";

    [Header("UI Progress")]
    [Tooltip("ลาก Slider (Glue Bar) มาใส่ตรงนี้")]
    public Slider glueSlider; // <-- เปลี่ยนจาก Image เป็น Slider

    [Header("Interact Settings")]
    [Tooltip("ปรับตำแหน่งไอคอน E ให้ต่ำลงหรือสูงขึ้นเฉพาะจุดนี้")]
    public Vector3 uiOffset = Vector3.zero;
    [Tooltip("ข้อความที่จะขึ้นโชว์ตอนกด E")]
    public string interactPrompt = "Start Machine";

    [Header("Fade & Cutscene")]
    public GameObject fadePanel;
    public float fadeDuration = 1.5f;

    [Space(10)]
    public UnityEvent onCutsceneStart;

    // ตัวแปรภายใน
    private int currentGlueCount = 0;
    private bool isReadyToInteract = false;
    private bool isCutsceneStarted = false;
    private CanvasGroup fadeCanvasGroup;
    private Image fadeImage;
    private Collider2D triggerCollider;

    void Start()
    {
        triggerCollider = GetComponent<Collider2D>();

        // ตั้งค่า Slider เริ่มต้น
        if (glueSlider != null)
        {
            glueSlider.maxValue = glueRequired; // กำหนดค่าเต็มเท่ากับจำนวนกาวที่ต้องใช้
            glueSlider.value = 0;               // เริ่มต้นที่ 0
        }

        // Setup FadePanel (ส่วนเดิม)
        if (fadePanel != null)
        {
            fadeCanvasGroup = fadePanel.GetComponent<CanvasGroup>();
            fadeImage = fadePanel.GetComponent<Image>();

            if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = 0f;
            else if (fadeImage != null)
            {
                Color c = fadeImage.color;
                c.a = 0f;
                fadeImage.color = c;
            }
            fadePanel.SetActive(true);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(glueTag) && !isCutsceneStarted)
        {
            Destroy(other.gameObject);

            if (!isReadyToInteract)
            {
                currentGlueCount++;

                // --- อัปเดตค่า Slider ---
                if (glueSlider != null)
                {
                    glueSlider.value = currentGlueCount;
                }
                // ---------------------

                Debug.Log($"Glue Progress: {currentGlueCount}/{glueRequired}");

                if (currentGlueCount >= glueRequired)
                {
                    isReadyToInteract = true;

                    // Trick: รีเฟรช Collider เพื่อให้ปุ่ม E ขึ้นทันทีถ้าผู้เล่นยืนแช่อยู่
                    if (triggerCollider != null)
                    {
                        triggerCollider.enabled = false;
                        triggerCollider.enabled = true;
                    }
                }
            }
        }
    }

    // --- Interface Implementation ---

    public string GetInteractText()
    {
        if (isReadyToInteract && !isCutsceneStarted)
        {
            return interactPrompt;
        }
        return "";
    }

    public Vector3 GetUiOffset()
    {
        return uiOffset;
    }

    public void Interact()
    {
        if (!isReadyToInteract || isCutsceneStarted) return;
        StartCoroutine(StartCutsceneSequence());
    }

    // ---------------------------------------------------------

    IEnumerator StartCutsceneSequence()
    {
        isCutsceneStarted = true;

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Clamp01(timer / fadeDuration);

            if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = alpha;
            else if (fadeImage != null)
            {
                Color c = fadeImage.color;
                c.a = alpha;
                fadeImage.color = c;
            }
            yield return null;
        }

        onCutsceneStart.Invoke();
    }
}