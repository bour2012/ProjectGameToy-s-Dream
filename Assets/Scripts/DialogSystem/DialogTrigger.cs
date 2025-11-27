using UnityEngine;
using UnityEngine.Events;
using System.Collections;

[System.Serializable]
public class DialogData
{
    [TextArea(2, 5)]
    public string dialogText;
    public AudioClip dialogVoice;
    [Header("Camera Focus")]
    public Transform focusTarget;
    public bool shakeCamera = false;
    [Header("Hint Settings")]
    [Tooltip("ติ๊กช่องนี้เพื่อทำให้ Dialog นี้แสดงผลเป็น Hint UI แทนกล่องบทพูดปกติ")]
    public bool isHint = false;

    [Tooltip("ลาก GameObject ของ UI ที่จะใช้แสดง Hint (เช่น TextMeshPro) มาใส่ที่นี่")]
    public GameObject hintUIElement;

    [Tooltip("ให้ Hint แสดงค้างไว้กี่วินาที (ถ้าใส่ 0 จะแสดงค้างไว้จนกว่าจะถูกสั่งปิด)")]
    public float displayDuration = 5f;
    
    [Header("Actor Actions")]
    [Tooltip("Event นี้จะทำงานทันทีที่บทพูดนี้เริ่มแสดง (เช่น สั่ง Actor เล่นท่าหัวเราะ, สั่งเปิดเสียงระเบิด)")]
    public UnityEvent onLineStart;
}

public class DialogTrigger : MonoBehaviour
{
    [Header("Dialog Identifier")]
    [Tooltip("ตั้งชื่อ ID เฉพาะสำหรับ Dialog นี้ (ห้ามซ้ำกัน!)")]
    public string dialogID;

    [Header("Dialog Sequence")]
    public DialogData[] dialogSequence;

    [Header("Settings")]
    public bool triggerOnce = true;
    [Tooltip("ติ๊กช่องนี้เพื่อหยุดการเคลื่อนไหวของผู้เล่นขณะที่ Dialog แสดง")]
    public bool freezePlayerOnStart = true;
    [Tooltip("ติ๊กช่องนี้เพื่อให้จำว่าเคยเล่นแล้ว แม้จะรีสตาร์ทเกมหรือโหลดซีนใหม่")]
    public bool rememberAcrossScenes = false;
    [Tooltip("ติ๊กเพื่อเปิดใช้งานการหน่วงเวลาก่อนแสดง Dialog")]
    public bool useDelay = false;
    [Tooltip("เวลาที่จะหน่วง (วินาที)")]
    [Range(0f, 10f)]
    public float delay = 1f;
    public bool autoAdvance = false;
    [Range(0.1f, 10f)]
    public float autoAdvanceDelay = 2f;

    [Header("Events")]
    [Tooltip("Event ที่จะทำงานหลังจาก Dialog Sequence นี้เล่นจบ")]
    public UnityEvent onDialogComplete;

    private bool hasTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            StartDialogSequence();
        }
    }

    public void TriggerDialog()
    {
        StartDialogSequence();
    }

    private void StartDialogSequence()
    {
        StartCoroutine(DialogCoroutine());
    }

    private IEnumerator DialogCoroutine()
    {
        // STEP 1: ตรวจสอบความจำ
        if (rememberAcrossScenes && GameManager.Instance != null && GameManager.Instance.HasAlreadyPlayed(dialogID))
        {
            yield break;
        }
        if (triggerOnce && hasTriggered)
        {
            yield break;
        }

        // STEP 2: หน่วงเวลา
        if (useDelay)
        {
            yield return new WaitForSeconds(delay);
        }

        // STEP 3: ตรวจสอบความพร้อม
        if (dialogSequence == null || dialogSequence.Length == 0)
        {
            Debug.LogWarning($"Dialog sequence is empty on '{gameObject.name}'.", this);
            yield break;
        }
        if (DialogManager.Instance == null)
        {
            Debug.LogError("DialogManager instance not found!");
            yield break;
        }

        // STEP 4: ทำงานและบันทึกสถานะ
        if (triggerOnce)
        {
            hasTriggered = true;
        }

        bool shouldAutoAdvance = autoAdvance || !freezePlayerOnStart;

        DialogManager.Instance.StartDialogSequence(dialogSequence, this, freezePlayerOnStart, shouldAutoAdvance, autoAdvanceDelay);


        if (rememberAcrossScenes && GameManager.Instance != null)
        {
            GameManager.Instance.MarkAsPlayed(dialogID);
        }
    }

    public void InvokeCompletionEvent()
    {
        Debug.Log($"Dialog '{gameObject.name}' completed. Invoking OnDialogComplete event.");
        onDialogComplete.Invoke();
    }

    public void ResetTrigger()
    {
        hasTriggered = false;
    }

    // --- ส่วน Helper สำหรับจัดการ Actor ---

    /// <summary>
    /// ใช้สำหรับสลับ Actor (ตัวแสดง) ให้หายไป แล้วเปิดตัว Real Enemy (ตัวจริง)
    /// ลากฟังก์ชันนี้ไปใส่ใน OnDialogComplete หรือ onLineStart ของประโยคสุดท้าย
    /// </summary>
    public void SwapActorToReal(GameObject actorObj)
    {
        if (actorObj != null)
        {
            actorObj.SetActive(false);
        }

        //if (realEnemyObj != null)
        //{
        //    realEnemyObj.SetActive(true);
        //}
    }

    /// <summary>
    /// ใช้สั่ง Actor เล่น Animation (เช่น "FlyAway", "Laugh")
    /// ลากฟังก์ชันนี้ไปใส่ใน onLineStart ของประโยคที่ต้องการ
    /// </summary>
    public void PlayActorAnimation(Animator actorAnimator, string triggerName)
    {
        if (actorAnimator != null)
        {
            actorAnimator.SetTrigger(triggerName);
        }
    }
    
    /// <summary>
    /// ใช้เล่นเสียง Effect ประกอบฉาก (เช่น เสียงระเบิด, เสียงหัวเราะ)
    /// </summary>
    public void PlaySoundEffect(AudioSource source)
    {
        if (source != null) source.Play();
    }
}