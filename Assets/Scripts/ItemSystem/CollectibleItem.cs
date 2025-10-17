using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CollectibleItem : MonoBehaviour
{
    [Header("Item Settings")]
    public ItemManager.ItemType itemType = ItemManager.ItemType.Glue;
    public int itemAmount = 1;
    [Tooltip("ID เฉพาะสำหรับไอเทมชิ้นนี้ (ถ้าเว้นว่างจะสร้างให้อัตโนมัติ)")]
    public string itemID;

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

        // ตรวจสอบกับ GameManager ว่าเคยถูกเก็บไปแล้วหรือยัง
        if (GameManager.Instance != null && GameManager.Instance.HasItemBeenCollected(itemID))
        {
            // ถ้าเคยเก็บแล้ว -> ทำลายตัวเองทิ้งไปเงียบๆ
            Destroy(gameObject);
            return;
        }

        // ทำให้แน่ใจว่า Collider เป็น Trigger
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // ถ้าผู้เล่นเดินมาชน และยังไม่เคยถูกเก็บ
        if (!isCollected && other.CompareTag("Player"))
        {
            Collect();
        }
    }

    void Collect()
    {
        if (isCollected || ItemManager.Instance == null) return;
        isCollected = true;

        // 1. บอก GameManager ให้ "จำไว้" ว่าไอเทมชิ้นนี้ถูกเก็บแล้ว
        if (GameManager.Instance != null)
        {
            GameManager.Instance.MarkItemAsCollected(itemID);
        }

        // 2. เพิ่มไอเทมให้ผู้เล่น
        ItemManager.Instance.CollectItem(itemType, itemAmount);

        // 3. เล่นเอฟเฟกต์
        if (collectEffect != null)
            Instantiate(collectEffect, transform.position, Quaternion.identity); // สร้าง Effect แยกออกมา

        if (collectSound != null)
            AudioSource.PlayClipAtPoint(collectSound.clip, transform.position); // เล่นเสียง ณ ตำแหน่งที่เก็บ

        // 4. ทำลายตัวเองทันที
        Destroy(gameObject);
    }
}