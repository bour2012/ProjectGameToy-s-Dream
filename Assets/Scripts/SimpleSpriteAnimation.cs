using UnityEngine;

public class SimpleSpriteAnimation : MonoBehaviour
{
    public Sprite[] frames;        // ลากรูป Sprite ทั้งหมดมาใส่ในช่องนี้
    public float fps = 10f;       // ความเร็ว (กี่เฟรมต่อวินาที)

    private SpriteRenderer spriteRenderer;
    private int currentIndex;
    private float timer;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // คำนวณเวลาเพื่อเปลี่ยนเฟรม
        timer += Time.deltaTime;

        if (timer >= 1f / fps)
        {
            timer -= 1f / fps;

            // ขยับ Index ไปรูปถัดไป (ถ้าถึงรูปสุดท้ายจะวนกลับมาที่ 0)
            currentIndex = (currentIndex + 1) % frames.Length;

            // เปลี่ยนรูปที่แสดง
            spriteRenderer.sprite = frames[currentIndex];
        }
    }
}