using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] CanvasGroup pauseMenuGroup; // เปลี่ยนจาก GameObject เป็น CanvasGroup
    //[SerializeField] GameObject pauseMenu_Help;

    private bool isPaused = false;

    void Start()
    {
        // เริ่มเกมมา สั่งซ่อนทันที
        SetPauseState(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isPaused = !isPaused;
            SetPauseState(isPaused);
        }
    }

    void SetPauseState(bool pause)
    {
        isPaused = pause;
        Time.timeScale = pause ? 0 : 1;

        // เทคนิค Canvas Group: ซ่อนแต่ไม่ปิด
        if (pauseMenuGroup != null)
        {
            pauseMenuGroup.alpha = pause ? 1 : 0; // 1=เห็น, 0=ไม่เห็น
            pauseMenuGroup.interactable = pause;  // กดปุ่มได้ไหม
            pauseMenuGroup.blocksRaycasts = pause; // บังเมาส์ไหม
        }
    }

    public void MainMenu()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene("Main Menu");
    }

    public void Resume()
    {
        SetPauseState(false);
    }

    public void Restart()
    {
        // 1. คืนค่าเวลาให้เป็นปกติก่อนรีเซ็ต (สำคัญมาก! ไม่งั้นฉากใหม่จะค้าง)
        Time.timeScale = 1;

        // 2. เรียกใช้ฟังก์ชัน ManualReset แบบเดียวกับปุ่ม R ใน GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ManualReset();
        }
        else
        {
            // กรณีกันเหนียว (Fallback) ถ้าหา GameManager ไม่เจอจริงๆ ให้โหลดฉากเดิม
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    //public void Help()
    //{
    //    pauseMenu_Help.SetActive(!pauseMenu_Help.activeSelf);
    //}
}