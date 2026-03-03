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

    [Header("Settings")]
    [Tooltip("ชื่อ Tag ของพื้นที่จะทำให้กล่องปลดล็อกอัตโนมัติ (เช่น น้ำแข็ง/ทางลาด)")]
    public string slidingGroundTag = "SlideGround";

    [Header("Physics Settings")]
    [Tooltip("แรงหน่วงเวลาอยู่บนพื้นลื่น (ยิ่งน้อยยิ่งลื่นไถลไกล แต่อย่าให้เป็น 0) แนะนำ 0.5 - 2.0")]
    public float slipperyDamping = 1f; // <-- เพิ่มตัวแปรนี้เข้ามา

    private float defaultDamping; // <-- เอาไว้จำค่าเดิมก่อนลงพื้นลื่น

    void Awake()
    {
        // หา Rigidbody2D ของตัวเองเก็บไว้
        rb = GetComponent<Rigidbody2D>();

        // จำค่า damping ดั้งเดิมที่ตั้งไว้ใน Inspector (บนตัว Rigidbody2D)
        if (rb != null)
        {
            defaultDamping = rb.linearDamping;
        }
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
        // ถ้าหลุดออกจากพื้นลื่น
        if (collision.gameObject.CompareTag(slidingGroundTag))
        {
            isOnSlipperyGround = false;

            // คืนค่าความฝืดกลับเป็นปกติ
            if (rb != null)
            {
                rb.linearDamping = defaultDamping;
            }

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
                // ใส่แรงหน่วงนิดหน่อยตามที่ตั้งค่าไว้ใน Inspector กล่องจะค่อยๆ ชะลอแล้วหยุด
                rb.linearDamping = slipperyDamping;
            }
            else
            {
                // ถ้าไม่ได้อยู่บนพื้นลื่น ก็ให้ใช้ค่าหน่วงปกติ
                rb.linearDamping = defaultDamping;
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
        }
    }
}