using UnityEngine;
[System.Serializable]
public class DeathZoneComponent : MonoBehaviour
{
    [Header("Death Zone Settings")]
    [Tooltip("ประเภทของโซนตาย")]
    public PlayerDeathSystem.DeathType deathType = PlayerDeathSystem.DeathType.InstantDeath;

    [Header("Visual Settings")]
    public bool showGizmo = true;
    public Color gizmoColor = Color.red;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerDeathSystem playerDeath = other.GetComponent<PlayerDeathSystem>();
            if (playerDeath != null)
            {
                playerDeath.Die(deathType);
            }
            else
            {
                Debug.LogWarning("Player ไม่มี PlayerDeathSystem component!");
            }
        }
   

    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerDeathSystem playerDeath = collision.gameObject.GetComponent<PlayerDeathSystem>();
            if (playerDeath != null)
            {
                playerDeath.Die(deathType);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;

        Gizmos.color = gizmoColor;

        // BoxCollider2D
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boxCollider.offset, boxCollider.size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);
            Gizmos.DrawCube(boxCollider.offset, boxCollider.size);
        }

        // CircleCollider2D
        CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
        if (circleCollider != null)
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position + (Vector3)circleCollider.offset, circleCollider.radius);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);
            Gizmos.DrawSphere(transform.position + (Vector3)circleCollider.offset, circleCollider.radius);
        }
    }
}