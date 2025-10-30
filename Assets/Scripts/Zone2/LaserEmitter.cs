using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LaserEmitter : MonoBehaviour
{
    [Header("Laser Settings")]
    [Tooltip("Layer ทั้งหมดที่เลเซอร์จะ 'หยุด' เมื่อชน (เช่น Ground, Wall, LaserBlocker)")]
    public LayerMask collisionLayers;
    [Tooltip("Layer ที่เลเซอร์จะ 'ทะลุผ่าน' และ 'สร้างความเสียหาย' (เช่น Player, Destroyable)")]
    public LayerMask damageLayers;
    public float laserMaxLength = 100f;
    public float laserWidth = 0.1f;

    [Header("Damage Settings")]
    public PlayerDeathSystem.DeathType deathType = PlayerDeathSystem.DeathType.InstantDeath;

    [Header("Effects (Optional)")]
    public GameObject laserHitWallEffect;

    // เราไม่ต้องการ destructionEffect ที่นี่แล้ว เพราะ CraftedObject จะจัดการเอง
    // public GameObject destructionEffect; 

    private LineRenderer lineRenderer;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        if (laserHitWallEffect != null) laserHitWallEffect.SetActive(false);
    }

    void Update()
    {
        // --- 1. ยิงเลเซอร์ "ที่มองเห็น" (เหมือนเดิม) ---
        Vector2 startPos = transform.position;
        Vector2 direction = transform.up;
        Vector2 visualEndPoint;

        RaycastHit2D visualHit = Physics2D.Raycast(startPos, direction, laserMaxLength, collisionLayers);

        if (visualHit.collider != null)
        {
            visualEndPoint = visualHit.point;
            if (laserHitWallEffect != null)
            {
                laserHitWallEffect.transform.position = visualEndPoint;
                laserHitWallEffect.SetActive(true);
            }
        }
        else
        {
            visualEndPoint = startPos + (direction * laserMaxLength);
            if (laserHitWallEffect != null)
            {
                laserHitWallEffect.SetActive(false);
            }
        }

        // --- 2. อัปเดตเส้น LineRenderer (เหมือนเดิม) ---
        lineRenderer.startWidth = laserWidth;
        lineRenderer.endWidth = laserWidth;
        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, visualEndPoint);

        // --- 3. ยิงเลเซอร์ "ทำลายล้าง" (DeadZone) ---
        PerformDamageRaycast(startPos, direction, Vector2.Distance(startPos, visualEndPoint));
    }

    /// <summary>
    /// ยิง "ลำแสงแบบหนา" (CapsuleCastAll) เพื่อตรวจจับและสร้างความเสียหาย
    /// </summary>
    private void PerformDamageRaycast(Vector2 start, Vector2 direction, float distance)
    {
        RaycastHit2D[] hits = Physics2D.CapsuleCastAll(start, new Vector2(laserWidth, laserWidth), CapsuleDirection2D.Vertical, 0f, direction, distance, damageLayers);

        foreach (var hit in hits)
        {
            // A. ถ้าโดนผู้เล่น (Player)
            if (hit.collider.CompareTag("Player"))
            {
                PlayerDeathSystem deathScript = hit.collider.GetComponent<PlayerDeathSystem>();
                if (deathScript != null)
                {
                    deathScript.Die(deathType);
                }
            }
            // B. ถ้าโดนกล่องที่ทำลายได้ (Destroyable)
            else
            {
                // ▼▼▼ "หัวใจ" ของการอัปเกรดอยู่ตรงนี้ ▼▼▼

                // 1. ลองหาสคริปต์ CraftedObject
                CraftedObject craftedObj = hit.collider.GetComponent<CraftedObject>();

                if (craftedObj != null)
                {
                    // 2. ถ้าเจอ -> สั่งให้มัน "กลับไปเป็นขยะ"
                    // (ซึ่ง CraftedObject.cs จะจัดการเรื่องเอฟเฟกต์และทำลายตัวเอง)
                    craftedObj.ReturnToTrash();
                }
                else
                {
                    // 3. ถ้าไม่เจอ (เป็น Object ทั่วไป) -> ก็ทำลายทิ้งแบบธรรมดา
                    Destroy(hit.collider.gameObject);
                }
                // ▲▲▲ สิ้นสุดส่วนที่แก้ไข ▲▲▲
            }
        }
    }
}