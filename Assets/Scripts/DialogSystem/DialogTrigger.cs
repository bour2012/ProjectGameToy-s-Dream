using UnityEngine;
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

}

public class DialogTrigger : MonoBehaviour
{
    [Header("Dialog Identifier")]
    [Tooltip("ตั้งชื่อ ID เฉพาะสำหรับ Dialog นี้ (ห้ามซ้ำกัน!)")]
    public string dialogID;

    [Header("Dialog Sequence")]
    public DialogData[] dialogSequence;
    [Tooltip("ติ๊กเพื่อเปิดใช้งานการหน่วงเวลาก่อนแสดง Dialog")]
    public bool useDelay = false;
    [Tooltip("เวลาที่จะหน่วง (วินาที)")]
    [Range(0f, 10f)]
    public float delay = 1f;

    [Header("Settings")]
    public bool triggerOnce = true;
    public bool autoAdvance = false;
    [Range(0.1f, 10f)]
    public float autoAdvanceDelay = 2f;

    private bool hasTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // แค่เรียกฟังก์ชันกลางพอ ที่เหลือให้ฟังก์ชันนั้นจัดการ
            StartDialogSequence();
        }
    }

    private void StartDialogSequence()
    {
        // เปลี่ยนจากการทำงานทันที เป็นการไปเริ่ม Coroutine แทน
        StartCoroutine(DialogCoroutine());
    }

    private IEnumerator DialogCoroutine()
    {
        // 1. เช็คก่อนว่า "เล่นครั้งเดียว" และ "เคยเล่นไปแล้ว" หรือไม่
        if (triggerOnce && hasTriggered)
        {
            yield break; // ถ้าใช่ ก็ออกจาก Coroutine ไปเลย
        }

        // 2. จัดการเรื่องการหน่วงเวลา
        if (useDelay)
        {
            yield return new WaitForSeconds(delay);
        }

        // 3. (Re-check อีกครั้งหลัง Delay) เช็คอีกรอบ เผื่อสถานะเปลี่ยนระหว่างรอ
        if (triggerOnce && hasTriggered)
        {
            yield break;
        }

        // 4. ถ้าผ่านหมด ก็บันทึกว่า "เล่นแล้วนะ"
        if (triggerOnce)
        {
            hasTriggered = true;
        }

        // 5. โค้ดเดิมสำหรับเริ่มเล่น Dialog
        if (dialogSequence == null || dialogSequence.Length == 0)
        {
            Debug.LogWarning("Dialog sequence is empty!");
            yield break;
        }

        if (DialogManager.Instance != null)
        {
            DialogManager.Instance.StartDialogSequence(dialogSequence, autoAdvance, autoAdvanceDelay);
        }
        else
        {
            Debug.LogError("DialogManager instance not found!");
        }
    }

    // ฟังก์ชันนี้ไม่ต้องแก้ไขอะไรเลย!
    public void TriggerDialog()
    {
        StartDialogSequence();
    }

    // Optional: Reset trigger
    public void ResetTrigger()
    {
        hasTriggered = false;
    }
}