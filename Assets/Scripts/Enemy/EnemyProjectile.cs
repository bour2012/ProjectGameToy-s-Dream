using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public int damage = 1;

    void Start()
    {
        // ทำลายตัวเองหลังจาก 5 วินาที (ถ้าไม่ชนอะไรเลย)
        Destroy(gameObject, 5f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // สร้างความเสียหายให้ Player (คุณต้องไปสร้างฟังก์ชันนี้ใน PlayerHealth)
            // other.GetComponent<PlayerHealth>()?.TakeDamage(damage);
            Destroy(gameObject);
        }
        else if (other.CompareTag("DistractableDoll"))
        {
            // สร้างความเสียหายให้ตุ๊กตา
            other.GetComponent<CraftedObject>()?.TakeDamage(damage);
            Destroy(gameObject);
        }
        else if (!other.CompareTag("Enemy") && !other.isTrigger)
        {
            // ชนกำแพงหรือพื้น
            Destroy(gameObject);
        }
    }
}