using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // --- [��������] ������� using TMPro ������ Text ---

public class PauseMenu : MonoBehaviour
{
    [SerializeField] CanvasGroup pauseMenuGroup;

    // --- [��������] ��ͧ����Ѻ��� Text �����˹�� Pause ---
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
    public void OpenSetting()
    {
       
            isPaused = !isPaused;
            SetPauseState(isPaused);
        
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

            // --- [��������] �֧���һѨ�غѹ�����͹������� Pause ---
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
        //SpeedrunTimer.Instance.SaveCurrentTimeProgress();
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