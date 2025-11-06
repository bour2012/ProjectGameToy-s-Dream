using UnityEngine;

public class Shield : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        // ตรวจสอบว่าสิ่งที่ชนคือ "กระสุนศัตรู" หรือไม่
        // (เราต้องไปตั้ง Tag "EnemyProjectile" ให้กับ Prefab กระสุนของศัตรูด้วย)
        if (other.CompareTag("EnemyProjectile"))
        {
      
            Destroy(other.gameObject);
        }
    }
}