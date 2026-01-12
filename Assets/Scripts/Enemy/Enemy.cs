using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public abstract class Enemy : MonoBehaviour, ISlowable
{
    [Header("Events")]
    public UnityEvent onSlowApplied;

    [Header("Head Collider")]
    public Collider2D headCollider;

    [Header("Environment Checks")]
    [Tooltip("Layer ของพื้น (ต้องตั้งค่าในคลาสลูกด้วย)")]
    [SerializeField] protected LayerMask groundLayer;
    [Tooltip("ระยะยิง Raycast ลงพื้นเพื่อตรวจจับ")]
    [SerializeField] protected float groundCheckDistance = 1f;
    [Tooltip("ระยะยิง Raycast ด้านข้างเพื่อตรวจจับกำแพง")]
    [SerializeField] protected float wallCheckDistance = 0.5f;

    protected Animator animator;

    [Header("Effects")]
    [Tooltip("Prefab ของ Particle Effect ที่จะเล่นตอนตาย")]
    public GameObject deathEffectPrefab;
    [Tooltip("ระยะเวลาที่เอฟเฟกต์จะเล่นก่อนที่ Object จะถูกทำลาย (วินาที)")]
    public float deathDuration = 2f;

    // ▼▼▼ [เพิ่ม] ส่วนตั้งค่าเสียง ▼▼▼
    [Header("Audio")]
    [Tooltip("เสียงที่จะเล่นตอนตาย")]
    public AudioClip deathSound;
    [Range(0f, 1f)]
    public float deathSoundVolume = 1f;
    // ▲▲▲ [สิ้นสุดส่วนเพิ่ม] ▲▲▲

    [Header("Basic Stats")]
    public string enemyName;
    public float maxHealth = 100f;
    public float moveSpeed = 2f;
    public float detectionRange = 3f;
    public LayerMask detectionLayers;
    public LayerMask obstacleLayers;

    private Vector3 lastPosition;
    public float currentHealth;
    protected Vector3 initialPosition;
    protected bool isChasing;
    private float originalSpeed;
    private Coroutine slowRoutine;
    protected Rigidbody2D rb;
    protected bool isGrounded;
    protected GameObject currentTarget;
    protected SpriteRenderer spriteRenderer;

    protected const string ANIM_IS_MOVING = "IsMoving";
    protected const string ANIM_HIT = "Hit";
    protected const string ANIM_DIE = "Die";

    protected virtual void Awake()
    {
        originalSpeed = moveSpeed;
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
    }

    protected virtual void Start()
    {
        currentHealth = maxHealth;
        initialPosition = transform.position;
    }

    protected void HandleAnimation()
    {
        if (animator == null) return;

        bool isMoving = false;
        if (rb != null)
        {
            isMoving = Mathf.Abs(rb.linearVelocity.x) > 0.1f;
        }
        else
        {
            float actualSpeed = (transform.position - lastPosition).magnitude / Time.deltaTime;
            isMoving = actualSpeed > 0.1f;
            lastPosition = transform.position;
        }
        animator.SetBool(ANIM_IS_MOVING, isMoving);
    }

    protected virtual void Update()
    {
        isGrounded = IsGrounded();

        if (animator != null && rb != null)
        {
            float actualSpeed = (transform.position - lastPosition).magnitude / Time.deltaTime;
            bool isMoving = actualSpeed > 0.1f;
            animator.SetBool(ANIM_IS_MOVING, isMoving);
            lastPosition = transform.position;
        }

        if (!isChasing)
        {
            if (isGrounded) Patrol();
            else StopHorizontalMovement();
        }
    }

    protected virtual void StopHorizontalMovement()
    {
        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
    }

    protected virtual bool IsGrounded()
    {
        RaycastHit2D groundHit = Physics2D.Raycast(transform.position, Vector2.down, groundCheckDistance, groundLayer);
#if UNITY_EDITOR
        Debug.DrawRay(transform.position, Vector2.down * groundCheckDistance, groundHit.collider ? Color.magenta : Color.gray);
#endif
        return groundHit.collider != null;
    }

    protected virtual void FlipSprite(float moveDirection)
    {
        if (spriteRenderer != null && moveDirection != 0)
        {
            spriteRenderer.flipX = (moveDirection > 0);
        }
    }

    #region Slowing Glue Effect
    public virtual void ApplySlow(float slowAmount, float duration)
    {
        onSlowApplied.Invoke();
        if (slowRoutine != null) StopCoroutine(slowRoutine);
        slowRoutine = StartCoroutine(SlowRoutine(slowAmount, duration));
    }

    public virtual void ApplyGradualSlow(float targetSlowAmount, float duration, float lerpTime)
    {
        onSlowApplied.Invoke();
        if (slowRoutine != null) StopCoroutine(slowRoutine);
        slowRoutine = StartCoroutine(GradualSlowRoutine(targetSlowAmount, duration, lerpTime));
    }

    private IEnumerator SlowRoutine(float slowAmount, float duration)
    {
        moveSpeed = originalSpeed * slowAmount;
        yield return new WaitForSeconds(duration);
        moveSpeed = originalSpeed;
    }

    private IEnumerator GradualSlowRoutine(float targetSlowAmount, float duration, float lerpTime)
    {
        float startSpeed = moveSpeed;
        float targetSpeed = originalSpeed * targetSlowAmount;
        float elapsed = 0f;

        while (elapsed < lerpTime)
        {
            moveSpeed = Mathf.Lerp(startSpeed, targetSpeed, elapsed / lerpTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
        moveSpeed = targetSpeed;
        yield return new WaitForSeconds(duration);
        moveSpeed = originalSpeed;
    }
    #endregion

    protected virtual GameObject DetectAndLockTarget()
    {
        if (currentTarget != null)
        {
            float dist = Vector2.Distance(transform.position, currentTarget.transform.position);

            if (((1 << currentTarget.layer) & detectionLayers) != 0 &&
                dist <= detectionRange &&
                HasLineOfSight(currentTarget))
            {
                return currentTarget;
            }
            else
            {
                currentTarget = null;
            }
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRange, detectionLayers);
        foreach (var hit in hits)
        {
            GameObject candidate = hit.gameObject;
            if (HasLineOfSight(candidate))
            {
                currentTarget = candidate;
                return currentTarget;
            }
        }
        return null;
    }

    public virtual void TakeDamage(float damage)
    {
        currentHealth -= damage;
        if (animator != null) animator.SetTrigger(ANIM_HIT);
        Debug.Log($"{enemyName} took {damage} damage! HP left: {currentHealth}");
        if (currentHealth <= 0) Die();
    }

    protected virtual void Die()
    {
        Debug.Log($"{enemyName} has been defeated!");

        // 1. สั่งเล่น Animation ตาย
        if (animator != null) animator.SetTrigger(ANIM_DIE);

        // ▼▼▼ [เพิ่ม] เล่นเสียงตายที่ตำแหน่งนี้ ▼▼▼
        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position, deathSoundVolume);
        }

        // 2. ปิดระบบฟิสิกส์
        this.enabled = false;
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
        {
            col.enabled = false;
        }

        if (TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.angularVelocity = 0f;
        }

        // 3. เริ่มขั้นตอนการรอ Destroy
        StartCoroutine(DieSequence());
    }

    private IEnumerator DieSequence()
    {
        // รอเวลาให้ Animation เล่นจนจบ
        yield return new WaitForSeconds(deathDuration);

        // พอรอเสร็จ ค่อยเสก Effect
        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    protected virtual void Patrol() { }

    protected bool HasLineOfSight(GameObject target)
    {
        if (target == null) return false;
        Vector2 direction = (target.transform.position - transform.position).normalized;
        float distance = Vector2.Distance(transform.position, target.transform.position);
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, obstacleLayers);
        if (hit.collider != null) return false;
        return true;
    }

    protected virtual void Chase(GameObject target)
    {
        float directionX = target.transform.position.x - transform.position.x;
        float moveDirection = Mathf.Sign(directionX);
        FlipSprite(moveDirection);
        transform.position += Vector3.right * moveDirection * moveSpeed * Time.deltaTime;
    }

    protected GameObject DetectTarget()
    {
        // (Logic เดิม)
        if (currentTarget != null)
        {
            float dist = Vector2.Distance(transform.position, currentTarget.transform.position);
            if (((1 << currentTarget.layer) & detectionLayers) != 0 &&
                dist <= detectionRange &&
                HasLineOfSight(currentTarget))
            {
                return currentTarget;
            }
            else
            {
                currentTarget = null;
            }
        }
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRange, detectionLayers);
        foreach (var hit in hits)
        {
            GameObject candidate = hit.gameObject;
            if (HasLineOfSight(candidate))
            {
                currentTarget = candidate;
                return currentTarget;
            }
        }
        return null;
    }

    public void ResetToInitialState()
    {
        isChasing = false;
    }

    public virtual void OnStomped(PlayerMovement player)
    {
        if (player != null)
        {
            Debug.Log($"{enemyName} was stomped!");
            player.BounceAfterStomp();

            // [หมายเหตุ] ฟังก์ชัน Die() ด้านบนมีการเล่นเสียงแล้ว ดังนั้นเรียก Die() เฉยๆ ก็จะมีเสียงครับ
            Die();
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}