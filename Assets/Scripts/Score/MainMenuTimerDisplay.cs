using UnityEngine;
using TMPro;

public class MainMenuTimerDisplay : MonoBehaviour
{
    public TextMeshProUGUI timeText;

    void Start()
    {
        // ดึงเวลาที่เซฟไว้ (ถ้าไม่เคยเล่นจบเลย จะได้ค่า 0 กลับมา)
        float savedTime = PlayerPrefs.GetFloat("GameClearTime", 0f);

        if (savedTime > 0f)
        {
            // ถ้าเวลามากกว่า 0 (แปลว่าเคยเล่นจบแล้ว) ให้แสดงข้อความ
            timeText.gameObject.SetActive(true);
            timeText.text = "Clear Time: " + SpeedrunTimer.FormatTime(savedTime);
        }
        else
        {
            // ถ้าเวลาเป็น 0 หรือยังไม่เคยเล่นจบ ให้ซ่อน UI Text ไปเลย
            timeText.gameObject.SetActive(false);
        }
    }
}