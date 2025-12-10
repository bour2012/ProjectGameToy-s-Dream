using UnityEngine;
using System.Collections;
using UnityEngine.Events;

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
    private float glueHitCooldown = 0.15f;

    [Header("Glue Accumulation")]
    [Tooltip("เวลา (วินาที) ที่การโจมตีกาวจะถูกสะสมก่อนจะรีเซ็ตเป็นศูนย์ ถ้าไม่มีการโดนกาวใหม่ในช่วงนี้")]
    public float glueAccumulationWindow = 3f;
    private float glueAccumulationTimer = 0f;
    private bool glueFullTriggered = false; // true เมื่อหลอดเต็มและ Fall ถูก triggered
    [Header("Fall / Grounding")]
    [Tooltip("Layer(s) used to detect ground contact when boss falls")]
    public LayerMask groundLayerMask;
    [Tooltip("gravityScale applied while boss is falling toward the ground")]
    public float fallGravityScale = 2f;
    [Tooltip("เวลาที่บอสจะนิ่งอยู่บนพื้น (วินาที) ก่อนรีเซ็ตกาวและกลับไปบินได้อีกครั้ง")]
    public float fallGroundedDuration = 5f;
    private bool isGroundedFromFall = false;
    private Coroutine groundedCoroutine = null;

    //[Header("Combat")]
    //public GameObject projectilePrefab;
    //public Transform projectileSpawnPoint;
    //public float projectileSpeed = 8f;
    //public GameObject minionPrefab;
    //public Transform[] minionSpawnPoints;

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

    [Header("Stomp")]
    [Tooltip("Damage applied to the boss when the player stomps its head while it's falling")]
    public float stompDamage = 10f;

    [Header("Stomp Indicator")]
    [Tooltip("Prefab shown above the boss to indicate the player should stomp its head while it's falling")]
    public GameObject stompIndicatorPrefab;
    [Tooltip("Local offset from the boss transform where the indicator will appear")]
    public Vector3 stompIndicatorOffset = new Vector3(0f, 1.6f, 0f);
    [Tooltip("Vertical bob amplitude for the indicator (world units)")]
    public float stompIndicatorBobAmplitude = 0.12f;
    [Tooltip("Vertical bob frequency for the indicator (cycles per second)")]
    public float stompIndicatorBobFrequency = 2f;

    // internal indicator instance
    private GameObject stompIndicatorInstance;
    private float stompIndicatorTimer = 0f;

    [Header("Final Phase Grounded")]
    [Tooltip("When true, entering the final phase will put the boss into a grounded 'glue-locked' state: glue will no longer accumulate/reset and stomping is disabled.")]
    public bool enableFinalPhaseGroundedBehavior = true;

    // internal flag to track final-phase grounded state
    private bool finalPhaseGroundedActive = false;

    [Header("Level Transition")]
    [Tooltip("Delay (seconds) after glue meter fills before triggering level transition. Use to allow dialog to finish.")]
    public float levelTransitionDelay = 3f;
    [Tooltip("If assigned, Boss will trigger this DialogTrigger and wait until it completes before transitioning levels.")]
    public DialogTrigger transitionDialogTrigger;

    [Tooltip("If true and a DialogTrigger is assigned, wait for the dialog to finish instead of using the numeric delay.")]
    public bool waitForDialogCompletion = true;

    // Public properties for State Machine
    [HideInInspector] public bool isCurrentlyFalling = false;
    [HideInInspector] public bool isInvincible = false;
    [HideInInspector] public Vector2 targetPosition;
    [HideInInspector] public float fallDuration = 3f;
    [HideInInspector] public float damageInvincibilityTime = 1f;

    private AudioSource audioSource;
    private SpriteRenderer bossSprite;
    // Cached reference to LevelManager for performance (avoid repeated Find calls)
    private LevelManager levelManager;
    private BossPhaseData currentPhaseData;
    // Position lock while accumulating glue to avoid being displaced by projectile collisions
    private Vector2 lastFixedPosition;
    [Tooltip("ถ้า true ขณะสะสมกาว จะล็อกตำแหน่งทางฟิสิกส์ไม่ให้ถูกดัน")]
    public bool enforcePositionLockWhileAccumulating = true;

    // Remember starting position so boss can return after falling
    public Vector2 startingPosition;
    [Header("Return Settings")]
    [Tooltip("ความเร็วที่บอสบินกลับตำแหน่งเริ่มต้น (หน่วย Unity units/second)")]
    public float returnToStartSpeed = 3f;
    private bool isReturningToStart = false;

    [System.Serializable]
    public class BossPhaseData
    {
        public string phaseName = "Phase 1";
        [Header("Health Range (0..1)")]
        [Range(0f, 1f)] public float minHealthPercent = 0f; // inclusive
        [Range(0f, 1f)] public float maxHealthPercent = 1f; // inclusive

        [Header("Phase Tuning")]
        public int requiredGlueHits = 5;
        public float moveSpeedMultiplier = 1f;
        public float attackSpeedMultiplier = 1f; // >1 => faster (cooldowns shorter)
        [Tooltip("If true the boss will fly to follow the player's X position during flight; if false the boss will stay at its start X (but can still perform attacks).")]
        public bool followPlayer = true;
        [Header("Optional Warp On Phase Enter")]
        [Tooltip("If true the boss will teleport to 'warpLocation' when entering this phase.")]
        public bool warpOnEnter = false;
        [Tooltip("Optional Transform to teleport the boss to when entering this phase. Assign a marker GameObject in the scene.")]
        public Transform warpLocation;
        [Tooltip("Optional prefab (particle/smoke/etc.) to spawn when warping; will be instantiated at the warp location.")]
        public GameObject warpEffectPrefab;
        [Tooltip("Optional animator trigger name to play before warping. Leave empty to skip animation.")]
        public string warpAnimationTrigger = "PhaseWarp";
        [Tooltip("Delay (seconds) to wait after playing the warp animation trigger before performing the teleport.")]
        public float warpPreDelay = 0.5f;
        public Color phaseColor = Color.white;
    }

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.gravityScale = 0;
        audioSource = GetComponent<AudioSource>();
        bossSprite = GetComponentInChildren<SpriteRenderer>();

        if (phases == null || phases.Length == 0)
        {
            phases = new BossPhaseData[2];
            phases[0] = new BossPhaseData { phaseName = "Phase 1", minHealthPercent = 0.5f, maxHealthPercent = 1f, requiredGlueHits = 5 };
            phases[1] = new BossPhaseData { phaseName = "Phase 2", minHealthPercent = 0f, maxHealthPercent = 0.5f, requiredGlueHits = 7 };
        }
    }

    protected override void Start()
    {
        base.Start();

        currentTarget = GameObject.FindGameObjectWithTag("Player");

        // cache starting position (use rb if available)
        if (rb != null) startingPosition = rb.position; else startingPosition = transform.position;

        // set default phase mapping if phases defined but ranges not configured
        if (phases != null && phases.Length > 0)
        {
            // If phases don't have sensible min/max ranges yet, set defaults: Phase1=0.6-1.0, Phase2=0.25-0.6, Phase3=0-0.25
            bool needsDefaults = true;
            foreach (var p in phases) if (p.minHealthPercent != 0f || p.maxHealthPercent != 1f) { needsDefaults = false; break; }
            if (needsDefaults && phases.Length >= 3)
            {
                // user requested: Phase1 = 60-100%, Phase2 = 25-60%, Phase3 = 0-25%
                phases[0].minHealthPercent = 0.6f; phases[0].maxHealthPercent = 1f;
                phases[1].minHealthPercent = 0.25f; phases[1].maxHealthPercent = 0.6f;
                phases[2].minHealthPercent = 0f; phases[2].maxHealthPercent = 0.25f;
            }
        }

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

        // Cache LevelManager instance to avoid repeated FindFirstObjectByType calls
        levelManager = FindFirstObjectByType<LevelManager>();
        if (levelManager == null && showDebugLogs) Debug.LogWarning($"[{bossName}] LevelManager not found in scene (BossController cached reference)");

        EnterPhase(0);
    }

    protected override void Update()
    {
        base.Update();
        UpdateSpeedFromGlue();
        CheckPhaseTransition();
        // Update stomp indicator when boss is falling
        if (isCurrentlyFalling)
        {
            if (stompIndicatorInstance == null && stompIndicatorPrefab != null)
            {
                stompIndicatorInstance = Instantiate(stompIndicatorPrefab, transform.position + stompIndicatorOffset, Quaternion.identity);
            }

            if (stompIndicatorInstance != null)
            {
                stompIndicatorTimer += Time.deltaTime;
                float bob = Mathf.Sin(stompIndicatorTimer * stompIndicatorBobFrequency * Mathf.PI * 2f) * stompIndicatorBobAmplitude;
                stompIndicatorInstance.transform.position = transform.position + stompIndicatorOffset + new Vector3(0f, bob, 0f);
            }
        }
        else
        {
            if (stompIndicatorInstance != null)
            {
                Destroy(stompIndicatorInstance);
                stompIndicatorInstance = null;
                stompIndicatorTimer = 0f;
            }
        }

        // decay glue accumulation timer
        if (!glueFullTriggered && currentGlueHitCount > 0)
        {
            glueAccumulationTimer -= Time.deltaTime;
            if (glueAccumulationTimer <= 0f)
            {
                if (showDebugLogs) Debug.Log($"[{bossName}] Glue accumulation timed out.");
                // ResetGlueAccumulation();
                // NOTE: glue reset is now performed by LevelManager during level transitions
            }
        }
    }

    void FixedUpdate()
    {
        if (rb != null)
            lastFixedPosition = rb.position;

        // If recovering, move smoothly back to starting position
        if (isReturningToStart && rb != null)
        {
            Vector2 current = rb.position;
            Vector2 next = Vector2.MoveTowards(current, startingPosition, returnToStartSpeed * Time.fixedDeltaTime);
            rb.MovePosition(next);

            // arrived
            if (Vector2.Distance(next, startingPosition) < 0.05f)
            {
                isReturningToStart = false;
                rb.linearVelocity = Vector2.zero;
                // ensure flight physics restored
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f;
                if (showDebugLogs) Debug.Log($"[{bossName}] Returned to starting position");
            }
        }
    }

    void UpdateSpeedFromGlue()
    {
        if (isCurrentlyFalling) return;

        BossPhaseData currentPhase = phases[currentPhaseIndex];
        float glueProgress = (float)currentGlueHitCount / Mathf.Max(1, currentPhase.requiredGlueHits);
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

        // find phase where healthPercent is within [min,max]
        for (int i = 0; i < phases.Length; i++)
        {
            var p = phases[i];
            if (healthPercent >= p.minHealthPercent && healthPercent <= p.maxHealthPercent)
            {
                if (i != currentPhaseIndex)
                {
                    if (animator != null)
                    {
                        animator.SetTrigger("PhaseTransition");
                    }
                    EnterPhase(i);
                }
                break;
            }
        }
    }

    public void EnterPhase(int phaseIndex)
    {
        currentPhaseIndex = phaseIndex;
        BossPhaseData phase = phases[phaseIndex];
        currentPhaseData = phase;
        // Handle glue reset behavior: in final phase we may want to keep glue and force grounded behavior
        finalPhaseGroundedActive = (enableFinalPhaseGroundedBehavior && phaseIndex == (phases != null ? phases.Length - 1 : -1));

        if (!finalPhaseGroundedActive)
        {
            // Reset glue on non-final phase enter
            currentGlueHitCount = 0;
            glueFullTriggered = false;
            glueAccumulationTimer = 0f;
        }
        else
        {
            // In final-phase grounded mode: ensure boss is grounded and will not respond to glue/stomps
            isCurrentlyFalling = false;
            isGroundedFromFall = true;
            // freeze physics on ground so boss stays in place
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
            if (animator != null)
            {
                try { animator.SetBool("Fall", false); } catch { }
            }
        }
        currentSpeedMultiplier = 1f;

        if (glueMeter != null)
        {
            glueMeter.SetMaxGlue(phase.requiredGlueHits);
            glueMeter.SetCurrentGlue(0);
        }

        if (showDebugLogs)
            Debug.Log($"[{bossName}] Entered {phase.phaseName}");

        // Apply multipliers to animator and notify other systems
        if (animator != null)
        {
            animator.SetInteger("Phase", phaseIndex);
            animator.speed = phase.moveSpeedMultiplier; // optional: scale base animation speed
        }

        // Optional: warp (instant teleport) when entering this phase
        if (phase.warpOnEnter && phase.warpLocation != null)
        {
            // If an animation trigger or a pre-delay is specified, play the animation then perform warp after delay
            if (animator != null && (!string.IsNullOrEmpty(phase.warpAnimationTrigger) || phase.warpPreDelay > 0f))
            {
                if (!string.IsNullOrEmpty(phase.warpAnimationTrigger))
                {
                    try { animator.SetTrigger(phase.warpAnimationTrigger); } catch { }
                }
                StartCoroutine(PerformWarpSequence(phase));
            }
            else
            {
                // immediate warp
                DoWarp(phase);
            }
        }

        var ai = GetComponent<BossAttackAI>();
        if (ai != null) ai.OnPhaseChanged(phaseIndex);

        var passive = GetComponent<BossPhasePassiveBehaviors>();
        if (passive != null) passive.OnPhaseChanged(phaseIndex);

        // Update startingPosition so the boss will use this point as its "home" when recovering from fall
        if (rb != null) startingPosition = rb.position; else startingPosition = transform.position;
    }

    //public void OnAnimationFinished()
    //{
    //    if (currentPhaseData != null)
    //        StartCoroutine(PerformWarpSequence(currentPhaseData));
    //}

    private void OnTriggerEnter2D(Collider2D other)
    {
        // glue projectile hits
        var gp = other.GetComponent<GlueProjectile>();
        if (gp != null)
        {
            // If final-phase grounded behavior is active, ignore glue hits entirely
            if (finalPhaseGroundedActive) return;

            // handle glue hit and prevent physics displacement while accumulating
            HandleGlueHit();

            if (enforcePositionLockWhileAccumulating && !glueFullTriggered && currentGlueHitCount > 0 && rb != null)
            {
                rb.position = lastFixedPosition;
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    void HandleGlueHit()
    {
        if (finalPhaseGroundedActive) return; // do nothing in final grounded phase
        if (isCurrentlyFalling) return; // when already falling, glue does nothing
        if (levelManager.IsTransitioning) return;
        if (Time.time - lastGlueHitTime < glueHitCooldown) return;

        // If a level transition is active, ignore glue hits until transition completes.
        if (levelManager == null)
        {
            // fallback - try to find and cache
            levelManager = FindFirstObjectByType<LevelManager>();
        }
        if (levelManager != null && levelManager.IsTransitioning)
        {
            if (showDebugLogs) Debug.Log($"[{bossName}] Ignoring glue hit while level transition in progress.");
            return;
        }

        lastGlueHitTime = Time.time;

        // Start or refresh accumulation window
        glueAccumulationTimer = glueAccumulationWindow;

        currentGlueHitCount++;

        if (showDebugLogs)
            Debug.Log($"[{bossName}] Glue hit! {currentGlueHitCount}/{phases[currentPhaseIndex].requiredGlueHits} (timer {glueAccumulationTimer:F1}s)");

        if (glueMeter != null)
            glueMeter.SetCurrentGlue(currentGlueHitCount);

        // if reached required count -> schedule level transition (no fall)
        if (!glueFullTriggered && currentGlueHitCount >= phases[currentPhaseIndex].requiredGlueHits)
        {
            glueFullTriggered = true;

            // Move animator to idle state (do not trigger a fall)
            if (animator != null)
            {
                try { animator.SetTrigger("GoToIdle"); } catch { }
                try { animator.ResetTrigger("FlyToAttack"); } catch { }
            }

            // Optionally play a sound to indicate glue full
            PlaySound(fallSound);

            // Reset and pause the attack AI to prevent it remembering/continuing attacks
            var ai = GetComponent<BossAttackAI>();
            if (ai != null)
            {
                ai.ResetAIState();
                ai.PauseForSummons(true);
            }

            if (showDebugLogs) Debug.Log($"[{bossName}] Glue full -> scheduling level transition in {levelTransitionDelay:F1}s");

            levelManager.MonitorBossGlueMeter();
            // Start delayed level transition so dialog or other actions can complete first
            //levelTriggerLevelTransition();
            //StartCoroutine(DelayedLevelTransition());
        }
    }

    /// <summary>
    /// Called externally when the fall sequence completes (Animation Event or SMB should call this).
    /// Resets glue state so boss can resume normal behaviour.
    /// </summary>
    public void OnFallComplete()
    {
        // If called manually, cancel any grounded coroutine and recover immediately
        if (groundedCoroutine != null)
        {
            StopCoroutine(groundedCoroutine);
            groundedCoroutine = null;
        }

        //RecoverFromFall();
    }

    //private IEnumerator TemporarilyInvincible(float duration)
    //{
    //    isInvincible = true;
    //    yield return new WaitForSeconds(duration);
    //    isInvincible = false;
    //}

    // Called when boss lands on ground after falling
    private void LandedOnGround()
    {
        if (isGroundedFromFall) return;
        isGroundedFromFall = true;

        // mark vulnerable while grounded
        isCurrentlyFalling = true;

        // stop physics motion and freeze on ground
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic; // keep in place while grounded
        }

        // start grounded timer to recover
        groundedCoroutine = StartCoroutine(GroundedRoutine());
    }

    private System.Collections.IEnumerator DelayedLevelTransition()
    {
        // If configured to wait for dialog and a DialogTrigger is assigned, start it and wait for completion
        if (waitForDialogCompletion && transitionDialogTrigger != null)
        {
            bool completed = false;
            UnityAction onComplete = () => { completed = true; };

            // Attach listener
            transitionDialogTrigger.onDialogComplete.AddListener(onComplete);

            // Trigger the dialog sequence
            transitionDialogTrigger.TriggerDialog();

            // Wait until dialog triggers completion event
            while (!completed)
            {
                yield return null;
            }

            // Clean up listener
            transitionDialogTrigger.onDialogComplete.RemoveListener(onComplete);
        }
        else
        {
            // Wait the configured delay (allows dialog or other events to finish)
            float waited = 0f;
            while (waited < levelTransitionDelay)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }

        // Trigger LevelManager transition if present (use cached reference when possible)
        if (levelManager == null)
        {
            levelManager = FindFirstObjectByType<LevelManager>();
        }
        if (levelManager != null)
        {
            if (showDebugLogs) Debug.Log($"[{bossName}] Executing level transition now.");
            levelManager.TriggerLevelTransition();
        }
    }

    private IEnumerator GroundedRoutine()
    {
        // stay on ground for configured duration
        float t = 0f;
        while (t < fallGroundedDuration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        groundedCoroutine = null;
        //RecoverFromFall();
    }

    //private void RecoverFromFall()
    //{
    //    // boss recovers and returns to flying
    //    isCurrentlyFalling = false;
    //    isGroundedFromFall = false;

    //    // reset glue and effects
    //    //ResetGlueAccumulation(); // Disabled: glue reset should occur on level change via LevelManager

    //    // restore physics for flight
    //    if (rb != null)
    //    {
    //        animator.SetBool("Fall", false);
    //        rb.bodyType = RigidbodyType2D.Dynamic;
    //        rb.gravityScale = 0f;
    //        rb.linearVelocity = Vector2.zero;
    //        // start moving back to starting position smoothly
    //        isReturningToStart = true;
    //        if (showDebugLogs) Debug.Log($"[{bossName}] Recovering: returning to start {startingPosition}");
    //    }

    //    // Let animations / AI resume normally. Do not automatically trigger another animation here;
    //    // the Attack AI or Animator controller can decide next state.

    //    // Ensure AI is unpaused and reset so it can resume its attack loop when back in flight
    //    var ai = GetComponent<BossAttackAI>();
    //    if (ai != null)
    //    {
    //        ai.ResetAIState();
    //        ai.OnPhaseChanged(currentPhaseIndex); // re-select the correct sequence
    //        ai.PauseForSummons(false);
    //    }
    //    // ensure any stomp indicator is removed on recovery
    //    if (stompIndicatorInstance != null)
    //    {
    //        Destroy(stompIndicatorInstance);
    //        stompIndicatorInstance = null;
    //        stompIndicatorTimer = 0f;
    //    }

    //    if (showDebugLogs) Debug.Log($"[{bossName}] Recovered from fall and resumed flight");
    //}

    /// <summary>
    /// Reset glue accumulation and related UI/effects immediately.
    /// </summary>
    public void ResetGlueAccumulation()
    {
        currentGlueHitCount = 0;
        glueAccumulationTimer = 0f;
        glueFullTriggered = false;

        if (glueMeter != null)
            glueMeter.SetCurrentGlue(0);

        if (glueAccumulationEffect != null)
        {
            glueAccumulationEffect.Stop();
            glueAccumulationEffect.Clear();
        }

        if (showDebugLogs) Debug.Log($"[{bossName}] Glue accumulation reset");
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

        // If this damage dropped the boss into the next phase(s), immediately transition and recover
        int newPhaseIndex = currentPhaseIndex;
        float healthPercent = currentHealth / maxHealth;
        for (int i = 0; i < phases.Length; i++)
        {
            var p = phases[i];
            if (healthPercent >= p.minHealthPercent && healthPercent <= p.maxHealthPercent)
            {
                newPhaseIndex = i;
                break;
            }
        }

        if (newPhaseIndex != currentPhaseIndex && newPhaseIndex > currentPhaseIndex)
        {
            if (showDebugLogs) Debug.Log($"[{bossName}] Damage caused phase change -> EnterPhase({newPhaseIndex}) and immediate recover");
            // Enter new phase and immediately recover from fall (stop waiting on grounded timer)
            EnterPhase(newPhaseIndex);
            OnFallComplete();
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Apply damage coming from environmental sources (e.g. burning glue) that should
    /// affect the boss regardless of its falling state. This bypasses the "only damage
    /// when falling" guard used by player stomps and projectile hits.
    /// </summary>
    public void ReceiveEnvironmentalDamage(float damage)
    {
        if (isInvincible)
        {
            if (showDebugLogs) Debug.Log($"[{bossName}] Environmental damage blocked by invincibility");
            return;
        }

        currentHealth -= damage;
        PlaySound(hurtSound);

        if (healthBar != null)
        {
            healthBar.SetHealth(currentHealth);
        }

        if (showDebugLogs)
            Debug.Log($"[{bossName}] Took environmental damage {damage} HP: {currentHealth}/{maxHealth}");

        if (animator != null)
        {
            animator.SetTrigger("TakeDamage");
        }

        // Check for phase transition (same logic as TakeDamage)
        int newPhaseIndex = currentPhaseIndex;
        float healthPercent = currentHealth / maxHealth;
        for (int i = 0; i < phases.Length; i++)
        {
            var p = phases[i];
            if (healthPercent >= p.minHealthPercent && healthPercent <= p.maxHealthPercent)
            {
                newPhaseIndex = i;
                break;
            }
        }

        if (newPhaseIndex != currentPhaseIndex && newPhaseIndex > currentPhaseIndex)
        {
            if (showDebugLogs) Debug.Log($"[{bossName}] Environmental damage caused phase change -> EnterPhase({newPhaseIndex})");
            EnterPhase(newPhaseIndex);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // Boss-specific stomp behavior: only respond when boss is in falling/vulnerable state
    public override void OnStomped(PlayerMovement player)
    {
        if (player == null) return;

        // In final-phase grounded behavior stomps are disabled
        if (finalPhaseGroundedActive)
        {
            if (showDebugLogs) Debug.Log($"[{bossName}] Stomp ignored: final-phase grounded (stomps disabled)");
            return;
        }

        // Only allow stomping when boss is currently falling / vulnerable
        if (!isCurrentlyFalling)
        {
            if (showDebugLogs) Debug.Log($"[{bossName}] Stomp ignored: not falling");
            return;
        }

        // Bounce the player as usual
        player.BounceAfterStomp();

        // Apply damage via existing TakeDamage method (it already respects isCurrentlyFalling/isInvincible)
        if (showDebugLogs) Debug.Log($"[{bossName}] Stomped by player -> applying {stompDamage} damage");
        TakeDamage(stompDamage);
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

    IEnumerator PerformWarpSequence(BossPhaseData phase)
    {
        // Wait for specified pre-delay (allow animation to play)
        float wait = Mathf.Max(0f, phase.warpPreDelay);
        if (wait > 0f)
            yield return new WaitForSeconds(wait);

        DoWarp(phase);
    }

    void DoWarp(BossPhaseData phase)
    {
        Vector2 dest = phase.warpLocation.position;

        // Spawn warp effect at the boss's current position first (smoke-out)
        if (phase.warpEffectPrefab != null)
        {
            Vector2 oldPos = transform.position;
            GameObject fxOut = Instantiate(phase.warpEffectPrefab, oldPos, Quaternion.identity);
            var psOut = fxOut.GetComponent<ParticleSystem>();
            if (psOut != null)
            {
                var mainOut = psOut.main;
                float maxLifetimeOut = (mainOut.startLifetime.mode == ParticleSystemCurveMode.TwoConstants) ? mainOut.startLifetime.constantMax : mainOut.startLifetime.constant;
                float destroyAfterOut = mainOut.duration + maxLifetimeOut + 0.25f;
                Destroy(fxOut, destroyAfterOut);
            }
            else
            {
                Destroy(fxOut, 4f);
            }
        }

        // Teleport physics body and transform
        if (rb != null)
        {
            rb.position = dest;
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
        }
        transform.position = dest;
        // Cancel any return-in-progress
        isReturningToStart = false;

        // Spawn warp effect at destination (smoke-in)
        if (phase.warpEffectPrefab != null)
        {
            GameObject fx = Instantiate(phase.warpEffectPrefab, dest, Quaternion.identity);
            var ps = fx.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                float maxLifetime = (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants) ? main.startLifetime.constantMax : main.startLifetime.constant;
                float destroyAfter = main.duration + maxLifetime + 0.25f;
                Destroy(fx, destroyAfter);
            }
            else
            {
                Destroy(fx, 4f);
            }
        }

        // Update startingPosition so the boss will use this point as its "home" when recovering from fall
        if (rb != null) startingPosition = rb.position; else startingPosition = transform.position;
    }

    /// <summary>
    /// Called by BossPhasePassiveBehaviors (or other systems) to pause AI actions while summons/obstacles are active.
    /// </summary>
    public void PauseForSummons(bool pause)
    {
        var ai = GetComponent<BossAttackAI>();
        if (ai != null) ai.PauseForSummons(pause);
        if (animator != null) animator.SetBool("PauseActions", pause);
        if (showDebugLogs) Debug.Log($"[{bossName}] PauseForSummons = {pause}");
    }

    // Called when a collision happens - used to detect landing on ground after fall
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!glueFullTriggered) return;

        // check collision layer against groundLayerMask
        if ((groundLayerMask.value & (1 << collision.gameObject.layer)) != 0)
        {
            LandedOnGround();
        }
    }

    //public void SummonMinions(int count)
    //{
    //    if (minionPrefab == null || minionSpawnPoints == null) return;

    //    int spawned = 0;
    //    foreach (Transform spawnPoint in minionSpawnPoints)
    //    {
    //        if (spawned >= count) break;
    //        if (spawnPoint != null)
    //        {
    //            Instantiate(minionPrefab, spawnPoint.position, Quaternion.identity);
    //            spawned++;
    //        }
    //    }
    //}

    public Vector2 GetRandomFlightPosition()
    {
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