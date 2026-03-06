using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

public class GiantGlueTrigger : MonoBehaviour, IInteractable
{
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
    private bool isReadyToInteract = true; // ปรับเป็น true ตั้งแต่เริ่มต้นเลย
    private bool isCutsceneStarted = false;
    private CanvasGroup fadeCanvasGroup;
    private Image fadeImage;

    void Start()
    {
        // Setup FadePanel
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

    // ลบ OnTriggerEnter2D ออกไปได้เลย เพราะระบบ IInteractable ของคุณ 
    // น่าจะมีการตรวจจับจากฝั่ง Player อยู่แล้ว

    // --- Interface Implementation ---

    public string GetInteractText()
    {
        // ถ้าคัตซีนยังไม่เริ่ม ก็โชว์ปุ่ม E ได้ตลอด
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