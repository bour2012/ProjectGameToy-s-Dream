using UnityEngine;

public class TestCollision : MonoBehaviour
{
    // àªç¤¡Ã³Õ Trigger
    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Player ¶Ù¡ Trigger ª¹â´Â: " + other.gameObject.name);
    }

    // àªç¤¡Ã³Õª¹á¢ç§
    void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log("Player ¶Ù¡ª¹(á¢ç§)â´Â: " + collision.gameObject.name);
    }
}