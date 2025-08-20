using UnityEngine;

public class LadderTrigger : MonoBehaviour
{
    [Header("Ladder Properties")]
    public bool allowClimbFromBottom = true;
    public bool allowClimbFromTop = true;

    private void Start()
    {
        // ตั้งค่า Trigger และ Layer
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        // ตั้ง Layer เป็น Ladder Layer (8)
        gameObject.layer = 8;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player entered ladder area");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player exited ladder area");
        }
    }
}
