using UnityEngine;
using UnityEngine.Events;
using System.Collections;

// --- ส่วนของ Data ที่เพิ่มความสามารถใหม่ ---
[System.Serializable]
public class DialogData
{
    [TextArea(2, 5)]
    public string dialogText;
    public AudioClip dialogVoice;

    [Header("Camera Control")]
    public Transform focusTarget;
    [Tooltip("ถ้าระบุ Target: จะให้รอกล้องวิ่งไปถึงก่อนค่อยขึ้นข้อความหรือไม่?")]
    public bool waitForCamera = false;
    [Tooltip("ความนุ่มนวลของกล้อง (ยิ่งเลขเยอะ ยิ่งนุ่ม/ช้า) | 0 = เร็ว/ปกติ, 2-5 = นุ่มมาก")]
    [Range(0f, 50f)]
    public float cameraSmoothness = 1f;
    [Header("Lens Zoom Control")]
    [Tooltip("ติ๊กถูกถ้าต้องการให้มีการ Zoom ในประโยคนี้")]
    public bool changeZoom = false;

    [Tooltip("ค่า Lens ที่ต้องการ (ถ้า 3D คือ FOV, ถ้า 2D คือ Ortho Size)")]
    public float targetLensSize = 40f; // ค่า default ตามรูปของคุณคือ 40

    [Tooltip("ความเร็วในการ Zoom (วินาที)")]
    public float zoomDuration = 1f;

    public bool shakeCamera = false;

    [Header("Transition Effect")]
    [Tooltip("ใช้การ Fade ดำเพื่อตัดฉากหรือไม่ (เหมาะสำหรับย้ายกล้องไกลๆ ไม่ให้เวียนหัว)")]
    public bool useFadeCut = false;
    
    [Tooltip("ความเร็วในการ Fade (วินาที)")]
    public float fadeDuration = 0.5f;

    // --- ส่วนที่เพิ่มใหม่ ---
    [Tooltip("ระยะเวลาที่จะแช่จอดำค้างไว้ (วินาที) ก่อนจะเปิดภาพกลับมา")]
    public float fadeHoldDuration = 0.5f; 
    // ----------------------

    [Header("Hint Settings")]
    public bool isHint = false;
    public GameObject hintUIElement;
    public float displayDuration = 5f;
    
    [Header("Actor Actions")]
    public UnityEvent onLineStart;
}

// --- ส่วน Trigger หลัก (เหมือนเดิมแต่รองรับ Data ใหม่) ---
public class DialogTrigger : MonoBehaviour
{
    [Header("Dialog Identifier")]
    public string dialogID;

    [Header("Dialog Sequence")]
    public DialogData[] dialogSequence;

    [Header("Settings")]
    public bool triggerOnce = true;
    public bool freezePlayerOnStart = true;
    public bool rememberAcrossScenes = false;
    public bool useDelay = false;
    [Range(0f, 10f)] public float delay = 1f;
    public bool autoAdvance = false;
    [Range(0.1f, 10f)] public float autoAdvanceDelay = 2f;

    [Header("Events")]
    public UnityEvent onDialogComplete;

    private bool hasTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            StartDialogSequence();
        }
    }

    public void TriggerDialog() => StartDialogSequence();

    private void StartDialogSequence() => StartCoroutine(DialogCoroutine());

    private IEnumerator DialogCoroutine()
    {
        if (rememberAcrossScenes && GameManager.Instance != null && GameManager.Instance.HasAlreadyPlayed(dialogID)) yield break;
        if (triggerOnce && hasTriggered) yield break;

        if (useDelay) yield return new WaitForSeconds(delay);

        if (dialogSequence == null || dialogSequence.Length == 0) yield break;
        if (DialogManager.Instance == null) yield break;

        if (triggerOnce) hasTriggered = true;

        bool shouldAutoAdvance = autoAdvance || !freezePlayerOnStart;

        // เรียก Manager ตัวใหม่
        DialogManager.Instance.StartDialogSequence(dialogSequence, this, freezePlayerOnStart, shouldAutoAdvance, autoAdvanceDelay);

        if (rememberAcrossScenes && GameManager.Instance != null) GameManager.Instance.MarkAsPlayed(dialogID);
    }

    public void InvokeCompletionEvent() => onDialogComplete.Invoke();
    public void ResetTrigger() => hasTriggered = false;
}