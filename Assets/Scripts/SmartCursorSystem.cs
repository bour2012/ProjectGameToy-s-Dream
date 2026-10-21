using UnityEngine;

public class SmartCursorSystem : MonoBehaviour
{
    [Header("Cursor Icons")]
    public Texture2D defaultCursor;
    public Texture2D threadValidCursor;
    public Texture2D glueValidCursor;

    [Header("Settings")]
    public Vector2 hotSpot = Vector2.zero;

    [Header("Detection Layers")]
    [Tooltip("เลือก Layer ของจุดโหน (เช่น RopePoint, Wall)")]
    public LayerMask ropeableLayer;

    [Tooltip("เลือก Layer ของพื้น/กำแพง (เช่น Ground)")]
    public LayerMask glueableLayer;

    [Header("References")]
    public GlueShooting glueShootingScript;
    public Rope ropeScript;

    private Camera mainCamera;

    // สร้าง Array เตรียมไว้แค่ครั้งเดียว (รับได้สูงสุด 10 ชิ้นซ้อนกัน)
    // ช่วยลดการสร้างขยะ (Garbage) ในหน่วยความจำได้มหาศาล
    private RaycastHit2D[] hitsBuffer = new RaycastHit2D[10];

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (glueShootingScript == null) return;
        UpdateSmartCursor();
    }

    void UpdateSmartCursor()
    {
        Texture2D cursorToUse = defaultCursor;

        // คำนวณตำแหน่งเมาส์
        Vector3 screenPosition = Input.mousePosition;
        screenPosition.z = -mainCamera.transform.position.z; // แก้เรื่องกล้อง Perspective
        Vector2 mousePos = mainCamera.ScreenToWorldPoint(screenPosition);

        // ดึงไอเทมที่ถืออยู่มาเก็บไว้ก่อน (Optimization: เรียกครั้งเดียวพอนอกลูป)
        ItemManager.ItemType currentItem = glueShootingScript.GetSelectedItem();

        // รวม Layer เป้าหมาย
        LayerMask targetLayers = ropeableLayer | glueableLayer;
        int hitCount = Physics2D.RaycastNonAlloc(mousePos, Vector2.zero, hitsBuffer, 100f, targetLayers);

        // --- กรณีถือเชือก (Thread) ---
        if (currentItem == ItemManager.ItemType.Thread)
        {
            // ถ้า Rope Script ตรวจพบเป้าหมายที่พร้อมจะ Snap ในทิศทางที่เล็ง ให้เปลี่ยน Cursor ทันที
            if (ropeScript != null && ropeScript.HasValidTarget())
            {
                cursorToUse = threadValidCursor;
            }
            else
            {
                // Fallback: ตรวจสอบกรณีเมาส์ชี้ไปที่ตัวจุดโหนโดยตรง
                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit2D hit = hitsBuffer[i];
                    int hitLayer = hit.collider.gameObject.layer;

                    if (IsLayerInMask(hitLayer, ropeableLayer))
                    {
                        float dist = Vector2.Distance(glueShootingScript.transform.position, mousePos);
                        if (ropeScript == null || dist <= ropeScript.ropeMaxCastDistance)
                        {
                            cursorToUse = threadValidCursor;
                            break;
                        }
                    }
                }
            }
        }
        // --- กรณีถือกาว (Glue) ---
        else if (currentItem == ItemManager.ItemType.Glue)
        {
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = hitsBuffer[i];
                int hitLayer = hit.collider.gameObject.layer;

                if (IsLayerInMask(hitLayer, glueableLayer))
                {
                    cursorToUse = glueValidCursor;
                    break;
                }
            }
        }

        Cursor.SetCursor(cursorToUse, hotSpot, CursorMode.Auto);
    }

    // ฟังก์ชันเช็ค Layer แบบ Inline (เร็วขึ้นนิดหน่อย)
    private bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask == (mask | (1 << layer)));
    }
}