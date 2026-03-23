using UnityEngine;
using TMPro;

public class MainMenuTimerDisplay : MonoBehaviour
{
    public TextMeshProUGUI timeText;

    void Start()
    {
       
        float savedTime = PlayerPrefs.GetFloat("GameClearTime", 0f);

        if (savedTime > 0f)
        {
       
            timeText.gameObject.SetActive(true);
            timeText.text = "Clear Time: " + SpeedrunTimer.FormatTime(savedTime);
        }
        else
        {
           
            timeText.gameObject.SetActive(false);
        }
    }
}