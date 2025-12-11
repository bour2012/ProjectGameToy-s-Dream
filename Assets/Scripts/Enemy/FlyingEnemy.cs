using System.Collections;
using System.Collections.Generic; // เพิ่มเพื่อใช้ List ถ้าจำเป็น แต่ Array ก็พอครับ
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
    [Tooltip("เวลาบล็อกการนับกาวซ้ำ (วินาที) - ป้องกันกระสุนเดียวนับหลายครั้ง")]
    public float glueHitCooldown = 0.3f;
    [Tooltip("ระยะเวลาที่ร่วงลงมาเมื่อโดนกาว")]
    public float glueFallDuration = 2.5f;

    [HideInInspector] public int currentGlueHitCount = 0;
    private float lastGlueHitTime = -999f;
    private bool isCurrentlyFalling = false; // ป้องกัน coroutine ซ้ำ

    [Header("Flying Enemy Settings")]
    // --- [ส่วนที่เพิ่ม 1] จุดพัก (Idle Points) หลายจุด ---
    [Tooltip("ลาก GameObject (Empty) มาใส่เพื่อกำหนดจุดที่ศัตรูจะบินกลับไปพัก")]
    public Transform[] idlePoints;

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

    // --- [ส่วนที่เพิ่ม 1] การตั้งค่าหลบ Laser ---
    [Header("Laser Avoidance")]
    [Tooltip("Layer ของ Laser หรือสิ่งที่ต้องการให้หลบ")]
    public LayerMask dangerLayer;
    [Tooltip("ระยะตรวจจับ Laser ล่วงหน้า")]
    public float avoidanceDistance = 3f;
    [Tooltip("รัศมีของวงกลมที่ใช้ตรวจจับ (ควรเท่ากับขนาดตัวศัตรู)")]
    public float avoidanceRadius = 0.5f;
    [Tooltip("แรงในการหักหลบ (ยิ่งเยอะยิ่งหลบไว)")]
    public float avoidanceForce = 5f;

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
    public bool showGlueDebugLogs = true;

    private bool isPlayerInZone = false;
    private GameObject playerInZone;
    private Vector2 flightTargetPosition;
    private float attackTimer;
    private bool isAttacking = false;
    private float currentSpeed;

    // --- [ส่วนที่เพิ่ม 2] ตัวแปรเก็บจุดหมายที่จะบินกลับ ---
    private Vector2 currentReturnTarget;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0;

        if (chaseMode == ChaseMode.Zone)
        {
            attackCooldown = Random.Range(1.0f, 3.0f);
            flightPatrolRadius = Random.Range(5f, 7f);
            attackTimer = Random.Range(0f, attackCooldown);
        }
        currentSpeed = 0f;
        
        // กำหนดค่าเริ่มต้น ให้เป็นจุดที่วางตัวศัตรูไว้ตอนแรก
        currentReturnTarget = initialPosition;
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
                // ไม่ทำอะไร รอ coroutine จัดการ
                break;
        }
    }

    protected override GameObject DetectAndLockTarget()
    {
        if (chaseMode == ChaseMode.Normal)
        {
            return base.DetectAndLockTarget();
        }
        return currentTarget;
    }

    void HandleIdleState()
    {
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

    // --- [ส่วนที่เพิ่ม 2] ฟังก์ชันคำนวณทิศทางหลบ ---
    Vector2 GetAvoidanceDirection(Vector2 desiredDirection)
    {
        // ยิง CircleCast ไปข้างหน้าในทิศทางที่จะไป
        RaycastHit2D hit = Physics2D.CircleCast(transform.position, avoidanceRadius, desiredDirection, avoidanceDistance, dangerLayer);

        if (hit.collider != null)
        {
            // ถ้าเจอ Laser หรืออันตราย
            if (showGlueDebugLogs) Debug.DrawLine(transform.position, hit.point, Color.red); // Debug เส้นสีแดง

            // คำนวณทิศทางสะท้อนกลับ (หลบออกจากจุดที่ชน)
            // ใช้ hit.normal (เวกเตอร์ตั้งฉากกับผิว Laser) เพื่อผลักศัตรูออกห่าง
            return hit.normal * avoidanceForce;
        }

        return Vector2.zero; // ถ้าไม่มีอะไรขวาง ไม่ต้องหลบ
    }

    void HandleFlyingState()
    {
        GameObject target = DetectAndLockTarget();
        if (target == null)
        {
            // --- [แก้ไข] เมื่อหาเป้าหมายไม่เจอ ให้คำนวณหาจุดกลับใหม่ทันที ---
            currentReturnTarget = GetNearestIdlePosition();
            currentState = State.Returning;
            return;
        }

        currentSpeed = Mathf.Lerp(currentSpeed, flyingSpeed, Time.deltaTime * 2f);

        // 1. คำนวณทิศทางที่ "อยากจะไป" (หาเป้าหมาย)
        Vector2 directionToTarget = (flightTargetPosition - (Vector2)transform.position).normalized;

        // 2. คำนวณแรงหลบหลีก (Avoidance)
        Vector2 avoidanceVec = GetAvoidanceDirection(directionToTarget);

        // 3. รวมทิศทาง (อยากไป + ต้องหลบ)
        Vector2 finalDirection = (directionToTarget + avoidanceVec).normalized;

        // --- [แก้ไข] เปลี่ยนมาใช้การเลื่อนตำแหน่งแบบ Vector แทน MoveTowards เพื่อให้หลบได้เนียนขึ้น ---
        transform.position += (Vector3)finalDirection * currentSpeed * Time.deltaTime;

        // หันหน้า
        if (finalDirection.x != 0)
        {
            transform.localScale = new Vector3(Mathf.Sign(finalDirection.x), 1, 1);
        }

        if (Vector2.Distance(transform.position, flightTargetPosition) < 0.5f)
        {
            SetNewFlightTarget();
        }

        attackTimer += Time.deltaTime;
        if (attackTimer >= attackCooldown && IsTargetInRange())
        {
            currentState = State.Attacking;
        }
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
        
        // 1. ทิศทางอยากกลับบ้าน
        Vector2 directionToHome = (currentReturnTarget - (Vector2)transform.position).normalized;

        // 2. แรงหลบ
        Vector2 avoidanceVec = GetAvoidanceDirection(directionToHome);

        // 3. รวมทิศทาง
        Vector2 finalDirection = (directionToHome + avoidanceVec).normalized;

        transform.position += (Vector3)finalDirection * currentSpeed * Time.deltaTime;

        if (finalDirection.x != 0)
        {
            transform.localScale = new Vector3(Mathf.Sign(finalDirection.x), 1, 1);
        }

        // เช็คระยะห่างกับจุดหมายใหม่
        if (Vector2.Distance(transform.position, currentReturnTarget) < 0.1f)
        {
            currentState = State.Idle;
            currentGlueHitCount = 0; // รีเซ็ตเมื่อกลับถึงรัง
            attackTimer = 0f;
        }
    }

    // --- [ส่วนที่เพิ่ม 3] ฟังก์ชันคำนวณหาจุดพักที่ใกล้ตัวที่สุด ---
    private Vector2 GetNearestIdlePosition()
    {
        // ถ้าไม่มีจุดพักเลย ให้กลับไปจุดเกิด (initialPosition)
        if (idlePoints == null || idlePoints.Length == 0)
        {
            return initialPosition;
        }

        Vector2 nearestPoint = initialPosition;
        float minDistance = float.MaxValue;

        // วนลูปเช็คทุกจุดใน Array
        foreach (Transform point in idlePoints)
        {
            if (point == null) continue;

            float dist = Vector2.Distance(transform.position, point.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearestPoint = point.position;
            }
        }
        
        // เช็คเทียบกับจุดเกิดด้วย (เผื่อจุดเกิดใกล้กว่า)
        float distToInit = Vector2.Distance(transform.position, initialPosition);
        if (distToInit < minDistance)
        {
            nearestPoint = initialPosition;
        }

        return nearestPoint;
    }

    IEnumerator AttackSequence()
    {
        isAttacking = true;

        float shootDelay = Random.Range(0.1f, 0.7f);
        yield return new WaitForSeconds(shootDelay);

        currentSpeed = 0f;
        yield return new WaitForSeconds(0.5f);

        if (currentTarget != null)
        {
            GameObject projectile = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.identity);
            Vector2 direction = (currentTarget.transform.position - projectileSpawnPoint.position).normalized;

            if (projectile.TryGetComponent<Rigidbody2D>(out var projectileRb))
            {
                projectileRb.linearVelocity = direction * projectileSpeed;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            projectile.transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        float afterShotWait = Random.Range(0.2f, 0.7f);
        yield return new WaitForSeconds(afterShotWait);

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
            if (currentTarget != null)
            {
                float randomX = Random.Range(-flightPatrolRadius, flightPatrolRadius);
                float targetY = currentTarget.transform.position.y + Random.Range(hoverHeight * 0.7f, hoverHeight * 1.3f);
                newTarget = new Vector2(currentTarget.transform.position.x + randomX, targetY);
            }
            else
            {
                float randomX = Random.Range(-flightPatrolRadius, flightPatrolRadius);
                newTarget = new Vector2(currentReturnTarget.x + randomX, currentReturnTarget.y);
            }

            if (IsPathClear(transform.position, newTarget))
            {
                validPositionFound = true;
                break;
            }
        }

        if (validPositionFound)
        {
            flightTargetPosition = newTarget;
        }
        else
        {
            if (showGlueDebugLogs)
                Debug.LogWarning("FlyingEnemy: ไม่พบตำแหน่งที่เหมาะสมในการบิน กำลังกลับรัง");
            // --- [แก้ไข] คำนวณจุดกลับใหม่ก่อนเปลี่ยน State ---
            currentReturnTarget = GetNearestIdlePosition();
            currentState = State.Returning;
        }
    }

    bool IsPathClear(Vector2 from, Vector2 to)
    {
        Vector2 direction = to - from;
        float distance = direction.magnitude;
        RaycastHit2D hit = Physics2D.Raycast(from, direction.normalized, distance, obstacleLayer);
        return hit.collider == null;
    }

    bool IsTargetInRange()
    {
        if (currentTarget == null) return false;
        return Vector2.Distance(transform.position, currentTarget.transform.position) <= attackRange;
    }

    public override void ApplyGradualSlow(float targetSlowAmount, float duration, float lerpTime)
    {
        // Flying enemy ไม่ใช้ slow effect แบบปกติ
        // ถ้าต้องการให้มี effect อื่นตอนโดนกาว สามารถเพิ่มได้ที่นี่
    }

    public override void ApplySlow(float slowAmount, float duration)
    {
        // Flying enemy จะจัดการกาวผ่าน OnTriggerEnter2D แทน
        // ฟังก์ชันนี้ไว้สำหรับ compatibility กับ base class
    }

    private IEnumerator GroundedByGlueSequence(float duration)
    {
        if (showGlueDebugLogs)
            Debug.Log($"[{enemyName}] Falling due to glue! (Hit count: {currentGlueHitCount}/{requiredGlueHitsToFall})");

        isCurrentlyFalling = true;
        currentState = State.Falling;

        // หยุดการโจมตีถ้ากำลังโจมตีอยู่
        if (isAttacking)
        {
            StopCoroutine(nameof(AttackSequence));
            isAttacking = false;
        }

        // เปิดแรงโน้มถ่วง
        rb.gravityScale = 1f;
        currentSpeed = 0f;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.5f, rb.linearVelocity.y); // ลดความเร็วแนวนอน

        yield return new WaitForSeconds(duration);

        if (showGlueDebugLogs)
            Debug.Log($"[{enemyName}] Glue effect ended. Returning to flight.");

        // กลับสู่สถานะบิน
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        currentGlueHitCount = 0; // รีเซ็ต counter
        isCurrentlyFalling = false;

        // --- [แก้ไข] เมื่อหายจากกาว ให้หาจุดกลับที่ใกล้ที่สุด ---
        currentReturnTarget = GetNearestIdlePosition();
        currentState = State.Returning;
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        // ตรวจสอบว่าเป็นกระสุนกาวหรือไม่
        if (other.GetComponent<GlueProjectile>() == null)
            return;

        // ต้องอยู่ใน state ที่บินได้เท่านั้น
        if (currentState != State.Flying && currentState != State.Attacking && currentState != State.Returning)
        {
            if (showGlueDebugLogs)
                Debug.Log($"[{enemyName}] Hit by glue but not in valid state ({currentState})");
            return;
        }
            
        // ถ้ากำลังร่วงอยู่แล้ว ไม่ต้องนับ
        if (isCurrentlyFalling)
        {
            if (showGlueDebugLogs)
                Debug.Log($"[{enemyName}] Already falling, ignoring glue hit");
            return;
        }

        // ตรวจสอบ cooldown เพื่อป้องกันการนับซ้ำจากกระสุนเดียวกัน
        if (Time.time - lastGlueHitTime < glueHitCooldown)
        {
            if (showGlueDebugLogs)
                Debug.Log($"[{enemyName}] Glue hit ignored (cooldown: {Time.time - lastGlueHitTime:F2}s)");
            return;
        }

        // นับการโดนกาว
        lastGlueHitTime = Time.time;
        currentGlueHitCount++;

        if (showGlueDebugLogs)
            Debug.Log($"[{enemyName}] Glue hit registered! Count: {currentGlueHitCount}/{requiredGlueHitsToFall}");

        // ตรวจสอบว่าครบจำนวนที่กำหนดหรือยัง
        if (currentGlueHitCount >= requiredGlueHitsToFall)
        {
            StartCoroutine(GroundedByGlueSequence(glueFallDuration));
        }
    }

    public void OnPlayerEnterZone(GameObject player)
    {
        isPlayerInZone = true;
        playerInZone = player;
        currentTarget = player;

        if (currentState == State.Idle || currentState == State.Returning)
        {
            currentState = State.Flying;
            SetNewFlightTarget();
        }
    }

    public void OnPlayerExitZone()
    {
        isPlayerInZone = false;
        playerInZone = null;
        currentTarget = null;
        // --- [แก้ไข] เมื่อผู้เล่นออกจากโซน ให้หาจุดกลับที่ใกล้ที่สุด ---
        currentReturnTarget = GetNearestIdlePosition();
        currentState = State.Returning;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Gizmos.color = Color.cyan;
        // ใช้ currentReturnTarget แทน initialPosition เพื่อดูว่ามันจะบินรอบๆ จุดไหน
        Vector3 center = Application.isPlaying ? (Vector3)currentReturnTarget : transform.position;
        DrawCircle(center, flightPatrolRadius, 30);

        Gizmos.color = Color.red;
        DrawCircle(transform.position, attackRange, 40);

        if (Application.isPlaying && currentState == State.Flying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, flightTargetPosition);
            Gizmos.DrawWireSphere(flightTargetPosition, 0.3f);
        }

        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Vector2 toTarget = flightTargetPosition - (Vector2)transform.position;
            Gizmos.DrawRay(transform.position, toTarget.normalized * wallDetectionDistance);
        }

        // แสดงสถานะกาว
        if (Application.isPlaying && currentGlueHitCount > 0)
        {
            Gizmos.color = Color.magenta;
            float radius = 0.5f + (currentGlueHitCount * 0.2f);
            DrawCircle(transform.position, radius, 20);
        }

        // --- [เพิ่ม] แสดงจุด Idle Points ทั้งหมดใน Scene ---
        if (idlePoints != null)
        {
            Gizmos.color = Color.blue;
            foreach (var point in idlePoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, 0.5f);
                    Gizmos.DrawLine(transform.position, point.position); // ลากเส้นเช็คระยะ
                }
            }
        }

        // --- [เพิ่ม] Gizmo แสดงรัศมีการตรวจจับ Laser ---
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f); // สีส้มจางๆ
        // วาดวงกลมข้างหน้าศัตรู
        Vector3 direction = Vector3.right;
        if(Application.isPlaying && currentState == State.Flying)
             direction = (flightTargetPosition - (Vector2)transform.position).normalized;
             
        Gizmos.DrawWireSphere(transform.position + (direction * avoidanceDistance), avoidanceRadius);
        Gizmos.DrawLine(transform.position, transform.position + (direction * avoidanceDistance));
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
}