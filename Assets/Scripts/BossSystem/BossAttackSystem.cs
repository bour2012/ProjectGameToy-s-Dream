using UnityEngine;
using System.Collections;

[System.Serializable]
public class AttackData
{
    public float cooldown = 2f;
    public float damage = 10f;
    public float projectileSpeed = 5f;
    public GameObject projectilePrefab;
    public GameObject warningEffectPrefab;
}

public class BossAttackSystem : MonoBehaviour
{
    [Header("Spread Attack Settings")]
    public AttackData spreadAttackData;
    [SerializeField] private int projectileCount = 8;
    [SerializeField] private float spreadAngle = 180f;
    [SerializeField] private float startAngle = -90f;

    [Header("Beam Attack Settings")]
    public AttackData beamAttackData;
    [SerializeField] private float warningDuration = 1.5f;
    [SerializeField] private float beamDuration = 2f;
    [SerializeField] private float beamWidth = 2f;
    [Tooltip("ความเร็วในการบินตามผู้เล่น (สำหรับ FlyBoss State)")]
    public float moveSpeed = 5f;
    [Tooltip("ระยะที่ถือว่าตรงกับผู้เล่นแล้ว (สำหรับ FlyBoss State)")]
    public float alignmentThreshold = 0.5f;
    [SerializeField] private float maxBeamLength = 50f; // ความยาวสูงสุดของลำแสง
    [SerializeField] private LayerMask obstacleLayer; // Layer ของกำแพง/พื้น

    private Transform player;
    private bool isAttacking;
    private float spreadAttackTimer;
    private float beamAttackTimer;
    private GameObject currentWarning;
    private GameObject currentBeam;
    private Vector3 originalPosition; // เก็บตำแหน่งเดิมของ Boss

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        originalPosition = transform.position;
    }

    private void Update()
    {
        // Update timers
        if (spreadAttackTimer > 0) spreadAttackTimer -= Time.deltaTime;
        if (beamAttackTimer > 0) beamAttackTimer -= Time.deltaTime;
    }

    // Animation Event 호출용 함수
    public void TriggerSpreadAttack()
    {
        if (CanUseSpreadAttack())
        {
            StartSpreadAttack();
        }
    }

    public void TriggerBeamAttack()
    {
        if (CanUseBeamAttack())
        {
            StartCoroutine(BeamAttackSequence());
        }
    }
    public bool CanUseAttack(int attackIndex)
    {
        // map legacy indices:
        if (attackIndex == 1) return CanUseSpreadAttack();
        if (attackIndex == 2) return CanUseBeamAttack();

        // For other indices, by default allow (or implement custom logic)
        // If you add more complex attacks, expand this method or override via subclass.
        return true;
    }
    public bool CanUseSpreadAttack()
    {
        return !isAttacking && spreadAttackTimer <= 0;
    }

    public bool CanUseBeamAttack()
    {
        return !isAttacking && beamAttackTimer <= 0;
    }

    private void StartSpreadAttack()
    {
        isAttacking = true;

        float angleStep = spreadAngle / (projectileCount - 1);
        float currentAngle = startAngle;

        for (int i = 0; i < projectileCount; i++)
        {
            Vector2 direction = Quaternion.Euler(0, 0, currentAngle) * Vector2.right;

            GameObject projectile = Instantiate(
                spreadAttackData.projectilePrefab,
                transform.position,
                Quaternion.Euler(0, 0, currentAngle)
            );

            if (projectile.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.linearVelocity = direction * spreadAttackData.projectileSpeed;
            }

            if (projectile.TryGetComponent<EnemyProjectile>(out var projectileComponent))
            {
                projectileComponent.damage = Mathf.RoundToInt(spreadAttackData.damage);
            }

            // ลบกระสุนอัตโนมัติหลังจากเวลาที่กำหนด
            Destroy(projectile, 2f);

            currentAngle += angleStep;
        }

        // apply phase attack speed multiplier if present
        float multiplier = 1f;
        var controller = GetComponent<BossController>();
        if (controller != null && controller.phases != null && controller.phases.Length > controller.currentPhaseIndex)
            multiplier = controller.phases[controller.currentPhaseIndex].attackSpeedMultiplier;

        spreadAttackTimer = spreadAttackData.cooldown / Mathf.Max(0.0001f, multiplier);
        isAttacking = false;
    }

    private IEnumerator BeamAttackSequence()
    {
        isAttacking = true;

        if (player == null)
        {
            isAttacking = false;
            yield break;
        }


        // Phase 2: แสดงเส้นเตือน
        float beamLength = CalculateBeamLength();

        currentWarning = Instantiate(
            beamAttackData.warningEffectPrefab,
            transform.position,
            Quaternion.Euler(0, 0, -90) // หันลงล่าง
        );

        // ปรับขนาดเส้นเตือนตามความยาวที่วัดได้
        if (currentWarning != null)
        {
            // <--- แก้ไขตรงนี้: สลับ beamLength กับ beamWidth
            currentWarning.transform.localScale = new Vector3(beamLength, beamWidth, 1);

            // ปรับตำแหน่งให้เส้นเริ่มจาก Boss
            currentWarning.transform.position = transform.position + Vector3.down * (beamLength / 2f);
        }

        yield return new WaitForSeconds(warningDuration);

        // ลบเส้นเตือน
        if (currentWarning != null)
        {
            Destroy(currentWarning);
        }

        // Phase 3: ยิงลำแสงจริง
        beamLength = CalculateBeamLength(); // คำนวณใหม่อีกครั้ง

        currentBeam = Instantiate(
            beamAttackData.projectilePrefab,
            transform.position,
            Quaternion.Euler(0, 0, -90) // หันลงล่าง
        );

        if (currentBeam != null)
        {
            // <--- แก้ไขตรงนี้: สลับ beamLength กับ beamWidth
            currentBeam.transform.localScale = new Vector3(beamLength, beamWidth, 1);

            // ปรับตำแหน่งให้ลำแสงเริ่มจาก Boss
            currentBeam.transform.position = transform.position + Vector3.down * (beamLength / 2f);

            if (currentBeam.TryGetComponent<EnemyProjectile>(out var projectileComponent))
            {
                projectileComponent.damage = Mathf.RoundToInt(beamAttackData.damage);
            }
        }

        yield return new WaitForSeconds(beamDuration);

        // Cleanup
        if (currentBeam != null)
        {
            Destroy(currentBeam);
        }

        // apply phase attack speed multiplier if present
        float multiplier = 1f;
        var controller = GetComponent<BossController>();
        if (controller != null && controller.phases != null && controller.phases.Length > controller.currentPhaseIndex)
            multiplier = controller.phases[controller.currentPhaseIndex].attackSpeedMultiplier;

        beamAttackTimer = beamAttackData.cooldown / Mathf.Max(0.0001f, multiplier);
        isAttacking = false;
    }

    // คำนวณความยาวของลำแสงจนกว่าจะชนสิ่งกีดขวาง
    private float CalculateBeamLength()
    {
        Vector2 origin = transform.position;
        Vector2 direction = Vector2.down;

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, maxBeamLength, obstacleLayer);

        if (hit.collider != null)
        {
            // ถ้าชนสิ่งกีดขวาง ใช้ระยะจนถึงจุดชน
            return hit.distance;
        }
        else
        {
            // ถ้าไม่ชนอะไร ใช้ความยาวสูงสุด
            return maxBeamLength;
        }
    }

    public void StopAllAttacks()
    {
        isAttacking = false;
        if (currentWarning != null) Destroy(currentWarning);
        if (currentBeam != null) Destroy(currentBeam);
        StopAllCoroutines();
    }

    private void OnDestroy()
    {
        StopAllAttacks();
    }

    // Debug: แสดงเส้น Raycast ใน Scene view
    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Vector2 origin = transform.position;
            float beamLength = CalculateBeamLength();
            Gizmos.DrawLine(origin, origin + Vector2.down * beamLength);
        }
    }
}