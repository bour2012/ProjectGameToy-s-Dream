// MovementBase.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public abstract class Enemy : MonoBehaviour, ISlowable
{
    [Header("Events")]
    public UnityEvent onSlowApplied;

    [Header("Head Collider")]
    public Collider2D headCollider; // Collider สำหรับหัวของศัตรู

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
    [Header("Basic Stats")]
    public string enemyName;
    public float maxHealth = 100f;
    public float moveSpeed = 2f;
    public float detectionRange = 3f;
    public LayerMask detectionLayers;
    public LayerMask obstacleLayers; // สำหรับตรวจ LOS

    protected float currentHealth;
    protected Vector3 initialPosition;
    protected bool isChasing;
    private float originalSpeed;
    private Coroutine slowRoutine;
    protected Rigidbody2D rb;
    protected bool isGrounded;
    protected GameObject currentTarget;
    protected SpriteRenderer spriteRenderer;


    protected virtual void Awake()
    {
        originalSpeed = moveSpeed;
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        animator = GetComponent<Animator>();
    }

    protected virtual void Start()
    {
        currentHealth = maxHealth;
        initialPosition = transform.position;
    }

    protected virtual void Update()
    {
        isGrounded = IsGrounded();

        if (!isChasing)
        {
            if (isGrounded)
            {
                Patrol();
            }
            else
            {
                StopHorizontalMovement();
            }
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

        // ค่อยๆ ลดความเร็ว
        while (elapsed < lerpTime)
        {
            moveSpeed = Mathf.Lerp(startSpeed, targetSpeed, elapsed / lerpTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
        moveSpeed = targetSpeed;

        yield return new WaitForSeconds(duration);

        // กลับสู่ความเร็วปกติ
        moveSpeed = originalSpeed;
    }
    #endregion

    protected GameObject DetectAndLockTarget()
    {
        // ถ้ามี target เดิมแล้ว → ตรวจสอบว่ายัง valid อยู่มั้ย
        if (currentTarget != null)
        {
            float dist = Vector2.Distance(transform.position, currentTarget.transform.position);

            // ยังอยู่ใน detection layer + ระยะ + มี LOS
            if (((1 << currentTarget.layer) & detectionLayers) != 0 &&
                dist <= detectionRange &&
                HasLineOfSight(currentTarget))
            {
                return currentTarget; // ล็อกเป้าเดิมไว้
            }
            else
            {
                currentTarget = null; // ไม่ valid แล้ว → ปล่อย
            }
        }

        // ถ้าไม่มี target → หาตัวใหม่
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRange, detectionLayers);

        foreach (var hit in hits)
        {
            GameObject candidate = hit.gameObject;

            // ตรวจสอบ LOS ก่อนล็อกเป้า
            if (HasLineOfSight(candidate))
            {
                currentTarget = candidate; // ล็อกเป้าใหม่
                return currentTarget;
            }
        }

        return null; // ไม่มีเป้า
    }

    public virtual void TakeDamage(float damage)
    {
        currentHealth -= damage;
        Debug.Log($"{enemyName} took {damage} damage! HP left: {currentHealth}");
        if (currentHealth <= 0) Die();
    }

    protected virtual void Die()
    {
        Debug.Log($"{enemyName} has been defeated!");
        this.enabled = false;
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
        {
            col.enabled = false;
        }
        if (TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
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

        if (hit.collider != null)
        {
            //// เจอกำแพงหรือสิ่งกีดขวาง → มองไม่เห็น
            //Debug.Log($"{enemyName} LOS blocked by {hit.collider.name}");
            return false;
        }

        return true; // ไม่มีสิ่งกีดขวาง → มองเห็น
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
        // ถ้ามี target เดิมแล้ว → ตรวจสอบว่ายัง valid อยู่มั้ย
        if (currentTarget != null)
        {
            float dist = Vector2.Distance(transform.position, currentTarget.transform.position);

            // ยังอยู่ใน detection layer + ระยะ + มี LOS
            if (((1 << currentTarget.layer) & detectionLayers) != 0 &&
                dist <= detectionRange &&
                HasLineOfSight(currentTarget))
            {
                return currentTarget; // ล็อกเป้าเดิมไว้
            }
            else
            {
                currentTarget = null; // ไม่ valid แล้ว → ปล่อย
            }
        }

        // ถ้าไม่มี target → หาตัวใหม่
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRange, detectionLayers);

        foreach (var hit in hits)
        {
            GameObject candidate = hit.gameObject;
            if (HasLineOfSight(candidate))
            {
                currentTarget = candidate; // ล็อกเป้าใหม่
                return currentTarget;
            }
        }

        return null; // ไม่มีเป้า
    }
    public void ResetToInitialState()
    {
        isChasing = false;
        // รีเซ็ตสถานะอื่น ๆ ตามต้องการ
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        //if (collision.collider.CompareTag("Player"))
        //{
        //    // ตรวจสอบว่าผู้เล่นชน Head Collider หรือไม่
        //    if (collision.otherCollider == headCollider)
        //    {
        //        OnStomped(collision.collider.GetComponent<PlayerMovement>());
        //    }
        //}
    }



    public void OnStomped(PlayerMovement player)
    {
        if (player != null)
        {
            Debug.Log($"{enemyName} was stomped!");
            player.BounceAfterStomp(); // ให้ผู้เล่นกระโดดใหม่
            Die(); // ทำลายศัตรู
        }
    }
    protected virtual void OnDrawGizmosSelected()
    {
        // วาดวงกลมแสดงระยะตรวจจับใน Scene View
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }

}
