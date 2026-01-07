using UnityEngine;

public class PlayerKnockback : MonoBehaviour
{
    private Rigidbody2D rb;
    private bool isKnocked = false;

    // 1. ลากสคริปต์ PlayerMovement มาใส่ในช่องนี้ที่หน้า Inspector
    // (ถ้าลืมลาก ตัวโค้ดจะพยายามหาอัตโนมัติใน Start())
    public PlayerMovement movementScript; 

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        // พยายามหาให้อัตโนมัติถ้าลืมลากใส่ Inspector
        if (movementScript == null)
            movementScript = GetComponent<PlayerMovement>();
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (isKnocked) return;

        Debug.Log("Player: โดนชนกระเด็น!");
        isKnocked = true;

        // 2. ปิดการบังคับ (Disabled Script) ชั่วคราว เพื่อให้ Physics ทำงานได้เต็มที่
        if(movementScript != null) movementScript.enabled = false;

        // หยุดความเร็วเดิม
        rb.linearVelocity = Vector2.zero;

        // ใส่แรงกระแทก
        rb.AddForce((direction + Vector2.up * 0.5f).normalized * force, ForceMode2D.Impulse);

        // เริ่มนับเวลาให้กลับมาบังคับได้อีกครั้ง
        Invoke("Recover", 0.5f); // เวลา 0.5 วินาทีคือกำลังดี
    }

    void Recover()
    {
        isKnocked = false;
        // เปิดการบังคับกลับมา
        if(movementScript != null) movementScript.enabled = true;
    }
}