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
    public LayerMask obstacleLayers; // สำหรับตรวจ LOS

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
        //// ถ้ามี target อยู่แล้ว → ตรวจสอบว่ายัง valid อยู่มั้ย
        //if (currentTarget != null)
        //{
        //    float dist = Vector3.Distance(transform.position, currentTarget.transform.position);

        //    // target ยังอยู่ใน layer ที่ตรวจจับและไม่ออกนอกระยะ
        //    if (((1 << currentTarget.layer) & detectionLayers) != 0 && dist <= detectionRange)
        //    {
        //        return currentTarget; // ยัง lock เป้าเดิมไว้
        //    }
        //    else
        //    {
        //        currentTarget = null; // หลุดระยะ → reset
        //    }
        //}

        //// ถ้าไม่มี target → หาตัวใหม่
        //Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRange, detectionLayers);
        //if (hits.Length > 0)
        //{
        //    // หาเป้าที่ใกล้ที่สุด
        //    Collider2D closest = null;
        //    float closestDist = Mathf.Infinity;

        //    foreach (var hit in hits)
        //    {
        //        float dist = Vector3.Distance(transform.position, hit.transform.position);
        //        if (dist < closestDist)
        //        {
        //            closestDist = dist;
        //            closest = hit;
        //        }
        //    }

        //    if (closest != null)
        //    {
        //        currentTarget = closest.gameObject;
        //        return currentTarget;
        //    }
        //}

        //return null; // ไม่มีเป้าในระยะ

        return currentTarget;
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

    protected bool HasLineOfSight(GameObject target)
    {
        if (target == null) return false;

        Vector2 direction = (target.transform.position - transform.position).normalized;
        float distance = Vector2.Distance(transform.position, target.transform.position);

        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, obstacleLayers);

        if (hit.collider != null)
        {
            // เจอกำแพงหรือสิ่งกีดขวาง → มองไม่เห็น
            // Debug.Log($"{enemyName} LOS blocked by {hit.collider.name}");
            return false;
        }

        return true;
    }

    protected virtual void Chase(GameObject target)
    {
        float directionX = target.transform.position.x - transform.position.x;
        directionX = Mathf.Sign(directionX); // +1 หรือ -1

        transform.position += Vector3.right * directionX * moveSpeed * Time.deltaTime;
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

    protected virtual void OnDrawGizmosSelected()
    {
        // วาดวงกลมแสดงระยะตรวจจับใน Scene View
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }

}
