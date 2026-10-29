using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Events;
using System.Collections;

// --- ส่วนของ Data ที่เพิ่มความสามารถใหม่ ---
[System.Serializable]
public class DialogData
{
    [TextArea(2, 5)]
    public string dialogText;
    [Header("Audio Settings")]
    [FormerlySerializedAs("dialogVoice")]
    public VoiceProfile currentVoice; // ลากไฟล์ Voice Profile ที่สร้างในข้อ 1 มาใส่ตรงนี้

    // --- ส่วนที่เพิ่มใหม่สำหรับ Typewriter Sound ---
    [Header("Typewriter Sound Settings")]
    [Tooltip("เสียง Blip ที่จะดังตอนตัวอักษรเด้งขึ้นมา")]
    public AudioClip typingSound;

    [Tooltip("เล่นเสียงทุกๆ กี่ตัวอักษร (1=ทุกตัว, 2=ทุกๆ 2 ตัว เพื่อไม่ให้หนวกหูเกินไป)")]
    [Range(1, 5)]
    public int playSoundFrequency = 2;

    [Tooltip("สุ่มความสูงต่ำของเสียงเพื่อให้ดูมีชีวิตชีวา (True = สไตล์ Undertale)")]
    public bool randomizePitch = true;

    [Tooltip("ช่วงความแปรปรวนของ Pitch (เช่น 0.8 ถึง 1.2)")]
    public Vector2 pitchRange = new Vector2(0.9f, 1.1f);
    // ------------------------------------------

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

    void Start()
    {
        GameManager gm = FindFirstObjectByType<GameManager>();
    }
    public enum ConflictStrategy
    {
        Ignore,     // ไม่ทำอะไรเลย (ถ้ามีคนพูดอยู่ อันนี้จะไม่ทำงาน)      // รอจนกว่าอันเก่าจบ แล้วค่อยพูด (แนะนำอันนี้)
        Interrupt   // ตัดบทอันเก่าทิ้ง แล้วพูดอันนี้ทันที
    }

    [Header("Conflict Handling")]
    [Tooltip("ถ้ามี Dialog อื่นเล่นอยู่ จะให้ทำอย่างไร?")]
    public ConflictStrategy conflictStrategy = ConflictStrategy.Ignore;

    [Header("Zone / Exit Settings (Optional)")]
    [Tooltip("เริ่มพูดทันทีที่ผู้เล่นเดินชน TriggerEnter หรือไม่ (ค่าเริ่มต้น: True เหมือนเดิม ถ้าปิดไว้ จะใช้ Collider นี้เป็นแค่โซนตรวจจับให้อีเว้นอื่นเรียกใช้)")]
    public bool triggerOnEnter = true;

    [Tooltip("ต้องให้ผู้เล่นอยู่ใน Collider เท่านั้นถึงจะเริ่มเล่น Dialog ได้ (ถ้าอยู่นอกโซน แม้ Event จะสั่ง TriggerDialog ก็จะไม่ทำงาน)")]
    public bool requirePlayerInside = false;

    [Tooltip("ถ้าเดินออกจาก Collider ขณะที่บทพูดกำลังแสดงอยู่ ให้สั่งปิดบทพูดทันทีหรือไม่")]
    public bool closeDialogOnExit = false;

    [Header("Events")]
    public UnityEvent onDialogComplete;

    private bool hasTriggered = false;
    private bool isPlayerInside = false;
    private int playerColliderCount = 0;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayer(other))
        {
            playerColliderCount++;
            isPlayerInside = true;

            if (triggerOnEnter)
            {
                StartDialogSequence();
            }
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (IsPlayer(other))
        {
            isPlayerInside = true;
            if (playerColliderCount == 0)
            {
                playerColliderCount = 1;
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other))
        {
            playerColliderCount = Mathf.Max(0, playerColliderCount - 1);
            if (playerColliderCount == 0)
            {
                isPlayerInside = false;

                if (closeDialogOnExit && DialogManager.Instance != null && DialogManager.Instance.IsDialogActive)
                {
                    DialogManager.Instance.ForceStopDialog();
                }
            }
        }
    }

    private void OnDisable()
    {
        playerColliderCount = 0;
        isPlayerInside = false;
    }

    private bool IsPlayer(Collider2D other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;
        if (other.transform.root != null && other.transform.root.CompareTag("Player")) return true;
        if (other.GetComponentInParent<PlayerMovement>() != null) return true;
        return false;
    }

    public void TriggerDialog() => StartDialogSequence();

    private void StartDialogSequence() => StartCoroutine(DialogCoroutine());

    private IEnumerator DialogCoroutine()
    {
        if (rememberAcrossScenes && GameManager.Instance != null && GameManager.Instance.HasAlreadyPlayed(dialogID)) yield break;
        if (triggerOnce && hasTriggered) yield break;
        if (requirePlayerInside && !isPlayerInside) yield break;

        if (useDelay)
        {
            float elapsed = 0f;
            while (elapsed < delay)
            {
                if (requirePlayerInside && !isPlayerInside) yield break;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (dialogSequence == null || dialogSequence.Length == 0) yield break;
        if (DialogManager.Instance == null) yield break;

        if (DialogManager.Instance.IsDialogActive)
        {
            switch (conflictStrategy)
            {
                case ConflictStrategy.Ignore:
                    yield break; // จบการทำงานไปเลย

                case ConflictStrategy.Interrupt:
                    // สั่งหยุดอันเก่า
                    DialogManager.Instance.ForceStopDialog();
                    // รอ 1 เฟรมเพื่อให้ระบบเคลียร์ค่า
                    yield return null;
                    break;
            }
        }

        if (requirePlayerInside && !isPlayerInside) yield break;

        if (triggerOnce) hasTriggered = true;

        bool shouldAutoAdvance = autoAdvance || !freezePlayerOnStart;

        // เรียก Manager ตัวใหม่
        DialogManager.Instance.StartDialogSequence(dialogSequence, this, freezePlayerOnStart, shouldAutoAdvance, autoAdvanceDelay);

        if (rememberAcrossScenes && GameManager.Instance != null) GameManager.Instance.MarkAsPlayed(dialogID);
    }

    public void InvokeCompletionEvent() => onDialogComplete.Invoke();
    public void ResetTrigger() => hasTriggered = false;
}