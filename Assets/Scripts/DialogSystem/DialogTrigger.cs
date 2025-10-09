using UnityEngine;

[System.Serializable]
public class DialogData
{
    [TextArea(2, 5)]
    public string dialogText;
    public AudioClip dialogVoice;
    [Header("Camera Focus")]
    public Transform focusTarget;
    public bool shakeCamera = false;

}

public class DialogTrigger : MonoBehaviour
{
    [Header("Dialog Sequence")]
    public DialogData[] dialogSequence;

    [Header("Settings")]
    public bool triggerOnce = true;
    public bool autoAdvance = false;
    [Range(0.1f, 10f)]
    public float autoAdvanceDelay = 2f;

    private bool hasTriggered = false;

 
   
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && (!hasTriggered || !triggerOnce))
        {
            if (triggerOnce)
                hasTriggered = true;

            StartDialogSequence();
        }
    }

    private void StartDialogSequence()
    {
        if (dialogSequence == null || dialogSequence.Length == 0)
        {
            Debug.LogWarning("Dialog sequence is empty!");
            return;
        }

        if (DialogManager.Instance != null)
        {
            DialogManager.Instance.StartDialogSequence(dialogSequence, autoAdvance, autoAdvanceDelay);
        }
        else
        {
            Debug.LogError("DialogManager instance not found!");
        }
    }

    // Optional: เรียก Dialog ด้วย script อื่น
    public void TriggerDialog()
    {
        StartDialogSequence();
    }

    // Optional: Reset trigger
    public void ResetTrigger()
    {
        hasTriggered = false;
    }
}