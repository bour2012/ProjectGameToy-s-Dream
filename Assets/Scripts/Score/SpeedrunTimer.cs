using UnityEngine;

public class SpeedrunTimer : MonoBehaviour
{
    public static SpeedrunTimer Instance { get; private set; }

    private float currentTime = 0f;
    private bool isTimerRunning = false;

    // คีย์สำหรับเซฟเวลาจบเกมลงเครื่อง
    private const string SAVE_KEY = "GameClearTime";

    void Awake()
    {
        // ทำให้สคริปต์นี้อยู่ยงคงกระพัน ไม่โดนทำลายตอนเปลี่ยนด่าน/ตาย
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        if (isTimerRunning)
        {
            currentTime += Time.deltaTime;
        }
    }

    // --- 1. เรียกใช้ตอน "เริ่มเกมด่าน Tutorial" หรือ "กดปุ่ม New Game" ---
    public void StartNewRun()
    {
        currentTime = 0f;
        isTimerRunning = true;
    }
    public float GetTotalTime()
    {
        return currentTime;
    }
    // --- (Optional) เอาไว้ให้ GameManager เรียกหยุดเวลาตอนพักเกม/ดูฉาก ---
    public void PauseTimer() { isTimerRunning = false; }
    public void ResumeTimer() { isTimerRunning = true; }

    // --- 2. เรียกใช้ตอน "จบด่านสุดท้าย" หรือ "ฆ่าบอสใหญ่ตาย" ---
    public void CompleteRun()
    {
        isTimerRunning = false;

        // บันทึกเวลาที่เล่นจบลง PlayerPrefs (เซฟถาวร)
        PlayerPrefs.SetFloat(SAVE_KEY, currentTime);
        PlayerPrefs.Save();

        Debug.Log("Game Cleared! Time saved: " + FormatTime(currentTime));
    }

    // --- ฟังก์ชันแปลงเวลาตัวเลข ให้เป็นข้อความแบบ HH:MM:SS ---
    public static string FormatTime(float timeInSeconds)
    {
        int hours = Mathf.FloorToInt(timeInSeconds / 3600f);
        int minutes = Mathf.FloorToInt((timeInSeconds % 3600f) / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);

        // จัดฟอร์แมตให้เป็นเลข 2 หลักเสมอ เช่น 01:05:09
        return string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds);
    }
}