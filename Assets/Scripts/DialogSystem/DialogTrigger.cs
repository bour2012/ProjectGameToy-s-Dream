using UnityEngine;

public class DialogTrigger : MonoBehaviour
{
    [TextArea(2, 5)]
    public string[] dialogLines;

    public AudioClip dialogVoice;

    [Header("Camera Focus")]
    public Transform focusTarget;  // เช่น ศัตรู
    public bool shakeCamera = false;

    private bool hasTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !hasTriggered)
        {
            hasTriggered = true;
            DialogManager.Instance.StartDialog(dialogLines, dialogVoice, focusTarget, shakeCamera);
        }
    }
}
