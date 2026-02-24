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
