using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] CanvasGroup pauseMenuGroup;
    [SerializeField] TextMeshProUGUI pauseTimeText;

    private bool isPaused = false;

    void Start()
    {
        SetPauseState(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // ตรวจสอบว่าเปิดหรือปิดอยู่
            if (isPaused)
            {
                Resume();
            }
            else
            {
                OpenSetting();
            }
        }
    }

    public void OpenSetting()
    {
        // เช็คก่อนว่าสามารถเปิดเมนูได้ไหม (เช่น ถ้าตายอยู่จะได้ไม่เปิดทับ)
        if (GameManager.Instance != null && !GameManager.Instance.CanChangeToState(GameState.Menu))
            return;

        SetPauseState(true);
    }

    public void Resume()
    {
        SetPauseState(false);
    }

    void SetPauseState(bool pause)
    {
        isPaused = pause;
        Time.timeScale = pause ? 0 : 1;

        if (pauseMenuGroup != null)
        {
            pauseMenuGroup.alpha = pause ? 1 : 0;
            pauseMenuGroup.interactable = pause;
            pauseMenuGroup.blocksRaycasts = pause; // ป้องกันการกดโดนปุ่มตอนที่เมนูปิดอยู่
        }

        if (pause)
        {
            SpeedrunTimer.Instance?.PauseTimer();

            if (pauseTimeText != null && SpeedrunTimer.Instance != null)
            {
                float currentPlayTime = SpeedrunTimer.Instance.GetTotalTime();
                pauseTimeText.text = "Time: " + SpeedrunTimer.FormatTime(currentPlayTime);
            }

            // --- [เพิ่มใหม่] สั่ง GameManager ให้เปลี่ยน State เป็น Menu เพื่อหยุด Player ---
            if (GameManager.Instance != null && GameManager.Instance.currentState != GameState.Menu)
            {
                GameManager.Instance.OpenMenu();
            }
        }
        else
        {
            SpeedrunTimer.Instance?.ResumeTimer();

            // --- [เพิ่มใหม่] สั่ง GameManager ให้เปลี่ยน State กลับเป็น Normal เพื่อคืนการควบคุม ---
            if (GameManager.Instance != null && GameManager.Instance.currentState == GameState.Menu)
            {
                GameManager.Instance.CloseMenu();
            }
        }
    }

    public void MainMenu()
    {
        Time.timeScale = 1;
        SpeedrunTimer.Instance?.PauseTimer();
        SceneManager.LoadScene("Main Menu");
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