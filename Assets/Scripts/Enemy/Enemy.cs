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


    public virtual void ApplySlow(float slowAmount, float duration)
    {
        if (slowRoutine != null) StopCoroutine(slowRoutine);
        slowRoutine = StartCoroutine(SlowRoutine(slowAmount, duration));
    }

    private IEnumerator SlowRoutine(float slowAmount, float duration)
    {
        moveSpeed = originalSpeed * slowAmount;
        yield return new WaitForSeconds(duration);
        moveSpeed = originalSpeed;
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
