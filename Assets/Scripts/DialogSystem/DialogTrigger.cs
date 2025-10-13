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

        DialogManager.Instance.StartDialogSequence(dialogSequence, this, autoAdvance, autoAdvanceDelay);

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
}