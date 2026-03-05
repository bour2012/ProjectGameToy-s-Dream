using UnityEngine;

public class SpeedrunEventTrigger : MonoBehaviour
{
    // ฟังก์ชันนี้สำหรับเรียกผ่าน Event เพื่อเริ่มนับเวลา
    public void TriggerStartTimer()
    {
        if (SpeedrunTimer.Instance != null)
        {
            SpeedrunTimer.Instance.StartNewRun();
            Debug.Log("Event Triggered: Start Speedrun Timer");
        }
    }

    // ฟังก์ชันนี้สำหรับเรียกผ่าน Event เพื่อจบเวลาและเซฟ
    public void TriggerCompleteTimer()
    {
        if (SpeedrunTimer.Instance != null)
        {
            SpeedrunTimer.Instance.CompleteRun();
            Debug.Log("Event Triggered: Complete Speedrun Timer");
        }
    }

    // (แถม) เผื่ออยากเอาไปผูกกับ Event หยุดเวลาชั่วคราว
    public void TriggerPauseTimer()
    {
        SpeedrunTimer.Instance?.PauseTimer();
    }

    public void TriggerResumeTimer()
    {
        SpeedrunTimer.Instance?.ResumeTimer();
    }
}