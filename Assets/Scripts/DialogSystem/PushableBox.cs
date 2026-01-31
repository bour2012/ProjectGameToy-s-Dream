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
    private bool isOnSlipperyGround = false;
    // ฟังก์ชันนี้จะถูกเรียกโดย Player
    [Header("Settings")]
    [Tooltip("ชื่อ Tag ของพื้นที่จะทำให้กล่องปลดล็อกอัตโนมัติ (เช่น น้ำแข็ง/ทางลาด)")]
    public string slidingGroundTag = "SlideGround"; // <-- 1. เพิ่มตัวแปรสำหรับชื่อ Tag
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
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // ถ้าสิ่งที่ชนมี Tag ตรงกับที่เราตั้งไว้ (เช่น "SlideGround")
        if (collision.gameObject.CompareTag(slidingGroundTag))
        {
            isOnSlipperyGround = true;
            UnlockBox(); // <-- ปลดล็อกทันที ไม่ต้องรอ E
            Debug.Log($"[PushableBox] Hit sliding ground: {collision.gameObject.name}. Unlocked physics.");
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        // (Optional) ถ้าหลุดออกจากพื้นลื่น จะให้กลับมาล็อคเหมือนเดิมไหม?
        // ถ้าต้องการให้พ้นพื้นลื่นแล้วหยุดไหล ให้เปิดคอมเมนต์บรรทัดล่างนี้ครับ
        if (collision.gameObject.CompareTag(slidingGroundTag))
        {
             isOnSlipperyGround = false;
             // LockBox(); // <-- ถ้าอยากให้หยุดทันทีเมื่อพ้นพื้นลื่น ให้เอา comment ออก
        }
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
            if (isOnSlipperyGround)
            {
                rb.linearDamping = 0f; // ลื่นปรื๊ด
            }
        }
    }
    public void LockBox()
    {
        if (isOnSlipperyGround) return;
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
            //Debug.Log($"[PushableBox] '{gameObject.name}' Locked.");
        }
    }
}