using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // --- [เพิ่มใหม่] อย่าลืม using TMPro เพื่อใช้ Text ---

public class PauseMenu : MonoBehaviour
{
    [SerializeField] CanvasGroup pauseMenuGroup;

    // --- [เพิ่มใหม่] ช่องสำหรับใส่ Text เวลาในหน้า Pause ---
    [SerializeField] TextMeshProUGUI pauseTimeText;
    // ---------------------------------------------

    private bool isPaused = false;

    void Start()
    {
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

        if (pauseMenuGroup != null)
        {
            pauseMenuGroup.alpha = pause ? 1 : 0;
            pauseMenuGroup.interactable = pause;
            pauseMenuGroup.blocksRaycasts = pause;
        }

        if (pause)
        {
            SpeedrunTimer.Instance?.PauseTimer();

            // --- [เพิ่มใหม่] ดึงเวลาปัจจุบันมาโชว์ตอนเข้าเมนู Pause ---
            if (pauseTimeText != null && SpeedrunTimer.Instance != null)
            {
                float currentPlayTime = SpeedrunTimer.Instance.GetTotalTime();
                pauseTimeText.text = "Time: " + SpeedrunTimer.FormatTime(currentPlayTime);
            }
            // ----------------------------------------------------
        }
        else
        {
            SpeedrunTimer.Instance?.ResumeTimer();
        }
    }

    public void MainMenu()
    {
        Time.timeScale = 1;
        SpeedrunTimer.Instance?.PauseTimer();
        SceneManager.LoadScene("Main Menu");
    }

    public void Resume()
    {
        SetPauseState(false);
    }

    public void Restart()
    {
        Time.timeScale = 1;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ManualReset();
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}