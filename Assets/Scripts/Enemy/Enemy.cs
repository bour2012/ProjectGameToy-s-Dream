// MovementBase.cs
using System.Collections;
using UnityEngine;

public abstract class Enemy : MonoBehaviour, ISlowable
{
    [Header("Basic Stats")]
    public string enemyName;
    public float maxHealth = 100f;
    public float moveSpeed = 2f;
    public float detectionRange = 3f;
    public LayerMask detectionLayers;

    protected float currentHealth;
    protected Vector3 initialPosition;
    protected bool isChasing;
    private float originalSpeed;
    private Coroutine slowRoutine;

    protected GameObject currentTarget;

    protected virtual void Awake()
    {
        originalSpeed = moveSpeed;
    }

    protected virtual void Start()
    {
        currentHealth = maxHealth;
        initialPosition = transform.position;
    }

    protected virtual void Update()
    {
        if (!isChasing) Patrol();
    }

    #region Slowing Glue Effect
    public void ApplySlow(float slowAmount, float duration)
    {
        if (slowRoutine != null) StopCoroutine(slowRoutine);
        slowRoutine = StartCoroutine(SlowRoutine(slowAmount, duration));
    }

    public void ApplyGradualSlow(float targetSlowAmount, float duration, float lerpTime)
    {
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
        if (currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.transform.position);

            // เช็คว่า target ยังอยู่ในเลเยอร์ที่ตรวจจับ
            if (((1 << currentTarget.layer) & detectionLayers) != 0 && dist <= detectionRange)
            {
                return currentTarget;
            }

        }

        // หาเป้าหมายใหม่ถ้ายังไม่มี
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRange, detectionLayers);
        if (hits.Length > 0)
        {
            currentTarget = hits[0].gameObject;
            return currentTarget;
        }
        return null;
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
        Destroy(gameObject);
    }

    protected virtual void Patrol() { }

    protected virtual void Chase(GameObject target)
    {
        float directionX = target.transform.position.x - transform.position.x;
        directionX = Mathf.Sign(directionX); // +1 หรือ -1

        transform.position += Vector3.right * directionX * moveSpeed * Time.deltaTime;
    }

    protected GameObject DetectTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRange, detectionLayers);
        return hits.Length > 0 ? hits[0].gameObject : null;
    }

    public void ResetToInitialState()
    {
        isChasing = false;
        // รีเซ็ตสถานะอื่น ๆ ตามต้องการ
    }

    protected virtual void OnDrawGizmosSelected()
    {
        // วาดวงกลมแสดงระยะตรวจจับใน Scene View
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }

}
