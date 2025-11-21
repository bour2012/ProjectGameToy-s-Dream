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
    [Tooltip("ความเร็วในการบินตามผู้เล่น (สำหรับ FlyBoss State)")]
    public float moveSpeed = 5f;
    [Tooltip("ระยะที่ถือว่าตรงกับผู้เล่นแล้ว (สำหรับ FlyBoss State)")]
    public float alignmentThreshold = 0.5f;
    [SerializeField] private float maxBeamLength = 50f; // ความยาวสูงสุดของลำแสง
    [SerializeField] private LayerMask obstacleLayer; // Layer ของกำแพง/พื้น

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
    [Header("Side Beam (New) Settings")]
    public AttackData sideBeamAttackData;
    public enum SideBeamMode { TowardPlayer = 0, Left = 1, Right = 2, Both = 3, RandomLR = 4 }
    [Tooltip("Choose how the side beam selects direction.")]
    public SideBeamMode sideBeamMode = SideBeamMode.TowardPlayer;
    [SerializeField] private float sideWarningDuration = 0.8f;
    [SerializeField] private float sideBeamDuration = 1.8f;
    [SerializeField] private float sideBeamWidth = 1.6f;
    [Header("Fire Barrier Attack Settings")]
    // Barrier prefab + placement
    public GameObject bossFireBarrierPrefab;
    [Tooltip("Offset from boss where barrier will spawn (local space). Default spawns to the left.")]
    public Vector2 barrierSpawnOffset = new Vector2(-3f, 0f);
    [Tooltip("How long the spawned barrier remains in scene (seconds)")]
    public float barrierDuration = 6f;

    // Projectile configuration (uses projectile prefab from AttackData)
    public AttackData fireBarrierAttackData;
    [Tooltip("How many homing projectiles to spawn from the barrier")]
    public int fireProjectileCount = 1;
    [Tooltip("Max turning speed (deg/sec) when homing")]
    public float fireProjectileTurnSpeed = 360f;
    [Tooltip("Lifetime (seconds) before projectile auto-destroys regardless of distance")]
    public float fireProjectileLifetime = 5f;
    [Tooltip("Distance the projectile travels before being destroyed (world units)")]
    public float fireProjectileDestroyDistance = 30f;
    [Tooltip("Prefab for destroy effect spawned when projectile is destroyed")]
    public GameObject fireProjectileDestroyEffect;
    [Tooltip("Extra wait after all projectiles destroyed before the boss may continue attacks")]
    public float waitAfterProjectilesDestroyed = 0.5f;
    [Tooltip("If the barrier prefab provides a child Transform named 'SpawnPoint', projectiles will spawn there. Otherwise they spawn this far from the barrier's position along its right vector.")]
    public float barrierProjectileSpawnDistance = 0.5f;
    [Tooltip("If true, the boss will wait until the spawned barrier object is destroyed before finishing this attack. If false, it waits until projectiles are gone (old behavior).")]
    public bool waitForBarrierDestruction = true;
    [Header("Spawn & Homing Tuning")]
    [Tooltip("Random position jitter applied to each spawned projectile (world units)")]
    public float fireProjectileSpawnJitter = 0.2f;
    [Tooltip("Delay between spawning each projectile (seconds)")]
    public float fireProjectileSpawnStagger = 0.08f;
    [Tooltip("Fractional slow applied to projectile speed when doing large turns (0..1). 0 = no slow, 0.5 = up to 50% slower while turning)")]
    [Range(0f, 1f)] public float fireProjectileTurnSlowFactor = 0.25f;
    [Tooltip("How long (seconds) the projectile actively homes toward the player before flying straight")]
    public float fireProjectileHomingDuration = 1.2f;

    // Internal timer
    private float fireBarrierTimer;

    private Transform player;
    private bool isAttacking;
    private float spreadAttackTimer;
    private float beamAttackTimer;
    private float sideBeamAttackTimer;
    private GameObject currentWarning;
    private GameObject currentBeam;
    private GameObject lastSpawnedBarrier;
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
        if (sideBeamAttackTimer > 0) sideBeamAttackTimer -= Time.deltaTime;
        if (fireBarrierTimer > 0) fireBarrierTimer -= Time.deltaTime;
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
    // Animation Event entry for the new side-beam attack.
    // Note: the user requested the trigger name `FrireBeamSide` (keeps the exact string requested).
    public void TriggerBeamSideAttack()
    {
        if (CanUseSideBeamAttack())
        {
            StartCoroutine(SideBeamSequence());
        }
    }
    // Animation-event entry to spawn a fire barrier and fire homing projectile(s)
    public void TriggerFireBarrierAttack()
    {
        if (CanUseFireBarrierAttack())
        {
            StartCoroutine(FireBarrierSequence());
        }
    }
    public bool CanUseAttack(int attackIndex)
    {
        // map legacy indices:
        if (attackIndex == 1) return CanUseSpreadAttack();
        if (attackIndex == 2) return CanUseBeamAttack();
        if (attackIndex == 3) return CanUseSideBeamAttack();
        if (attackIndex == 4) return CanUseFireBarrierAttack();

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

    public bool CanUseSideBeamAttack()
    {
        return !isAttacking && sideBeamAttackTimer <= 0;
    }

    public bool CanUseFireBarrierAttack()
    {
        return !isAttacking && fireBarrierTimer <= 0 && bossFireBarrierPrefab != null && fireBarrierAttackData != null && fireBarrierAttackData.projectilePrefab != null;
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

    // New side-beam sequence (fires left/right as a straight beam)
    private IEnumerator SideBeamSequence()
    {
        isAttacking = true;

        if (player == null)
        {
            // still allow using player-less logic for Left/Right/Both
        }

        // determine which directions to fire
        var dirs = new System.Collections.Generic.List<Vector2>();
        switch (sideBeamMode)
        {
            case SideBeamMode.Left:
                dirs.Add(Vector2.left);
                break;
            case SideBeamMode.Right:
                dirs.Add(Vector2.right);
                break;
            case SideBeamMode.Both:
                dirs.Add(Vector2.left);
                dirs.Add(Vector2.right);
                break;
            case SideBeamMode.RandomLR:
                dirs.Add(Random.value < 0.5f ? Vector2.left : Vector2.right);
                break;
            case SideBeamMode.TowardPlayer:
            default:
                if (player != null)
                {
                    dirs.Add((player.position.x < transform.position.x) ? Vector2.left : Vector2.right);
                }
                else
                {
                    dirs.Add(Vector2.right);
                }
                break;
        }

        // Phase: show warnings for each direction
        var warnings = new System.Collections.Generic.List<GameObject>();
        foreach (var d in dirs)
        {
            float len = CalculateSideBeamLength(d);
            var warnPrefab = sideBeamAttackData.warningEffectPrefab != null ? sideBeamAttackData.warningEffectPrefab : beamAttackData.warningEffectPrefab;
            if (warnPrefab != null)
            {
                Quaternion rot = (d == Vector2.right) ? Quaternion.identity : Quaternion.Euler(0, 0, 180f);
                var w = Instantiate(warnPrefab, transform.position, rot);
                if (w != null)
                {
                    w.transform.localScale = new Vector3(len, sideBeamWidth, 1);
                    w.transform.position = transform.position + (Vector3)(d * (len / 2f));
                    warnings.Add(w);
                }
            }
        }

        yield return new WaitForSeconds(sideWarningDuration);

        // remove warnings
        foreach (var w in warnings) if (w != null) Destroy(w);

        // Phase: create beams
        var beams = new System.Collections.Generic.List<GameObject>();
        foreach (var d in dirs)
        {
            float len = CalculateSideBeamLength(d);
            var prefab = sideBeamAttackData.projectilePrefab != null ? sideBeamAttackData.projectilePrefab : beamAttackData.projectilePrefab;
            Quaternion rot = (d == Vector2.right) ? Quaternion.identity : Quaternion.Euler(0, 0, 180f);
            var b = Instantiate(prefab, transform.position, rot);
            if (b != null)
            {
                b.transform.localScale = new Vector3(len, sideBeamWidth, 1);
                b.transform.position = transform.position + (Vector3)(d * (len / 2f));

                if (b.TryGetComponent<EnemyProjectile>(out var projectileComponent))
                {
                    projectileComponent.damage = Mathf.RoundToInt(sideBeamAttackData.damage);
                }

                beams.Add(b);
            }
        }

        yield return new WaitForSeconds(sideBeamDuration);

        // cleanup
        foreach (var b in beams) if (b != null) Destroy(b);

        // apply phase multiplier to cooldown
        float multiplier = 1f;
        var controller = GetComponent<BossController>();
        if (controller != null && controller.phases != null && controller.phases.Length > controller.currentPhaseIndex)
            multiplier = controller.phases[controller.currentPhaseIndex].attackSpeedMultiplier;

        sideBeamAttackTimer = sideBeamAttackData.cooldown / Mathf.Max(0.0001f, multiplier);
        isAttacking = false;
    }

    // Fire Barrier: spawn barrier on left, then fire homing projectile(s) from it
    private IEnumerator FireBarrierSequence()
    {
        isAttacking = true;

        // spawn barrier at left offset (relative to boss)
        Vector3 spawnPos = transform.position + (Vector3)barrierSpawnOffset;
        if (bossFireBarrierPrefab != null)
        {
            lastSpawnedBarrier = Instantiate(bossFireBarrierPrefab, spawnPos, Quaternion.identity);
            // auto-destroy barrier after duration
            if (barrierDuration > 0f)
                Destroy(lastSpawnedBarrier, barrierDuration);
        }

        // Determine projectile spawn base position from the barrier (prefer a child named 'SpawnPoint')
        Vector3 projSpawnBase = spawnPos;
        if (lastSpawnedBarrier != null)
        {
            var spawnChild = lastSpawnedBarrier.transform.Find("SpawnPoint");
            if (spawnChild != null)
                projSpawnBase = spawnChild.position;
            else
                projSpawnBase = lastSpawnedBarrier.transform.position + lastSpawnedBarrier.transform.right * barrierProjectileSpawnDistance;
        }

        // Spawn multiple homing projectiles from the barrier position
        var projPrefab = fireBarrierAttackData.projectilePrefab;
        if (projPrefab == null)
        {
            isAttacking = false;
            yield break;
        }

        var activeProjectiles = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < Mathf.Max(1, fireProjectileCount); i++)
        {
            // small offset to avoid exact overlap
            Vector3 offset = Vector3.zero;
            if (fireProjectileCount > 1)
                offset = new Vector3(0f, (i - (fireProjectileCount - 1) * 0.5f) * 0.2f, 0f);

            // apply a small random jitter so projectiles do not spawn exactly overlapping
            Vector2 jitter = Random.insideUnitCircle * fireProjectileSpawnJitter;
            GameObject proj = Instantiate(projPrefab, projSpawnBase + offset + (Vector3)jitter, Quaternion.identity);
            if (proj == null) continue;

            // set damage if EnemyProjectile exists
            if (proj.TryGetComponent<EnemyProjectile>(out var projComp))
            {
                projComp.damage = Mathf.RoundToInt(fireBarrierAttackData.damage);
            }

            // Immediately face the player so the projectile spawns oriented toward them
            if (player != null)
            {
                Vector3 toPlayer = (player.position - proj.transform.position);
                if (toPlayer.sqrMagnitude > 0.0001f)
                {
                    float desiredAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
                    proj.transform.rotation = Quaternion.Euler(0f, 0f, desiredAngle);

                    // If projectile has Rigidbody2D, give it an initial velocity toward player
                    if (proj.TryGetComponent<Rigidbody2D>(out var rbInit))
                    {
                        Vector3 forward = proj.transform.right;
                        rbInit.linearVelocity = forward * fireBarrierAttackData.projectileSpeed;
                    }
                }
            }

            activeProjectiles.Add(proj);

            // start homing movement coroutine for each projectile
            StartCoroutine(MoveHomingProjectile(proj, fireBarrierAttackData.projectileSpeed, fireProjectileDestroyDistance, fireProjectileLifetime, fireProjectileTurnSpeed));

            // stagger spawn slightly so they don't all appear at the exact same frame/position
            if (fireProjectileSpawnStagger > 0f)
                yield return new WaitForSeconds(fireProjectileSpawnStagger);
        }

        // Wait behavior: allow choosing whether to wait for the barrier to be destroyed
        if (waitForBarrierDestruction)
        {
            // If barrier was spawned, wait until it is destroyed (barrier may have longer duration than projectiles)
            if (lastSpawnedBarrier != null)
                yield return new WaitUntil(() => lastSpawnedBarrier == null);
        }
        else
        {
            // default: wait until all spawned projectiles have been destroyed
            yield return new WaitUntil(() =>
            {
                for (int i = activeProjectiles.Count - 1; i >= 0; i--)
                {
                    if (activeProjectiles[i] == null) activeProjectiles.RemoveAt(i);
                }
                return activeProjectiles.Count == 0;
            });
        }

        if (waitAfterProjectilesDestroyed > 0f) yield return new WaitForSeconds(waitAfterProjectilesDestroyed);

        // apply phase attack speed multiplier for cooldown
        float multiplier = 1f;
        var controller = GetComponent<BossController>();
        if (controller != null && controller.phases != null && controller.phases.Length > controller.currentPhaseIndex)
            multiplier = controller.phases[controller.currentPhaseIndex].attackSpeedMultiplier;

        fireBarrierTimer = fireBarrierAttackData.cooldown / Mathf.Max(0.0001f, multiplier);
        isAttacking = false;
    }

    // Move the projectile toward the player each frame; support smooth turning, lifetime, and destroy effect
    private IEnumerator MoveHomingProjectile(GameObject proj, float speed, float destroyDistance, float lifetime, float turnSpeedDegPerSec)
    {
        if (proj == null) yield break;
        Vector3 startPos = proj.transform.position;
        Rigidbody2D projRb = proj.GetComponent<Rigidbody2D>();
        float elapsed = 0f;
        float homingElapsed = 0f;

        while (proj != null)
        {
            elapsed += Time.deltaTime;

            // Determine whether we are still in the homing phase
            bool stillHoming = homingElapsed < fireProjectileHomingDuration;

            if (stillHoming)
            {
                homingElapsed += Time.deltaTime;

                Vector3 targetPos = (player != null) ? (Vector3)player.position : (startPos + Vector3.right * destroyDistance);
                Vector3 toTarget = (targetPos - proj.transform.position);
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    float desiredAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                    float currentAngle = proj.transform.rotation.eulerAngles.z;
                    float angleDiff = Mathf.DeltaAngle(currentAngle, desiredAngle);

                    // apply smooth turning limited by turnSpeedDegPerSec
                    float maxDelta = turnSpeedDegPerSec * Time.deltaTime;
                    float clampedDelta = Mathf.Clamp(angleDiff, -maxDelta, maxDelta);
                    float newAngle = currentAngle + clampedDelta;
                    proj.transform.rotation = Quaternion.Euler(0f, 0f, newAngle);

                    // forward direction: treat right (1,0) as forward for projectile
                    Vector3 forward = proj.transform.right;

                    // If projectile is turning sharply, slightly reduce forward speed
                    float absAngle = Mathf.Abs(angleDiff);
                    float turnFactor = Mathf.Clamp01(absAngle / 90f); // 0 at 0deg, 1 at 90deg or more
                    float speedMultiplier = 1f - (fireProjectileTurnSlowFactor * turnFactor);

                    if (projRb != null)
                    {
                        projRb.linearVelocity = forward * (speed * speedMultiplier);
                    }
                    else
                    {
                        proj.transform.position += forward * (speed * speedMultiplier) * Time.deltaTime;
                    }
                }
            }
            else
            {
                // Homing finished: continue straight along current forward direction
                Vector3 forward = proj.transform.right;
                if (projRb != null)
                {
                    projRb.linearVelocity = forward * speed;
                }
                else
                {
                    proj.transform.position += forward * speed * Time.deltaTime;
                }
            }

            // destroy by lifetime or by distance traveled
            if ((lifetime > 0f && elapsed >= lifetime) || Vector3.Distance(startPos, proj.transform.position) >= destroyDistance)
            {
                if (fireProjectileDestroyEffect != null)
                {
                    var fx = Instantiate(fireProjectileDestroyEffect, proj.transform.position, Quaternion.identity);
                    var ps = fx.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        float maxLifetime = (ps.main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants) ? ps.main.startLifetime.constantMax : ps.main.startLifetime.constant;
                        Destroy(fx, ps.main.duration + maxLifetime + 0.25f);
                    }
                    else Destroy(fx, 4f);
                }

                Destroy(proj);
                yield break;
            }

            yield return null;
        }
    }

    private float CalculateSideBeamLength(Vector2 direction)
    {
        Vector2 origin = transform.position;
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, maxBeamLength, obstacleLayer);
        if (hit.collider != null) return hit.distance;
        return maxBeamLength;
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
            // show side beams as well
            Gizmos.color = Color.cyan;
            float rightLen = CalculateSideBeamLength(Vector2.right);
            float leftLen = CalculateSideBeamLength(Vector2.left);
            Gizmos.DrawLine(origin, origin + Vector2.right * rightLen);
            Gizmos.DrawLine(origin, origin + Vector2.left * leftLen);
        }
    }
}