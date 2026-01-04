using UnityEngine;
using UnityEngine.SceneManagement;
public class PauseMenu : MonoBehaviour
{
    [SerializeField] GameObject pauseMenu;
    [SerializeField] GameObject pauseMenu_Help;
    

    public void Pause()
    {
        pauseMenu.SetActive(true);
        Time.timeScale = 0;
    }
    public void MainMenu()
    {
        SceneManager.LoadScene("Main Menu");
    }
    public void Resume()
    {
        pauseMenu.SetActive(false);
        Time.timeScale = 1;
    }
    public void Help()
    {
        pauseMenu_Help.SetActive(!pauseMenu_Help.activeSelf);
    }

    //public void CloseHelp()
    //{
    //    pauseMenu_Help.SetActive(false);
    //}
}
