using System.Collections;
using System.Collections.Generic; // เพิ่มเพื่อใช้ List ถ้าจำเป็น แต่ Array ก็พอครับ
using UnityEngine;

public class FlyingEnemy : Enemy
{
    private enum State { Idle, PreparingAttack, Flying, Attacking, Falling, Returning, FlyingToDoll, Perched }
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
    [Header("Doll / Nest System")]
    [Tooltip("Layer ของตุ๊กตาหรือจุดที่ต้องการให้ศัตรูบินไปเกาะ")]
    public LayerMask dollLayer;
    [Tooltip("ระยะที่ศัตรูจะมองเห็นตุ๊กตา")]
    public float dollDetectionRadius = 15f;
    [Tooltip("ระยะห่างที่จะทำการ Snap เข้าเกาะ")]
    public float dollSnapDistance = 0.5f;

    private Transform currentDollTarget; // เก็บเป้าหมายตุ๊กตาปัจจุบัน
    #region Attack Settings
    [Header("Attack Settings")]
    [Tooltip("ระยะที่เริ่มเตรียมตัวโจมตี")]
    public float attackRange = 8f;

    [Tooltip("เวลาคูลดาวน์ระหว่างการโจมตี")]
    public float attackCooldown = 3f;

    [Tooltip("⭐ เวลาเตรียมตัวก่อนยิง (แสดง Telegraph)")]
    public float attackPrepareTime = 1.2f;

    [Tooltip("⭐ แสดง LineRenderer เป็นเลเซอร์เตือนก่อนยิง")]
    public bool showAttackTelegraph = true;

    [Tooltip("สี Telegraph (เริ่มต้น)")]
    public Color telegraphStartColor = new Color(1f, 1f, 0f, 0.3f);

    [Tooltip("สี Telegraph (ก่อนยิง)")]
    public Color telegraphEndColor = new Color(1f, 0f, 0f, 0.8f);

    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;
    public float projectileSpeed = 10f;

    private LineRenderer attackLineRenderer;
    private float attackTimer;
    private bool isAttacking = false;
    private Vector2 lockedAttackTarget; // ⭐ ล็อคเป้าหมายตอน prepare
    public enum FireRespawnMode { TimeBased, WaitForArrival }
    [Tooltip("เลือกโหมดการปล่อยกระสุน: TimeBased = ตามคูลดาวน์, WaitForArrival = รอให้กระสุนถึงเป้าก่อนปล่อยใหม่")]
    public FireRespawnMode fireRespawnMode = FireRespawnMode.TimeBased;
    [Tooltip("ระยะที่ถือว่ากระสุนถึงเป้าหมาย (ในหน่วยยูนิต)")]
    public float arrivalThreshold = 0.5f;
    [Tooltip("เวลาสูงสุดที่จะรอกระสุนมาถึงก่อนยอมให้ยิงใหม่ (s)")]
    public float projectileMaxArrivalWait = 5f;
    private bool canFire = true;
    #endregion

    [Header("Loot Drop")]
    [Tooltip("(Optional) ไอเทมที่จะดรอปเมื่อตาย")]
    public GameObject dropItemPrefab;

    [Header("Debug")]
    public bool showDebugGizmos = true;
    public bool showGlueDebugLogs = true;
 
    private bool isPlayerInZone = false;
    private GameObject playerInZone;
    private Vector2 flightTargetPosition;

    private float currentSpeed;

    // --- [ส่วนที่เพิ่ม 2] ตัวแปรเก็บจุดหมายที่จะบินกลับ ---
    private Vector2 currentReturnTarget;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        if (showAttackTelegraph)
        {
            attackLineRenderer = gameObject.AddComponent<LineRenderer>();
            attackLineRenderer.startWidth = 0.1f;
            attackLineRenderer.endWidth = 0.05f;
            attackLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            attackLineRenderer.startColor = telegraphStartColor;
            attackLineRenderer.endColor = telegraphStartColor;
            attackLineRenderer.enabled = false;
            attackLineRenderer.sortingOrder = 10;
        }
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
        // ถ้าอยู่ในสถานะร่วง หรือโดนกาวครบ ให้หยุดการทำงานของ states ทั้งหมด
        // ยกเว้นถ้าอยู่ในจุดเกาะ (Idle) — ในกรณีนั้นปล่อยให้ Idle ทำงานปกติ
        if ((isCurrentlyFalling || currentGlueHitCount >= requiredGlueHitsToFall) && currentState != State.Idle)
        {
            currentState = State.Falling;
            return;
        }
        if (currentState != State.Perched && currentState != State.Falling && currentState != State.FlyingToDoll)
        {
            CheckForDoll();
        }

        switch (currentState)
        {
            case State.Idle:
                HandleIdleState();
                break;
            case State.Flying:
                HandleFlyingState();
                break;
            case State.PreparingAttack:
                HandlePreparingAttackState();
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
            case State.FlyingToDoll:
                HandleFlyingToDollState();
                break;
            case State.Perched:
                HandlePerchedState();
                break;
        }
    }

    #region CheckForDoll
    void CheckForDoll()
    {
        // ใช้ OverlapCircle หา object ใน Doll Layer
        Collider2D doll = Physics2D.OverlapCircle(transform.position, dollDetectionRadius, dollLayer);

        if (doll != null)
        {
            // ถ้าเจอตุ๊กตา ให้เปลี่ยนเป้าหมายทันที
            currentDollTarget = doll.transform;
            currentState = State.FlyingToDoll;

            // ยกเลิกการโจมตีทั้งหมด
            isAttacking = false;
            if (attackLineRenderer != null) attackLineRenderer.enabled = false;
            StopAllCoroutines();
        }
    }

    // --- [Logic ใหม่] บินไปหาตุ๊กตา ---
    void HandleFlyingToDollState()
    {
        // ถ้าตุ๊กตาหายไป ให้กลับไป Idle หรือหาที่เกาะใหม่
        if (currentDollTarget == null)
        {
            currentReturnTarget = GetNearestIdlePosition();
            currentState = State.Returning;
            return;
        }

        // ensure physics won't interfere while flying toward a doll
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        currentSpeed = Mathf.Lerp(currentSpeed, flyingSpeed * 1.5f, Time.deltaTime * 2f); // บินเร็วกว่าปกตินิดหน่อยเพราะดีใจ

        // บินไปหาตำแหน่งตุ๊กตา
        Vector2 directionToDoll = (currentDollTarget.position - transform.position).normalized;
        Vector2 avoidanceVec = GetAvoidanceDirection(directionToDoll); // ยังคงหลบกำแพงอยู่
        Vector2 finalDirection = (directionToDoll + avoidanceVec).normalized;

        // ถ้าไม่มีสิ่งกีดขวาง ให้พุ่งตรงเลยจะได้แม่นยำ
        if (avoidanceVec == Vector2.zero)
        {
            transform.position = Vector2.MoveTowards(transform.position, currentDollTarget.position, currentSpeed * Time.deltaTime);
        }
        else
        {
            transform.position += (Vector3)finalDirection * currentSpeed * Time.deltaTime;
        }

        // หันหน้า
        if (directionToDoll.x != 0) transform.localScale = new Vector3(Mathf.Sign(directionToDoll.x), 1, 1);

        // เช็คระยะ Snap
        if (Vector2.Distance(transform.position, currentDollTarget.position) <= dollSnapDistance)
        {
            currentState = State.Perched;
        }
    }

    // --- [Logic ใหม่] เกาะนิ่งๆ (Snap) ---
    void HandlePerchedState()
    {
        if (currentDollTarget == null)
        {
            // ตุ๊กตาหาย/พัง -> บินกลับจุดปกติต่อ
            currentState = State.Flying;
            SetNewFlightTarget();
            return;
        }

        // Snap ตำแหน่งให้ติดหนึบไปกับตุ๊กตา
        transform.position = currentDollTarget.position;
        rb.linearVelocity = Vector2.zero;
        currentSpeed = 0f;

        // Reset ค่าต่างๆ
        currentGlueHitCount = 0;
        attackTimer = 0f;
        isAttacking = false;

        // *หมายเหตุ* ใน State นี้จะไม่มีการเรียก Attack Logic เลย ทำให้มันไม่โจมตี
    }
    #endregion

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
                // ensure physics won't keep the enemy sliding if it was pushed while grounded
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
                SetNewFlightTarget();
            }
        }
        else if (chaseMode == ChaseMode.Zone)
        {
                if (isPlayerInZone && playerInZone != null)
                {
                    currentTarget = playerInZone;
                    currentState = State.Flying;
                    // ensure physics won't keep the enemy sliding if it was pushed while grounded
                    rb.gravityScale = 0f;
                    rb.linearVelocity = Vector2.zero;
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

        // ensure physics won't keep pushing the enemy after being knocked while grounded
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        // make sure rigidbody isn't still carrying any push impulse
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

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
        if (attackTimer >= attackCooldown && IsTargetInRange() && canFire)
        {
            currentState = State.PreparingAttack;
            attackTimer = 0f;
            canFire = false; // prevent overlapping prepare coroutines
        }
    }

    void HandleAttackingState()
    {
        // State นี้ใช้ตอนยิงจริงๆ แล้ว (จะถูก coroutine จัดการ)
    }


    void HandlePreparingAttackState()
    {
        if (!isAttacking)
        {
            StartCoroutine(PrepareAndAttackSequence());
        }
    }


    #region Attack System
    // ⭐ Coroutine ใหม่: เตรียมตัว -> แสดง Telegraph -> ยิง
    IEnumerator PrepareAndAttackSequence()
    {
        isAttacking = true;
        currentSpeed = 0f; // หยุดเคลื่อนที่

        // ถ้ากำลังร่วงแล้ว ให้ยกเลิกการเตรียมโจมตี
        if (isCurrentlyFalling)
        {
            isAttacking = false;
            yield break;
        }

        // 1. ล็อคเป้าหมาย
        if (currentTarget != null)
        {
            lockedAttackTarget = currentTarget.transform.position;
        }
        else
        {
            // ถ้าไม่มีเป้าหมาย ยกเลิกการโจมตี
            isAttacking = false;
            currentState = State.Flying;
            yield break;
        }

        // 2. แสดง Telegraph (เลเซอร์เตือน)
        if (showAttackTelegraph && attackLineRenderer != null)
        {
            attackLineRenderer.enabled = true;
            float elapsedTime = 0f;

            while (elapsedTime < attackPrepareTime)
            {
                // ถ้าถูกกาวหรือร่วงในระหว่างเตรียม ให้ยกเลิก
                if (isCurrentlyFalling || currentGlueHitCount >= requiredGlueHitsToFall)
                {
                    attackLineRenderer.enabled = false;
                    isAttacking = false;
                    yield break;
                }

                elapsedTime += Time.deltaTime;
                float t = elapsedTime / attackPrepareTime;

                // อัพเดทตำแหน่งเลเซอร์
                attackLineRenderer.SetPosition(0, projectileSpawnPoint.position);
                attackLineRenderer.SetPosition(1, lockedAttackTarget);

                // เปลี่ยนสีจากเหลือง -> แดง
                Color lerpedColor = Color.Lerp(telegraphStartColor, telegraphEndColor, t);
                attackLineRenderer.startColor = lerpedColor;
                attackLineRenderer.endColor = lerpedColor;

                yield return null;
            }

            attackLineRenderer.enabled = false;
        }
        else
        {
            // ถ้าไม่มี Telegraph ก็รอตามเวลาปกติ
            yield return new WaitForSeconds(attackPrepareTime);
        }

        // 3. ยิงกระสุน (ยิงไปที่ตำแหน่งที่ล็อคไว้)
        if (isCurrentlyFalling || currentGlueHitCount >= requiredGlueHitsToFall)
        {
            isAttacking = false;
            yield break;
        }
        GameObject projectile = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.identity);
        Vector2 direction = (lockedAttackTarget - (Vector2)projectileSpawnPoint.position).normalized;

        if (projectile.TryGetComponent<Rigidbody2D>(out var projectileRb))
        {
            projectileRb.linearVelocity = direction * projectileSpeed;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        projectile.transform.rotation = Quaternion.Euler(0, 0, angle);

        // Manage firing mode: either wait until projectile arrives or allow firing after cooldown
        if (fireRespawnMode == FireRespawnMode.WaitForArrival)
        {
            StartCoroutine(WatchProjectileArrival(projectile, lockedAttackTarget));
        }
        else
        {
            StartCoroutine(ResetCanFireAfterCooldown(attackCooldown));
        }

        // 4. พักสั้นๆ หลังยิง
        yield return new WaitForSeconds(0.3f);

        // 5. กลับสู่สถานะบิน
        currentState = State.Flying;
        SetNewFlightTarget();
        isAttacking = false;
    }
    #endregion

    void HandleReturningState()
    {
        currentSpeed = Mathf.Lerp(currentSpeed, flyingSpeed, Time.deltaTime * 2f);

        // 1. คำนวณทิศทางและแรงหลบ
        Vector2 directionToHome = (currentReturnTarget - (Vector2)transform.position).normalized;
        Vector2 avoidanceVec = GetAvoidanceDirection(directionToHome);
        Vector2 finalDirection = (directionToHome + avoidanceVec).normalized;

        // 2. ใช้ MoveTowards เพื่อให้หยุดสนิทที่จุดหมาย
        // ถ้าไม่มีแรงหลบ ให้ใช้ MoveTowards เข้าหาจุดหมายโดยตรง
        if (avoidanceVec == Vector2.zero)
        {
            transform.position = Vector2.MoveTowards(transform.position, currentReturnTarget, currentSpeed * Time.deltaTime);
        }
        else
        {
            // ถ้าต้องหลบ ให้ใช้ทิศทางผสม
            transform.position += (Vector3)finalDirection * currentSpeed * Time.deltaTime;
        }

        // หันหน้า
        if (finalDirection.x != 0)
            transform.localScale = new Vector3(Mathf.Sign(finalDirection.x), 1, 1);

        // 3. ปรับระยะเช็คให้กว้างขึ้นเล็กน้อย และบังคับตำแหน่งเมื่อถึง
        if (Vector2.Distance(transform.position, currentReturnTarget) < 0.15f)
        {
            transform.position = currentReturnTarget; // Snap เข้าจุด
            rb.linearVelocity = Vector2.zero;         // หยุดแรงเฉื่อย Rigidbody
            currentSpeed = 0f;
            currentState = State.Idle;
            currentGlueHitCount = 0;
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

        Vector2 bestPoint = initialPosition;
        float minWeightedDistance = float.MaxValue;

        List<Vector2> allPoints = new List<Vector2>();
        foreach (var t in idlePoints) if (t != null) allPoints.Add(t.position);
        allPoints.Add(initialPosition);

        foreach (Vector2 pointPos in allPoints)
        {
            float realDist = Vector2.Distance(transform.position, pointPos);
            float weightedDist = realDist;

            // ยิง Raycast เช็คว่ามีกำแพงบังไหม
            Vector2 direction = (pointPos - (Vector2)transform.position).normalized;
            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, realDist, obstacleLayer);

            if (hit.collider != null)
            {
                // ถ้ามีกำแพงบัง ให้คูณโทษเข้าไป (เช่น ถือว่าไกลขึ้น 5 เท่า)
                // หรือบวกค่าคงที่เข้าไปเพื่อให้มันไม่อยากไปทางนี้
                weightedDist = realDist * 5f + 100f;
            }

            // เลือกจุดที่มีคะแนน (Weighted Distance) น้อยที่สุด
            if (weightedDist < minWeightedDistance)
            {
                minWeightedDistance = weightedDist;
                bestPoint = pointPos;
            }
        }

        return bestPoint;
    }

    IEnumerator AttackSequence()
    {
        isAttacking = true;

        if (isCurrentlyFalling)
        {
            isAttacking = false;
            yield break;
        }

        float shootDelay = Random.Range(0.1f, 0.7f);
        yield return new WaitForSeconds(shootDelay);

        if (isCurrentlyFalling)
        {
            isAttacking = false;
            yield break;
        }

        currentSpeed = 0f;
        yield return new WaitForSeconds(0.5f);

        if (isCurrentlyFalling)
        {
            isAttacking = false;
            yield break;
        }
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
            // manage canFire for this attack path as well
            canFire = false;
            if (fireRespawnMode == FireRespawnMode.WaitForArrival)
            {
                StartCoroutine(WatchProjectileArrival(projectile, currentTarget.transform.position));
            }
            else
            {
                StartCoroutine(ResetCanFireAfterCooldown(attackCooldown));
            }
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

        // จุดอ้างอิง: ถ้ามีเป้าหมายใช้เป้าหมาย ถ้าไม่มีใช้จุดกลับบ้าน
        Vector2 basePos = (currentTarget != null) ? (Vector2)currentTarget.transform.position : currentReturnTarget;

        for (int i = 0; i < maxPositionAttempts; i++)
        {
            // สุ่มตำแหน่ง
            float randomX = Random.Range(-flightPatrolRadius, flightPatrolRadius);
            float randomY = Random.Range(hoverHeight * 0.7f, hoverHeight * 1.3f);

            // ถ้าเป็น Zone Mode หรือ Returning ให้ใช้ BasePos เป็นแกนกลาง
            // แต่ถ้าไล่ล่าผู้เล่น ให้พยายามอยู่เหนือหัวผู้เล่น
            newTarget = new Vector2(basePos.x + randomX, basePos.y + randomY);

            // 1. เช็คว่าตำแหน่งปลายทาง ไปชนกำแพงไหม (Overlap)
            Collider2D hitWall = Physics2D.OverlapCircle(newTarget, 0.3f, obstacleLayer);

            // 2. เช็คเส้นทางจากตัวเรา ไปหาจุดนั้น มีกำแพงกั้นไหม (Raycast)
            bool pathIsClear = IsPathClear(transform.position, newTarget);

            if (hitWall == null && pathIsClear)
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
            // ถ้าหาทางไปไม่ได้เลย ให้กลับไปจุด Idle ที่ปลอดภัยที่สุด
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
        if (showGlueDebugLogs) Debug.Log($"[{enemyName}] Falling due to glue!");
        isCurrentlyFalling = true;
        currentState = State.Falling;

        if (isAttacking) { StopAllCoroutines(); isAttacking = false; }
        if (attackLineRenderer != null) attackLineRenderer.enabled = false;

        rb.gravityScale = 1f;
        currentSpeed = 0f;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.5f, rb.linearVelocity.y);

        yield return new WaitForSeconds(duration);

        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        yield return new WaitForSeconds(2.0f); // นอนมึน 2 วิ

        // Takeoff
        float takeoffTimer = 0f;
        while (takeoffTimer < 1.0f)
        {
            transform.position += Vector3.up * flyingSpeed * Time.deltaTime;
            takeoffTimer += Time.deltaTime;
            yield return null;
        }

        // ก่อนจะตัดสินใจบินกลับ ให้เช็คตุ๊กตาก่อนเป็นอันดับแรก
        currentGlueHitCount = 0;
        isCurrentlyFalling = false;

        CheckForDoll(); // ลองหาตุ๊กตาทันทีที่ฟื้น
        if (currentState == State.FlyingToDoll)
        {
            // ถ้าเจอตุ๊กตา ให้ไปหาตุ๊กตาเลย (State เปลี่ยนไปแล้วใน CheckForDoll)
        }
        else
        {
            // ถ้าไม่เจอ ให้กลับจุดเดิม
            currentReturnTarget = GetNearestIdlePosition();
            currentState = State.Returning;
        }
    }

    protected override void Die()
    {
        if (dropItemPrefab != null)
        {
            Instantiate(dropItemPrefab, transform.position, Quaternion.identity);
        }
        base.Die();
    }

    IEnumerator WatchProjectileArrival(GameObject projectile, Vector2 target)
    {
        float timer = 0f;
        while (projectile != null && timer < projectileMaxArrivalWait)
        {
            if (projectile == null) break;
            try
            {
                if (Vector2.Distance(projectile.transform.position, target) <= arrivalThreshold) break;
            }
            catch { break; }
            timer += Time.deltaTime;
            yield return null;
        }
        canFire = true;
    }

    IEnumerator ResetCanFireAfterCooldown(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        canFire = true;
    }

    protected override void Patrol() { }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // ตรวจสอบว่าเป็นกระสุนกาวหรือไม่
        if (other.GetComponent<GlueProjectile>() == null)
            return;
        if (currentState == State.Perched) return;
        // ถ้าอยู่ในจุดเกาะ (Idle) ให้ละเว้นการนับกาว แต่ยอมรับกาวในทุก state อื่นๆ
        if (currentState == State.Idle)
        {
            if (showGlueDebugLogs)
                Debug.Log($"[{enemyName}] Hit by glue but currently perched (Idle), ignoring glue");
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
        if (currentState == State.Perched || currentState == State.FlyingToDoll) return;
        isPlayerInZone = true;
        playerInZone = player;
        currentTarget = player;

        if (currentState == State.Idle || currentState == State.Returning)
        {
            currentState = State.Flying;
            // ensure we clear any push impulse so flight starts cleanly
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
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
        Gizmos.color = Color.green;
        DrawCircle(transform.position, dollDetectionRadius, 40);
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