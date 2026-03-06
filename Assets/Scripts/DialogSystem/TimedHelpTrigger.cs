using UnityEngine;
using System.Collections;

public class TimedHelpTrigger : MonoBehaviour
{
    [Header("Timer Settings")]
    [Tooltip("ผู้เล่นต้องติดอยู่ในโซนนี้กี่วินาที ถึงจะแสดงบทพูดช่วยเหลือ")]
    [Range(5f, 120f)]
    public float timeUntilHelp = 15f;

    [Header("Dialog To Trigger")]
    [Tooltip("ลาก DialogTrigger ที่เป็นบทพูดช่วยเหลือมาใส่ที่นี่")]
    public DialogTrigger helpDialog;

    // ตัวแปรภายในสำหรับจัดการ Coroutine
    private Coroutine timerCoroutine;
    private bool hasTriggeredHelp = false;

    /// <summary>
    /// ฟังก์ชันสาธารณะสำหรับเริ่มนับเวลาถอยหลัง
    /// </summary>
    public void StartHelpTimer()
    {
        // ถ้าเคยแสดงบทพูดไปแล้ว หรือกำลังนับเวลาอยู่แล้ว ก็ไม่ต้องทำอะไร
        if (hasTriggeredHelp || timerCoroutine != null)
        {
            return;
        }

        Debug.Log($"[TimedHelp] Starting help timer for {timeUntilHelp} seconds.");
        timerCoroutine = StartCoroutine(HelpTimerCoroutine());
    }

    /// <summary>
    /// ฟังก์ชันสาธารณะสำหรับหยุดนับเวลา (เมื่อผู้เล่นทำสำเร็จ)
    /// </summary>
    public void StopHelpTimer()
    {
        if (timerCoroutine != null)
        {
            Debug.Log("[TimedHelp] Stopping help timer because player succeeded.");
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }
    }

    // Coroutine ที่ทำงานนับเวลาถอยหลัง
    private IEnumerator HelpTimerCoroutine()
    {
        // หยุดรอตามเวลาที่กำหนด
        yield return new WaitForSeconds(timeUntilHelp);

        // เมื่อเวลาหมดแล้ว...
        Debug.Log("[TimedHelp] Timer finished! Triggering help dialog.");

        // เช็คอีกครั้งว่า helpDialog ถูกลากมาใส่หรือยัง
        if (helpDialog != null)
        {
            // สั่งให้เล่นบทพูด
            helpDialog.TriggerDialog();

            // ตั้งค่าว่าเคยช่วยไปแล้ว (จะได้ไม่ช่วยซ้ำ)
            hasTriggeredHelp = true;
        }

        // เคลียร์ค่า coroutine เมื่อทำงานเสร็จ
        timerCoroutine = null;
    }

    // --- ส่วนนี้สำหรับ "โซน" อัตโนมัติ (ถ้าต้องการ) ---

    // เมื่อ Player เดินเข้ามาในโซนนี้
    private void OnTriggerEnter2D(Collider2D other)
    {
        //if (other.CompareTag("Player"))
        //{
        //    StartHelpTimer();
        //}
    }

    // เมื่อ Player เดินออกจากโซนนี้
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            StopHelpTimer();
        }
    }
}