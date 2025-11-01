using UnityEngine;

public class ImmovableByPlayer : MonoBehaviour
{
    public Rigidbody2D rb;

    void Awake()
    {
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    /// <summary>
    /// ทำงานเมื่อผู้เล่นเข้ามาใน "ออร่าเตือนภัย" (Trigger)
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 2. ตรวจสอบว่าเป็น "ร่างกาย" ของผู้เล่นหรือไม่ (ไม่ใช่ Trigger อื่นๆ)
        if (other.CompareTag("Player") && !other.isTrigger)
        {
          
            //rb.bodyType = RigidbodyType2D.Kinematic;
            rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        }
    }

    /// <summary>
    /// ทำงานเมื่อผู้เล่นเดินออกจาก "ออร่าเตือนภัย" (Trigger)
    /// </summary>
    private void OnTriggerExit2D(Collider2D other)
    {
        // 4. ตรวจสอบว่าเป็น "ร่างกาย" ของผู้เล่นหรือไม่
        if (other.CompareTag("Player") && !other.isTrigger)
        {
            // 5. "ปลดล็อก" กล่องกลับเป็น Dynamic
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.constraints = /*RigidbodyConstraints2D.FreezePositionX | */RigidbodyConstraints2D.FreezeRotation;
        }
    }
}