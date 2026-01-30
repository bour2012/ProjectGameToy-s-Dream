using UnityEngine;

[System.Serializable]
public class DeathZoneComponent : MonoBehaviour
{
    [Header("Death Zone Settings")]
    [Tooltip("ประเภทการตายของ Player")]
    public PlayerDeathSystem.DeathType deathType = PlayerDeathSystem.DeathType.InstantDeath;

    [Header("Self Destruct Settings (โหมดทำลายตัวเอง)")]
    [Tooltip("If true: when ANY object hits this, destroy this object.")]
    public bool destroyOnAnyHit = false;

    [Tooltip("เปิดใช้งานโหมดทำลายตัวเองเมื่อชนกับ Layer ที่กำหนด")]
    public bool destroyOnSpecificLayer = false;

    [Tooltip("เลือก Layer ที่เมื่อชนแล้ว Object นี้จะหายไป")]
    public LayerMask targetLayers;

    [Header("Visual Settings")]
    public bool showGizmo = true;
    public Color gizmoColor = Color.red;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // ตรวจสอบเงื่อนไขการทำลายตัวเอง (Self Destruct Check)
        if (CheckSelfDestruct(other.gameObject)) return;

        // Logic ฆ่าผู้เล่น (Player Kill Logic)
        if (other.CompareTag("Player"))
        {
            HandlePlayerDeath(other.gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // ตรวจสอบเงื่อนไขการทำลายตัวเอง (Self Destruct Check)
        if (CheckSelfDestruct(collision.gameObject)) return;

        // Logic ฆ่าผู้เล่น (Player Kill Logic)
        if (collision.gameObject.CompareTag("Player"))
        {
            HandlePlayerDeath(collision.gameObject);
        }
    }

    // ฟังก์ชันตรวจสอบว่าจะทำลายตัวเองหรือไม่
    private bool CheckSelfDestruct(GameObject hitObject)
    {
        // 1. กรณีชนอะไรก็หายหมด (destroyOnAnyHit)
        if (destroyOnAnyHit)
        {
            Destroy(gameObject);
            return true;
        }

        // 2. กรณีชนเฉพาะ Layer ที่เลือก (destroyOnSpecificLayer)
        if (destroyOnSpecificLayer)
        {
            // เช็คว่า Layer ของของที่มาชน อยู่ใน LayerMask ที่เราเลือกไว้หรือไม่ (Bitwise Operation)
            if (((1 << hitObject.layer) & targetLayers) != 0)
            {
                Destroy(gameObject);
                return true;
            }
        }

        return false;
    }

    // แยก Logic การฆ่าผู้เล่นออกมาเพื่อให้โค้ดสะอาดขึ้น
    private void HandlePlayerDeath(GameObject playerObj)
    {
        PlayerDeathSystem playerDeath = playerObj.GetComponent<PlayerDeathSystem>();
        if (playerDeath != null)
        {
            playerDeath.Die(deathType);
        }
        else
        {
            Debug.LogWarning("Player missing PlayerDeathSystem component!");
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