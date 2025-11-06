using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CollectibleItem : MonoBehaviour
{
    public enum CollectibleType { GenericItem, Key }
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
        if (isCollected || ItemManager.Instance == null) return;
        isCollected = true;

        switch (type)
        {
            case CollectibleType.GenericItem:
                // 1. บอก GameManager ให้จำว่าเก็บไปแล้ว โดยใช้ itemID ที่เป็นเอกลักษณ์
                // GameManager.Instance?.MarkItemAsCollected(this.name); // <--- บรรทัดเดิมที่ใช้ชื่อ
                GameManager.Instance?.MarkItemAsCollected(this.itemID);   // <--- บรรทัดที่แก้ไขใหม่!

                // 2. เพิ่มไอเทมให้ผู้เล่น
                ItemManager.Instance.CollectItem(itemType, itemAmount);
                break;

            case CollectibleType.Key:
                // การเก็บกุญแจจะไม่ถูกบันทึกในระบบนี้ ซึ่งถูกต้องตามที่คุณต้องการ
                ItemManager.Instance.AddKey(keyID);
                break;
        }

        // 3. เล่นเอฟเฟกต์
        if (collectEffect != null)
            Instantiate(collectEffect, transform.position, Quaternion.identity);

        if (collectSound != null)
            AudioSource.PlayClipAtPoint(collectSound.clip, transform.position);

        // 4. ทำลายตัวเองทันที
        Destroy(gameObject);
    }
}