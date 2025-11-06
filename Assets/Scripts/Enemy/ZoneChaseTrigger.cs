using UnityEngine;
using System.Collections.Generic;

public class ZoneChaseTrigger : MonoBehaviour
{
    public List<FlyingEnemy> enemiesInZone; // ใส่ enemy ได้หลายตัวใน Inspector

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            foreach (FlyingEnemy enemy in enemiesInZone)
            {
                if (enemy != null)
                    enemy.OnPlayerEnterZone(other.gameObject);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            foreach (FlyingEnemy enemy in enemiesInZone)
            {
                if (enemy != null)
                    enemy.OnPlayerExitZone();
            }
        }
    }
}
