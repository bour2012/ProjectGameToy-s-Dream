using UnityEngine;

public class LevelTrigger : MonoBehaviour
{
    [Header("Trigger Settings")]
    public LevelManager levelManager;
    public int targetLevelIndex = -1; // -1 means next level
    public bool triggerOnPlayerEnter = true;
    public bool triggerOnce = true;
    
    [Header("Trigger Conditions")]
    public string requiredTag = "Player";
    
    private bool hasTriggered = false;
    
    void Start()
    {
        if (levelManager == null)
        {
            levelManager = FindAnyObjectByType<LevelManager>();
        }
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!triggerOnPlayerEnter || (triggerOnce && hasTriggered)) return;
        
        if (other.CompareTag(requiredTag))
        {
            TriggerLevelChange();
        }
    }
    
    public void TriggerLevelChange()
    {
        if (levelManager == null)
        {
            Debug.LogError("LevelManager reference not found!");
            return;
        }
        
        if (triggerOnce && hasTriggered) return;
        
        hasTriggered = true;
        
        //if (targetLevelIndex >= 0)
        //{
        //    levelManager.LoadSpecificLevel(targetLevelIndex);
        //}
        //else
        //{
        //    levelManager.NextLevel();
        //}
        
        Debug.Log($"Level change triggered. Target: {(targetLevelIndex >= 0 ? targetLevelIndex.ToString() : "Next Level")}");
    }
    
    // Method that can be called by other scripts or UI buttons
    public void TriggerNextLevel()
    {
        if (levelManager != null)
        {
            levelManager.NextLevel();
        }
    }
    
    public void TriggerSpecificLevel(int levelIndex)
    {
        if (levelManager != null)
        {
            //levelManager.LoadSpecificLevel(levelIndex);
        }
    }
}