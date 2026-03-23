using UnityEngine;
using UnityEngine.SceneManagement;

public class SpeedrunTimer : MonoBehaviour
{
    public static SpeedrunTimer Instance { get; private set; }

    private float currentTime = 0f;
    private bool isTimerRunning = false;

    private const string SAVE_KEY = "GameClearTime";
    private const string RUNTIME_KEY = "CurrentRunTime"; // เพิ่ม Key สำหรับเก็บเวลาที่กำลังเดินอยู่

    void Awake()
    {
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

    // แก้ไขฟังก์ชันนี้ให้เช็คว่ากด Continue หรือ New Game
    public void StartNewRun()
    {
        if (PlayerPrefs.GetInt("IsContinuing", 0) == 0)
        {
            currentTime = 0f;
            PlayerPrefs.SetFloat(RUNTIME_KEY, 0f);
            isTimerRunning = true;
            Debug.Log("<color=cyan>[Timer] Started New Run. Time reset to 0.</color>");
        }
    }

    public float GetTotalTime()
    {
        return currentTime;
    }

    public void PauseTimer()
    {
        isTimerRunning = false;
    }

    public void ResumeTimer()
    {
        isTimerRunning = true;
    }

    // เพิ่มฟังก์ชันนี้สำหรับเรียกตอนกด Quilt Game
    public void SaveCurrentTimeProgress()
    {
        PlayerPrefs.SetFloat(RUNTIME_KEY, currentTime);
        PlayerPrefs.Save();
        Debug.Log("Saved current run time: " + FormatTime(currentTime));
    }

    public void CompleteRun()
    {
        isTimerRunning = false;
        PlayerPrefs.SetFloat(SAVE_KEY, currentTime);

        // เมื่อเคลียร์เกม ลบสถานะเซฟเกมทิ้ง เพื่อไม่ให้กด Continue ได้อีกจนกว่าจะเริ่มเล่นใหม่
        PlayerPrefs.DeleteKey("HasSavedGame");
        PlayerPrefs.DeleteKey(RUNTIME_KEY);

        PlayerPrefs.Save();
        Debug.Log("Game Cleared! Time saved: " + FormatTime(currentTime));
    }

    public static string FormatTime(float timeInSeconds)
    {
        int hours = Mathf.FloorToInt(timeInSeconds / 3600f);
        int minutes = Mathf.FloorToInt((timeInSeconds % 3600f) / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        return string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // ถ้าโหลดเข้าหน้าชื่อ "MainMenu" ให้หยุดเวลาและบันทึกเวลาทันที!
        if (scene.name == "Main Menu")
        {
            isTimerRunning = false;
            if (currentTime > 0f)
            {
                SaveCurrentTimeProgress();
                Debug.Log("<color=orange>[Timer] ตรวจพบหน้า Main Menu: บังคับหยุดและเซฟเวลาอัตโนมัติ</color>");
            }
        }
        else
        {
            if (PlayerPrefs.GetInt("IsContinuing", 0) == 1)
            {
                // ดึงเวลาเดิมกลับมาและสั่งให้เวลาเดินทันทีที่โหลดฉากเสร็จ!
                currentTime = PlayerPrefs.GetFloat(RUNTIME_KEY, 0f);
                isTimerRunning = true;

                // ล้างค่าตัวนี้ทิ้ง เพื่อป้องกันการโหลดฉากถัดไปแล้วเวลาบั๊ก
                PlayerPrefs.SetInt("IsContinuing", 0);
                PlayerPrefs.Save();

                Debug.Log("<color=lime>[Timer] กลับเข้าเกม (Continue): เริ่มนับเวลาต่อจาก " + FormatTime(currentTime) + "</color>");
            }
        }
    }
}