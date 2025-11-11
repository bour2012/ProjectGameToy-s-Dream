using UnityEngine;
using System.Collections;

// ========================================
// Boss Controller (Main Script)
// ========================================
public class BossController : Enemy
{
    [Header("Boss Identity")]
    public string bossName = "Sky Terror";

    [Header("Phase Management")]
    public BossPhaseData[] phases;
    [HideInInspector] public int currentPhaseIndex = 0;

    [Header("Glue System")]
    public int currentGlueHitCount = 0;
    [HideInInspector] public float currentSpeedMultiplier = 1f;
    public AnimationCurve glueSlowCurve = AnimationCurve.Linear(0, 1, 1, 0.3f);
    private float lastGlueHitTime = -999f;
    private float glueHitCooldown = 0.2f;

    [Header("Combat")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;
    public float projectileSpeed = 8f;
    public GameObject minionPrefab;
    public Transform[] minionSpawnPoints;

    [Header("Movement")]
    public float baseSpeed = 4f;
    public float attackRange = 10f;
    public float hoverHeight = 4f;
    public float flightRadius = 6f;
    public LayerMask obstacleLayer;

    [Header("UI References")]
    public BossHealthBar healthBar;
    public BossGlueMeter glueMeter;

    [Header("Visual Effects")]
    public ParticleSystem phaseTransitionEffect;
    public ParticleSystem glueAccumulationEffect;

    [Header("Audio")]
    public AudioClip phaseTransitionSound;
    public AudioClip attackSound;
    public AudioClip fallSound;
    public AudioClip hurtSound;

    [Header("Debug")]
    public bool showDebugLogs = true;

    // (ส่วน AI & Attack Logic ถูกย้ายออกไปแล้ว)

    // Public properties for State Machine
    [HideInInspector] public bool isCurrentlyFalling = false;
    [HideInInspector] public bool isInvincible = false;
    [HideInInspector] public Vector2 targetPosition;
    //[HideInInspector] public GameObject currentTarget; // ทำให้เป็น Public หรือ [HideInInspector]
    [HideInInspector] public float fallDuration = 3f;
    [HideInInspector] public float damageInvincibilityTime = 1f;

    private AudioSource audioSource;
    private SpriteRenderer bossSprite;

    [System.Serializable]
    public class BossPhaseData
    {
        public string phaseName = "Phase 1";
        [Range(0f, 1f)] public float healthThreshold = 0.5f;
        public int requiredGlueHits = 5;
        public float flyingSpeed = 3f;
        public float attackCooldown = 3f;
        public Color phaseColor = Color.white;
    }

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        audioSource = GetComponent<AudioSource>();
        bossSprite = GetComponentInChildren<SpriteRenderer>();

        if (phases == null || phases.Length == 0)
        {
            phases = new BossPhaseData[2];
            phases[0] = new BossPhaseData { phaseName = "Phase 1", healthThreshold = 0.5f, requiredGlueHits = 5 };
            phases[1] = new BossPhaseData { phaseName = "Phase 2", healthThreshold = 0f, requiredGlueHits = 7 };
        }
    }

    protected override void Start()
    {
        base.Start();

        // หา Player เก็บไว้ให้ MovementAI ใช้
        currentTarget = GameObject.FindGameObjectWithTag("Player");

        if (healthBar != null)
        {
            healthBar.SetMaxHealth(maxHealth);
            healthBar.SetBossName(bossName);
            healthBar.Show();
        }

        if (glueMeter != null)
        {
            glueMeter.SetMaxGlue(phases[currentPhaseIndex].requiredGlueHits);
            glueMeter.Show();
        }

        EnterPhase(0);
    }

    protected override void Update()
    {
        base.Update();
        UpdateSpeedFromGlue();
        CheckPhaseTransition();

        //// อัพเดท animator parameters (ยังคงจำเป็น)
        //if (animator != null)
        //{
        //    animator.SetFloat("SpeedMultiplier", currentSpeedMultiplier);
        //    animator.SetBool("IsFalling", isCurrentlyFalling);
        //    animator.SetBool("IsInvincible", isInvincible);
        //    animator.SetInteger("CurrentPhase", currentPhaseIndex);
        //    animator.SetInteger("GlueCount", currentGlueHitCount);
        //}

        // (AI Loop ทั้งหมดถูกลบออกจากส่วนนี้)
    }

    // (ฟังก์ชัน RunPhase1AttackLoop() ถูกลบออกไป)

    void UpdateSpeedFromGlue()
    {
        if (isCurrentlyFalling) return;

        BossPhaseData currentPhase = phases[currentPhaseIndex];
        float glueProgress = (float)currentGlueHitCount / currentPhase.requiredGlueHits;
        currentSpeedMultiplier = glueSlowCurve.Evaluate(glueProgress);

        if (animator != null)
        {
            animator.speed = currentSpeedMultiplier;
        }

        if (glueAccumulationEffect != null)
        {
            var emission = glueAccumulationEffect.emission;
            emission.rateOverTime = glueProgress * 20f;
        }
    }

    void CheckPhaseTransition()
    {
        float healthPercent = currentHealth / maxHealth;

        for (int i = phases.Length - 1; i > currentPhaseIndex; i--)
        {
            if (healthPercent <= phases[i].healthThreshold)
            {
                if (animator != null)
                {
                    animator.SetTrigger("PhaseTransition");
                }
                EnterPhase(i);
                break;
            }
        }
    }

    public void EnterPhase(int phaseIndex)
    {
        currentPhaseIndex = phaseIndex;
        BossPhaseData phase = phases[phaseIndex];

        currentGlueHitCount = 0;
        currentSpeedMultiplier = 1f;

        if (glueMeter != null)
        {
            glueMeter.SetMaxGlue(phase.requiredGlueHits);
            glueMeter.SetCurrentGlue(0);
        }

        if (showDebugLogs)
            Debug.Log($"[{bossName}] Entered {phase.phaseName}");

        // --- เพิ่มส่วนนี้ ---
        // แจ้ง AI ที่เกี่ยวข้องว่าเฟสเปลี่ยนแล้ว
        GetComponent<BossAttackAI>()?.OnPhaseChanged(phaseIndex);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<GlueProjectile>() != null)
        {
            HandleGlueHit();
        }
    }

    void HandleGlueHit()
    {
        if (isCurrentlyFalling) return;
        if (Time.time - lastGlueHitTime < glueHitCooldown) return;

        lastGlueHitTime = Time.time;
        currentGlueHitCount++;

        if (showDebugLogs)
            Debug.Log($"[{bossName}] Glue hit! {currentGlueHitCount}/{phases[currentPhaseIndex].requiredGlueHits}");

        if (glueMeter != null)
        {
            glueMeter.SetCurrentGlue(currentGlueHitCount);
        }

        if (currentGlueHitCount >= phases[currentPhaseIndex].requiredGlueHits)
        {
            if (animator != null)
            {
                animator.SetTrigger("Fall");
            }
        }
    }

    public override void TakeDamage(float damage)
    {
        if (!isCurrentlyFalling || isInvincible)
        {
            if (showDebugLogs)
                Debug.Log($"[{bossName}] Damage blocked");
            return;
        }

        currentHealth -= damage;
        PlaySound(hurtSound);

        if (healthBar != null)
        {
            healthBar.SetHealth(currentHealth);
        }

        if (showDebugLogs)
            Debug.Log($"[{bossName}] Took {damage} damage! HP: {currentHealth}/{maxHealth}");

        if (animator != null)
        {
            animator.SetTrigger("TakeDamage");
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected override void Die()
    {
        if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        if (healthBar != null) healthBar.Hide();
        if (glueMeter != null) glueMeter.Hide();

        if (showDebugLogs)
            Debug.Log($"[{bossName}] Defeated!");
    }

    public void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    public void SpawnProjectile(Vector2 direction)
    {
        if (projectilePrefab == null || projectileSpawnPoint == null) return;

        GameObject projectile = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.identity);

        if (projectile.TryGetComponent<Rigidbody2D>(out var projectileRb))
        {
            projectileRb.linearVelocity = direction * projectileSpeed;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        projectile.transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    public void SummonMinions(int count)
    {
        if (minionPrefab == null || minionSpawnPoints == null) return;

        int spawned = 0;
        foreach (Transform spawnPoint in minionSpawnPoints)
        {
            if (spawned >= count) break;
            if (spawnPoint != null)
            {
                Instantiate(minionPrefab, spawnPoint.position, Quaternion.identity);
                spawned++;
            }
        }
    }

    public Vector2 GetRandomFlightPosition()
    {
        // 'initialPosition' ควรมรดกมาจาก base class 'Enemy'
        Vector2 basePos = initialPosition;

        if (currentTarget != null)
        {
            basePos = currentTarget.transform.position;

            float randomX = Random.Range(-flightRadius, flightRadius);
            float targetY = basePos.y + Random.Range(hoverHeight * 0.7f, hoverHeight * 1.3f);
            return new Vector2(basePos.x + randomX, targetY);
        }
        else
        {
            float randomX = Random.Range(-flightRadius, flightRadius);
            return new Vector2(basePos.x + randomX, basePos.y);
        }
    }

    public override void ApplySlow(float slowAmount, float duration) { }
    public override void ApplyGradualSlow(float targetSlowAmount, float duration, float lerpTime) { }
    protected override void Patrol() { }
}