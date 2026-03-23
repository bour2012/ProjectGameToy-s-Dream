using UnityEngine;

public class SpeedrunEventTrigger : MonoBehaviour
{
   
    public void TriggerStartTimer()
    {
        if (SpeedrunTimer.Instance != null)
        {
            SpeedrunTimer.Instance.StartNewRun();
            Debug.Log("Event Triggered: Start Speedrun Timer");
        }
    }

 
    public void TriggerCompleteTimer()
    {
        if (SpeedrunTimer.Instance != null)
        {
            SpeedrunTimer.Instance.CompleteRun();
            Debug.Log("Event Triggered: Complete Speedrun Timer");
        }
    }


    public void TriggerPauseTimer()
    {
        SpeedrunTimer.Instance?.PauseTimer();
    }

    public void TriggerResumeTimer()
    {
        SpeedrunTimer.Instance?.ResumeTimer();
    }
}