using UnityEngine;
[System.Serializable]
public class DeathZoneComponent : MonoBehaviour
{
    [Header("Death Zone Settings")]
    [Tooltip("�������ͧ⫹���")]
    public PlayerDeathSystem.DeathType deathType = PlayerDeathSystem.DeathType.InstantDeath;

    [Header("Visual Settings")]
    public bool showGizmo = true;
    public Color gizmoColor = Color.red;
    [Header("Behaviour")]
    [Tooltip("If true: when any object collides with this DeathZone it will be destroyed (the DeathZone object), otherwise it will kill the player as normal.")]
    public bool destroyOnAnyHit = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // If configured to self-destruct on any hit, do so and skip player death behavior
        if (destroyOnAnyHit)
        {
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("Player"))
        {
            PlayerDeathSystem playerDeath = other.GetComponent<PlayerDeathSystem>();
            if (playerDeath != null)
            {
                playerDeath.Die(deathType);
            }
            else
            {
                Debug.LogWarning("Player missing PlayerDeathSystem component!");
            }
        }
   

    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // If configured to self-destruct on any hit, do so and skip player death behavior
        if (destroyOnAnyHit)
        {
            Destroy(gameObject);
            return;
        }

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