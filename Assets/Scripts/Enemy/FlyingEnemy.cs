using System.Collections;
using System.Security.Cryptography;
using UnityEngine;

public class FlyingEnemy : Enemy
{
    private enum State { Idle, Flying, Attacking, Falling, Returning }
    private State currentState = State.Idle;

    public enum ChaseMode { Normal, Zone }
    public ChaseMode chaseMode = ChaseMode.Normal;

    [Header("Glue Settings")]
    [Tooltip("จำนวนครั้งที่ต้องโดนกาวก่อนจะร่วง")]
    public int requiredGlueHitsToFall = 3;
    public float glueHitCooldown = 0.1f; // เวลาบล็อกนับเบิ้ล (วินาที)
    // สำหรับนับว่าโดนกาวแล้วกี่ครั้ง (รีเซ็ตได้เมื่อลอยอีกครั้ง)
    [HideInInspector]
    public int currentGlueHitCount = 0;
    //private float lastGlueHitTime = -10f;

    [Header("Flying Enemy Settings")]
    public float flyingSpeed = 3f;
    public float attackRange = 8f;
    public float attackCooldown = 3f;
    [Tooltip("ระยะการบินลาดตระเวนในแนวนอน (ซ้าย-ขวา) จากจุดเริ่มต้น")]
    public float flightPatrolRadius = 5f;
    [Tooltip("ความสูงที่บินเหนือผู้เล่น")]
    public float hoverHeight = 3f;
    [Tooltip("ความเร็วในการหมุนตัว (สำหรับ Smooth Movement)")]
    public float rotationSpeed = 5f;

    [Header("Wall Detection")]
    [Tooltip("ระยะในการตรวจจับกำแพง")]
    public float wallDetectionDistance = 2f;
    [Tooltip("Layer ของกำแพง/พื้น")]
    public LayerMask obstacleLayer;
    [Tooltip("จำนวนครั้งที่พยายามหาตำแหน่งใหม่ก่อนยอมแพ้")]
    public int maxPositionAttempts = 5;

    [Header("Attack Settings")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;
    [Tooltip("ความเร็วของกระสุน")]
    public float projectileSpeed = 10f;

    [Header("Loot Drop")]
    [Tooltip("(Optional) ไอเทมที่จะดรอปเมื่อตาย")]
    public GameObject dropItemPrefab;


    [Header("Debug")]
    public bool showDebugGizmos = true;
    private bool isPlayerInZone = false;
    private GameObject playerInZone;
    private bool canFallByGlue = false;
    private Vector2 flightTargetPosition;
    private float attackTimer;
    private bool isAttacking = false;
    private float currentSpeed; // สำหรับ Smooth acceleration

    protected override void Awake()
    {
        //base.Awake();
        //rb = GetComponent<Rigidbody2D>();
        //rb.gravityScale = 0;
        //currentSpeed = 0f;

        base.Awake();
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0;

        // สุ่ม speed, cooldown ตอนเกิด
        //flyingSpeed = Random.Range(2f, 4.5f);
        if (chaseMode == ChaseMode.Zone)
        {
            attackCooldown = Random.Range(1.0f, 3.0f);
            flightPatrolRadius = Random.Range(5f, 7f);
            //hoverHeight = Random.Range(1.5f, 4f);
            attackTimer = Random.Range(0f, attackCooldown);
        }
        currentSpeed = 0f;
    }

    protected override void Update()
    {
        switch (currentState)
        {
            case State.Idle:
                HandleIdleState();
                break;
            case State.Flying:
                HandleFlyingState();
                break;
            case State.Attacking:
                HandleAttackingState();
                break;
            case State.Returning:
                HandleReturningState();
                break;
            case State.Falling:
                //currentGlueHitCount = 0;
                break;
        }
    }

    protected override GameObject DetectAndLockTarget()
    {
        if (chaseMode == ChaseMode.Normal)
        {
            // เรียกของ base class เฉพาะ Normal mode
            return base.DetectAndLockTarget();
        }
        // ถ้า zone mode ไม่หาเป้าเองเลย
        return currentTarget;
    }

    void HandleIdleState()
    {
        //currentGlueHitCount = 0;
        if (chaseMode == ChaseMode.Normal)
        {
            GameObject target = DetectAndLockTarget();
            if (target != null)
            {
                currentState = State.Flying;
                SetNewFlightTarget();
            }
        }
        else if (chaseMode == ChaseMode.Zone)
        {
            if (isPlayerInZone && playerInZone != null)
            {
                currentTarget = playerInZone;
                currentState = State.Flying;
                SetNewFlightTarget();
            }
        }
    }

    void HandleFlyingState()
    {
        GameObject target = DetectAndLockTarget();
        if (target == null)
        {
            currentState = State.Returning;
            return;
        }

        // Smooth acceleration
        currentSpeed = Mathf.Lerp(currentSpeed, flyingSpeed, Time.deltaTime * 2f);

        // เคลื่อนที่แบบ Smooth
        Vector2 direction = (flightTargetPosition - (Vector2)transform.position).normalized;
        transform.position = Vector2.MoveTowards(transform.position, flightTargetPosition, currentSpeed * Time.deltaTime);

        // Flip sprite ตามทิศทาง
        if (direction.x != 0)
        {
            transform.localScale = new Vector3(Mathf.Sign(direction.x), 1, 1);
        }

        // เมื่อถึงเป้าหมายหรือใกล้มาก
        if (Vector2.Distance(transform.position, flightTargetPosition) < 0.5f)
        {
            SetNewFlightTarget();
        }

        // นับเวลาโจมตี
        attackTimer += Time.deltaTime;
        if (attackTimer >= attackCooldown && IsTargetInRange())
        {
            currentState = State.Attacking;
        }

        if (!canFallByGlue)
            canFallByGlue = false;
    }

    void HandleAttackingState()
    {
        if (!isAttacking)
        {
            StartCoroutine(AttackSequence());
        }
    }

    void HandleReturningState()
    {
        currentSpeed = Mathf.Lerp(currentSpeed, flyingSpeed, Time.deltaTime * 2f);
        transform.position = Vector2.MoveTowards(transform.position, initialPosition, currentSpeed * Time.deltaTime);

        // Flip sprite
        Vector2 direction = ((Vector2)initialPosition - (Vector2)transform.position).normalized;

        if (direction.x != 0)
        {
            transform.localScale = new Vector3(Mathf.Sign(direction.x), 1, 1);
        }

        if (Vector2.Distance(transform.position, initialPosition) < 0.1f)
        {
            currentState = State.Idle;
           
            currentGlueHitCount = 0;
            attackTimer = 0f;
        }
    }

    IEnumerator AttackSequence()
    {
        isAttacking = true;

        // เพิ่มสุ่ม delay ก่อนยิง
        float shootDelay = Random.Range(0.1f, 0.7f);
        yield return new WaitForSeconds(shootDelay);

        // หยุดเคลื่อนที่และเล็ง
        currentSpeed = 0f;
        yield return new WaitForSeconds(0.5f);

        if (currentTarget != null)
        {
            // ยิงกระสุนไปที่ผู้เล่น
            GameObject projectile = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.identity);
            Vector2 direction = (currentTarget.transform.position - projectileSpawnPoint.position).normalized;

            if (projectile.TryGetComponent<Rigidbody2D>(out var projectileRb))
            {
                projectileRb.linearVelocity = direction * projectileSpeed;
            }

            // หมุนกระสุนให้ตรงกับทิศทาง (Optional)
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            projectile.transform.rotation = Quaternion.Euler(0, 0, angle);
        }
        float afterShotWait = Random.Range(0.2f, 0.7f);
        yield return new WaitForSeconds(afterShotWait);
        //yield return new WaitForSeconds(1f);

        attackTimer = 0f;
        currentState = State.Flying;
        SetNewFlightTarget();
        isAttacking = false;
    }

    void SetNewFlightTarget()
    {
        Vector2 newTarget = Vector2.zero;
        bool validPositionFound = false;

        for (int i = 0; i < maxPositionAttempts; i++)
        {
            // กรณีมีผู้เล่น: บินไปรอบๆ เหนือผู้เล่น

            if (currentTarget != null)
            {
                float randomX = Random.Range(-flightPatrolRadius, flightPatrolRadius); 
                float targetY = currentTarget.transform.position.y + Random.Range(hoverHeight * 0.7f, hoverHeight * 1.3f);
                newTarget = new Vector2(currentTarget.transform.position.x + randomX, targetY);
            }
            else
            {
                // ปกติบินรอบรัง
                float randomX = Random.Range(-flightPatrolRadius, flightPatrolRadius);
                newTarget = new Vector2(initialPosition.x + randomX, initialPosition.y);
            }
            //if (currentTarget != null)
            //{
            //    float randomX = Random.Range(-flightPatrolRadius, flightPatrolRadius);
            //    float targetY = currentTarget.transform.position.y + hoverHeight;
            //    newTarget = new Vector2(currentTarget.transform.position.x + randomX, targetY);
            //}
            //else
            //{
            //    // กรณีไม่มีผู้เล่น: บินรอบรัง
            //    float randomX = Random.Range(-flightPatrolRadius, flightPatrolRadius);
            //    newTarget = new Vector2(initialPosition.x + randomX, initialPosition.y);
            //}

            // ตรวจสอบว่าเส้นทางไปยังตำแหน่งใหม่ชนกำแพงหรือไม่
            if (IsPathClear(transform.position, newTarget))
            {
                validPositionFound = true;
                break;
            }
        }

        // ถ้าหาไม่เจอเลย ให้อยู่กับที่หรือกลับรัง
        if (validPositionFound)
        {
            flightTargetPosition = newTarget;
        }
        else
        {
            Debug.LogWarning("FlyingEnemy: ไม่พบตำแหน่งที่เหมาะสมในการบิน กำลังกลับรัง");
            currentState = State.Returning;
        }
    }

    bool IsPathClear(Vector2 from, Vector2 to)
    {
        Vector2 direction = to - from;
        float distance = direction.magnitude;

        // ยิง Raycast เช็คว่ามีสิ่งกีดขวางหรือไม่
        RaycastHit2D hit = Physics2D.Raycast(from, direction.normalized, distance, obstacleLayer);

        // ถ้าไม่ชน = เส้นทางปลอดภัย
        return hit.collider == null;
    }

    bool IsTargetInRange()
    {
        if (currentTarget == null) return false;
        return Vector2.Distance(transform.position, currentTarget.transform.position) <= attackRange;
    }

    public override void ApplyGradualSlow(float targetSlowAmount, float duration, float lerpTime)
    {
        // ไม่ต้องสนใจค่า slowAmount หรือ lerpTime
        // แค่เรียกฟังก์ชัน ApplySlow ของตัวเอง แล้วส่ง "ระยะเวลา" ที่ถูกต้องไปก็พอ
     
        ApplySlow(targetSlowAmount, duration);
        //ApplySlow(targetSlowAmount, duration);
        //ApplySlow(0f, duration);
    }
    public override void ApplySlow(float slowAmount, float duration)
    {

        //// บล็อกถ้าเพิ่งโดนในเวลา cooldown
        //if (Time.time - lastGlueHitTime < glueHitCooldown)
        //    return;

        //lastGlueHitTime = Time.time;

        if ((currentState == State.Flying || currentState == State.Attacking || currentState == State.Returning))
        {
            
            //// ถ้าครบจำนวนที่กำหนด → ร่วง
            //if (currentGlueHitCount >= requiredGlueHitsToFall && !canFallByGlue)
            //{
            //    canFallByGlue = true;
            //    StartCoroutine(GroundedByGlueSequence(duration));
                
            //}
            // ถ้ายังไม่ถึง limit แค่ชะลอความเร็วหรือ effect ได้ (no fall yet)
        }
        else
        {
            // โดนกาวขณะไม่บิน รีเซ็ต counter ทิ้ง
           

            canFallByGlue = false;
        }
    }

    private IEnumerator GroundedByGlueSequence(float duration)
    {
        Debug.Log("Flying enemy hit by glue! Falling...");
        currentState = State.Falling;
        rb.gravityScale = 1f; // 1. เปิดแรงโน้มถ่วงเพื่อให้ร่วง
        currentSpeed = 0f;

        yield return new WaitForSeconds(duration); // 2. รอตามระยะเวลาของกาวทั้งหมด

        Debug.Log("Glue effect wore off. Returning to flight.");
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        currentState = State.Returning; // 3. สั่งให้บินกลับรัง
        canFallByGlue = false;
        currentGlueHitCount = 0; // รีเซ็ต counter (หรือจะรอให้บินใหม่ก่อนรีเซ็ตก็ได้)
    }
   
    protected override void Die()
    {
        if (dropItemPrefab != null)
        {
            Instantiate(dropItemPrefab, transform.position, Quaternion.identity);
        }
        base.Die();
    }

    protected override void Patrol() { }

    // แสดงเส้น Debug ใน Scene View
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        // วงกลมรัศมีการบิน
        Gizmos.color = Color.cyan;
        Vector3 center = Application.isPlaying ? initialPosition : transform.position;
        DrawCircle(center, flightPatrolRadius, 30);

        // วงกลมระยะโจมตี
        Gizmos.color = Color.red;
        DrawCircle(transform.position, attackRange, 40);

        // เส้นไปยังเป้าหมาย
        if (Application.isPlaying && currentState == State.Flying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, flightTargetPosition);
            Gizmos.DrawWireSphere(flightTargetPosition, 0.3f);
        }

        // เส้นตรวจจับกำแพง
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Vector2 toTarget = flightTargetPosition - (Vector2)transform.position;
            Gizmos.DrawRay(transform.position, toTarget.normalized * wallDetectionDistance);
        }
    }

    void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }

    public void OnPlayerEnterZone(GameObject player)
    {
        isPlayerInZone = true;
        playerInZone = player;
        currentTarget = player;
        currentState = State.Flying;
        SetNewFlightTarget();
    }

    public void OnPlayerExitZone()
    {
        isPlayerInZone = false;
        playerInZone = null;
        currentTarget = null;
        currentState = State.Returning;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<GlueProjectile>() != null)
        {
            currentGlueHitCount++;
            //Debug.Log($"{enemyName} glue hit! (count = {currentGlueHitCount})");

            // ถ้าครบจำนวนที่กำหนด → ร่วง
            if (currentGlueHitCount >= requiredGlueHitsToFall && !canFallByGlue)
            {
                canFallByGlue = true;
                StartCoroutine(GroundedByGlueSequence(2.5f));

            }
        }
    }
}