using UnityEngine;

public class UltimateGlueBullet : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Boss")) // อย่าลืมติด Tag Boss ให้บอส
        {
            BossController boss = other.GetComponent<BossController>();
            if (boss != null)
            {
                boss.HitByUltimateGlue(); // สั่งบอสร่วง
                Destroy(gameObject); // ทำลายกระสุน
            }
        }
    }
}