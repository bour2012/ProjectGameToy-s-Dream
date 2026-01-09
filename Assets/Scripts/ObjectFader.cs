using UnityEngine;

public class ObjectFader : MonoBehaviour
{
    [Header("ลากปุ่ม E มาใส่ตรงนี้")]
    public GameObject targetObject; // ตัว Object ปุ่ม E

    [Header("ความเร็วในการ จาง/โผล่")]
    public float fadeSpeed = 10f;

    // เราจะคุม SpriteRenderer (เผื่อปุ่มมีหลายชิ้น เช่น กรอบปุ่ม + ตัวอักษร)
    private SpriteRenderer[] sprites;
    private float targetAlpha = 0f; // 0 = หาย, 1 = เห็น
    private float currentAlpha = 0f;

    void Start()
    {
        if (targetObject != null)
        {
            // ดึง SpriteRenderer ทั้งหมดในปุ่ม E (รวมลูกๆ ด้วย)
            sprites = targetObject.GetComponentsInChildren<SpriteRenderer>();

            // เริ่มเกมมา สั่งซ่อนก่อนเลย
            SetAlpha(0f);
            currentAlpha = 0f;
        }
    }

    void Update()
    {
        // คำนวณค่า Alpha ให้ค่อยๆ เปลี่ยน (Smooth)
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
        
        // อัปเดตค่า Alpha ให้กับ Sprite ทุกชิ้น
        SetAlpha(currentAlpha);
    }

    // ฟังก์ชันช่วยวนลูปปรับสี
    void SetAlpha(float alphaVal)
    {
        if (sprites == null) return;

        foreach (SpriteRenderer sr in sprites)
        {
            Color tmpColor = sr.color;
            tmpColor.a = alphaVal; // ปรับแค่ค่า a (Alpha)
            sr.color = tmpColor;
        }
    }

    // เมื่อเดินเข้า
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            targetAlpha = 1f; // สั่งให้โผล่
        }
    }

    // เมื่อเดินออก
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            targetAlpha = 0f; // สั่งให้หาย
        }
    }
}