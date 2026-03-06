using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DeathDialogZone : MonoBehaviour
{
    [Tooltip("ลาก DialogTrigger ของบทพูดตอนตายในโซนนี้มาใส่")]
    public DialogTrigger deathDialogToTrigger;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            //Debug.Log("<color=cyan>Player entered death zone!</color>"); // <-- เพิ่ม
            DeathDialogManager manager = other.GetComponent<DeathDialogManager>();
            if (manager != null)
            {
                manager.SetDeathDialog(deathDialogToTrigger);
                //Debug.Log("<color=yellow>Death Dialog Set To: </color>" + deathDialogToTrigger.name);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            DeathDialogManager manager = other.GetComponent<DeathDialogManager>();
            if (manager != null)
            {
                manager.ClearDeathDialog();
            }
        }
    }
}