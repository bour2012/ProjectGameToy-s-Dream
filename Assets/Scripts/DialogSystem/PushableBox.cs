using UnityEngine;
using UnityEngine.Events; // <-- สำคัญมาก!

public class PushableBox : MonoBehaviour
{
    [Header("Events")]
    [Tooltip("Event ที่จะทำงานเมื่อผู้เล่นเริ่มผลักกล่องนี้ครั้งแรก")]
    public UnityEvent onPlayerStartPush;
    private Rigidbody2D rb;
    public bool modeXUnLock = false;
    private bool hasBeenPushed = false;

    // ฟังก์ชันนี้จะถูกเรียกโดย Player

    void Awake()
    {
        // หา Rigidbody2D ของตัวเองเก็บไว้
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        // "ใส่เบรกมือ" ให้กล่องทันทีที่เริ่มเกม
        LockBox();
    }

    public void NotifyPlayerIsPushing()
    {
        // ถ้ายังไม่เคยถูกผลักมาก่อน
        if (!hasBeenPushed)
        {
            // "กดปุ่ม" Event
            onPlayerStartPush.Invoke();

            // ตั้งค่าว่าเคยถูกผลักไปแล้ว (เพื่อไม่ให้ทำงานซ้ำ)
            hasBeenPushed = true;
        }
    }

    public void UnlockBox()
    {
        if (rb != null)
        {
            // ปลดล็อกแกน X และ Y แต่ยังคงล็อกการหมุนไว้
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            Debug.Log($"[PushableBox] '{gameObject.name}' Unlocked.");
        }
    }
    public void LockBox()
    {
        if (rb != null)
        {
            // ล็อกทุกอย่าง: ตำแหน่ง X, Y และการหมุน Z

            if (modeXUnLock)
            {
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                Debug.Log($"[PushableBox] '{gameObject.name}' Locked Y axis only.");
                return;
            }
            rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
            Debug.Log($"[PushableBox] '{gameObject.name}' Locked.");
        }
    }
}