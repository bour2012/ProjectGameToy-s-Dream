using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CollectibleItem : MonoBehaviour
{
    public enum CollectibleType { GenericItem, Key, BossPhaseTrigger }

    [Header("Item Settings")]
    public CollectibleType type = CollectibleType.GenericItem;

    [Tooltip("สำหรับ GenericItem: เลือกประเภทไอเทม")]
    public ItemManager.ItemType itemType = ItemManager.ItemType.Glue;
    [Tooltip("สำหรับ GenericItem: จำนวนที่จะได้รับ")]
    public int itemAmount = 1;
    [Tooltip("สำหรับ Key: ID เฉพาะของกุญแจดอกนี้ (ต้องตรงกับประตู)")]
    public string keyID;

    [Tooltip("ID เฉพาะสำหรับไอเทมชิ้นนี้ (ถ้าเว้นว่างจะสร้างให้อัตโนมัติ)")]
    public string itemID; // <-- เราจะใช้ตัวนี้เป็นหลักในการจดจำ

    [Header("Visual Settings")]
    public GameObject itemVisual;
    public ParticleSystem collectEffect;
    public AudioSource collectSound;

    // ==========================================
    // [ปรับใหม่] การตั้งค่าเอฟเฟกต์ลอยและแสงด้านหลัง
    // ==========================================
    [Header("Hover & Background Glow Effects")]
    public bool enableHoverEffect = true;      // เปิด/ปิด การลอย
    public float hoverSpeed = 2f;              // ความเร็วในการลอยขึ้นลง
    public float hoverHeight = 0.2f;           // ระยะความสูงที่ลอย

    public bool enableGlowEffect = true;       // เปิด/ปิด แสงด้านหลัง
    public SpriteRenderer glowSpriteRenderer;  // **สำคัญ** ลาก Sprite ของแสงด้านหลังมาใส่ช่องนี้!
    public float minGlowOpacity = 0.2f;        // ความสว่างขั้นต่ำ (Alpha 0-1)
    public float maxGlowOpacity = 1f;          // ความสว่างสูงสุด (Alpha 0-1)
    public float glowSpeed = 3f;               // ความเร็วกะพริบ

    private Vector3 startPosition;
    // ==========================================

    private bool isCollected = false;

    void Awake()
    {
        // สร้าง ID อัตโนมัติถ้าไม่ได้กำหนด
        if (string.IsNullOrEmpty(itemID))
        {
            itemID = $"{gameObject.scene.name}_{gameObject.name}_{transform.position.sqrMagnitude}";
        }

        // --- ส่วนสำคัญที่สุด ---
        // ตรวจสอบกับ GameManager (ที่ยังคงอยู่หลังตาย) ว่า itemID นี้เคยถูกเก็บไปแล้วหรือยัง
        if (GameManager.Instance != null && GameManager.Instance.HasItemBeenCollected(itemID))
        {
            // ถ้าเคยเก็บแล้ว -> ทำลายตัวเองทิ้งไปเงียบๆ ก่อนที่ผู้เล่นจะเห็น
            Destroy(gameObject);
            return; // ออกจากฟังก์ชันทันที ไม่ต้องทำอะไรต่อ
        }

        // ทำให้แน่ใจว่า Collider เป็น Trigger
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Start()
    {
        // จำตำแหน่งเริ่มต้นไว้ เพื่อให้ลอยขึ้นลงจากจุดเดิม
        startPosition = transform.position;

        // ดักกรณีผู้เล่นลืมลาก Glow Sprite มาใส่ ให้ลองหาอัตโนมัติ
        if (enableGlowEffect && glowSpriteRenderer == null)
        {
            // หา SpriteRenderer ในลูกๆ
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
            {
                // สมมติว่าแสงด้านหลังไม่ใช่ไอเทมหลัก (Visual)
                if (itemVisual == null || sr.gameObject != itemVisual)
                {
                    glowSpriteRenderer = sr;
                    break;
                }
            }
        }
    }

    void Update()
    {
        if (isCollected) return;

        // 1. เอฟเฟกต์ลอยขึ้นลง (Hover)
        if (enableHoverEffect)
        {
            float newY = startPosition.y + (Mathf.Sin(Time.time * hoverSpeed) * hoverHeight);
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }

        // 2. เอฟเฟกต์แสงด้านหลัง (Background Glow Pulse)
        if (enableGlowEffect && glowSpriteRenderer != null)
        {
            // ใช้ Mathf.Sin คำนวณค่าคลื่น (-1 ถึง 1) แปลงเป็น (0 ถึง 1)
            float glowAlpha = (Mathf.Sin(Time.time * glowSpeed) + 1f) / 2f;

            // ใช้ Mathf.Lerp เพื่อกะพริบ Alpha ระหว่างค่า Min กับ Max
            float finalAlpha = Mathf.Lerp(minGlowOpacity, maxGlowOpacity, glowAlpha);

            // เซ็ตสีใหม่โดยเปลี่ยนแค่ค่า Alpha (A)
            Color currentColor = glowSpriteRenderer.color;
            currentColor.a = finalAlpha;
            glowSpriteRenderer.color = currentColor;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isCollected && other.CompareTag("Player"))
        {
            Collect();
        }
    }

    void Collect()
    {
        if (isCollected) return; // ตัด ItemManager.Instance == null ออกเพื่อให้เก็บ Boss Item ได้แม้ไม่มี Manager
        isCollected = true;

        // บันทึกว่าเก็บแล้ว (ยกเว้น Key)
        if (type != CollectibleType.Key)
        {
            GameManager.Instance?.MarkItemAsCollected(this.itemID);
        }

        switch (type)
        {
            case CollectibleType.GenericItem:
                if (ItemManager.Instance != null)
                    ItemManager.Instance.CollectItem(itemType, itemAmount);
                break;

            case CollectibleType.Key:
                if (ItemManager.Instance != null)
                    ItemManager.Instance.AddKey(keyID);
                break;

            case CollectibleType.BossPhaseTrigger:
                Debug.Log("เก็บของครบ! ไปด่านต่อไป!");
                BossItemUI ui = FindFirstObjectByType<BossItemUI>();
                if (ui != null)
                {
                    ui.AddBossItem();
                }
                LevelManager levelMgr = FindFirstObjectByType<LevelManager>();
                if (levelMgr != null)
                {
                    // สั่งให้เปลี่ยนด่าน (เดี๋ยว LevelManager จะไปสั่งบอสเอง)
                    // ถ้ายังไม่จบเกม ให้ไปด่านถัดไป
                    levelMgr.NextLevel();

                }
                break;
        }

        if (collectEffect != null)
            Instantiate(collectEffect, transform.position, Quaternion.identity);

        if (collectSound != null)
            AudioSource.PlayClipAtPoint(collectSound.clip, transform.position);

        Destroy(gameObject);
    }
}