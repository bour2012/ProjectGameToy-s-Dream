using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyingEnemy : Enemy
{
    private enum State { Idle, PreparingAttack, Flying, Attacking, Falling, Returning, FlyingToDoll, Perched, GroundedForever }
    private State currentState = State.Idle;

    public enum ChaseMode { Normal, Zone }
    public ChaseMode chaseMode = ChaseMode.Normal;

    [Header("Permanent Rest Settings")]
    [Tooltip("ลากจุด IdlePoint ที่ต้องการให้เป็นจุดกับดัก (พักตลอดไป) มาใส่ในช่องนี้")]
    public Transform permanentRestPoint;

    [Header("Glue Settings")]
    [Tooltip("จำนวนครั้งที่ต้องโดนกาวก่อนจะร่วง")]
    public int requiredGlueHitsToFall = 3;
    [Tooltip("เวลาบล็อกการนับกาวซ้ำ (วินาที) - ป้องกันกระสุนเดียวนับหลายครั้ง")]
    public float glueHitCooldown = 0.3f;
    [Tooltip("ระยะเวลาที่ร่วงลงมาเมื่อโดนกาว")]
    public float glueFallDuration = 2.5f;

    [HideInInspector] public int currentGlueHitCount = 0;
    private float lastGlueHitTime = -999f;
    private bool isCurrentlyFalling = false;

    [Header("Flying Enemy Settings")]
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

    private Transform currentDollTarget;

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
    private Vector2 lockedAttackTarget;
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
        currentReturnTarget = initialPosition;
    }

    protected override void Update()
    {
        // ⭐ แก้ไข 1: ถ้าเป็น GroundedForever ให้ล็อคตายตรงนี้เลย
        if (currentState == State.GroundedForever)
        {
            if (animator != null)
            {
                animator.SetBool("IsMoving", true);   // Walk
                animator.SetBool("IsSpecial", false);
                animator.SetBool("IsAttacking", false);
            }
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if ((isCurrentlyFalling || currentGlueHitCount >= requiredGlueHitsToFall) && currentState != State.Idle)
        {
            currentState = State.Falling;
            if (animator != null)
            {
                animator.SetBool("IsSpecial", false);
                animator.SetBool("IsAttacking", false);
            }
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
                break;
            case State.FlyingToDoll:
                HandleFlyingToDollState();
                break;
            case State.Perched:
                HandlePerchedState();
                break;
        }

        if (currentState != State.GroundedForever)
        {
            HandleCustomAnimation();
        }
    }

    private void HandleCustomAnimation()
    {
        if (animator == null) return;
        if (currentState == State.GroundedForever) return;

        bool isSpecialAction = (currentState == State.Perched);
        animator.SetBool("IsSpecial", isSpecialAction);

        //bool isAttackingAction = (currentState == State.PreparingAttack);
        //if(isAttackingAction)
      

        

        if (currentState == State.Flying || currentState == State.Returning || currentState == State.FlyingToDoll)
        {
            animator.SetBool("IsMoving", false); // บิน -> Idle Animation
        }
        else if (currentState == State.Idle )
        {
            animator.SetBool("IsMoving", false); // Idle State -> Walk Animation
        }
    }

    #region CheckForDoll
    void CheckForDoll()
    {
        Collider2D doll = Physics2D.OverlapCircle(transform.position, dollDetectionRadius, dollLayer);
        if (doll != null)
        {
            currentDollTarget = doll.transform;
            currentState = State.FlyingToDoll;
            isAttacking = false;
            if (attackLineRenderer != null) attackLineRenderer.enabled = false;
            StopAllCoroutines();
        }
    }

    void HandleFlyingToDollState()
    {
        if (currentDollTarget == null)
        {
            currentReturnTarget = GetNearestIdlePosition();
            currentState = State.Returning;
            return;
        }
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        currentSpeed = Mathf.Lerp(currentSpeed, flyingSpeed * 1.5f, Time.deltaTime * 2f);

        Vector2 directionToDoll = (currentDollTarget.position - transform.position).normalized;
        Vector2 avoidanceVec = GetAvoidanceDirection(directionToDoll);
        Vector2 finalDirection = (directionToDoll + avoidanceVec).normalized;

        if (avoidanceVec == Vector2.zero)
            transform.position = Vector2.MoveTowards(transform.position, currentDollTarget.position, currentSpeed * Time.deltaTime);
        else
            transform.position += (Vector3)finalDirection * currentSpeed * Time.deltaTime;

        if (directionToDoll.x != 0) transform.localScale = new Vector3(Mathf.Sign(directionToDoll.x), 1, 1);

        if (Vector2.Distance(transform.position, currentDollTarget.position) <= dollSnapDistance)
        {
            currentState = State.Perched;
        }
    }

    void HandlePerchedState()
    {
        if (currentDollTarget == null)
        {
            currentState = State.Flying;
            SetNewFlightTarget();
            return;
        }
        transform.position = currentDollTarget.position;
        rb.linearVelocity = Vector2.zero;
        currentSpeed = 0f;
        currentGlueHitCount = 0;
        attackTimer = 0f;
        isAttacking = false;
    }
    #endregion

    protected override GameObject DetectAndLockTarget()
    {
        if (chaseMode == ChaseMode.Normal)
            return base.DetectAndLockTarget();
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
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
                SetNewFlightTarget();
            }
        }
    }

    Vector2 GetAvoidanceDirection(Vector2 desiredDirection)
    {
        RaycastHit2D hit = Physics2D.CircleCast(transform.position, avoidanceRadius, desiredDirection, avoidanceDistance, dangerLayer);
        if (hit.collider != null)
        {
            if (showGlueDebugLogs) Debug.DrawLine(transform.position, hit.point, Color.red);
            return hit.normal * avoidanceForce;
        }
        return Vector2.zero;
    }

    void HandleFlyingState()
    {
        GameObject target = DetectAndLockTarget();
        if (target == null)
        {
            currentReturnTarget = GetNearestIdlePosition();
            currentState = State.Returning;
            return;
        }
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        currentSpeed = Mathf.Lerp(currentSpeed, flyingSpeed, Time.deltaTime * 2f);

        Vector2 directionToTarget = (flightTargetPosition - (Vector2)transform.position).normalized;
        Vector2 avoidanceVec = GetAvoidanceDirection(directionToTarget);
        Vector2 finalDirection = (directionToTarget + avoidanceVec).normalized;

        transform.position += (Vector3)finalDirection * currentSpeed * Time.deltaTime;

        if (finalDirection.x != 0)
            transform.localScale = new Vector3(Mathf.Sign(finalDirection.x), 1, 1);

        if (Vector2.Distance(transform.position, flightTargetPosition) < 0.5f)
        {
            SetNewFlightTarget();
        }

        attackTimer += Time.deltaTime;
        if (attackTimer >= attackCooldown && IsTargetInRange() && canFire)
        {
            currentState = State.PreparingAttack;
            attackTimer = 0f;
            canFire = false;
        }
    }

    // ⭐ เพิ่มกลับมาให้แล้ว: ฟังก์ชันสุ่มหาจุดบินใหม่
    void SetNewFlightTarget()
    {
        Vector2 newTarget = Vector2.zero;
        bool validPositionFound = false;

        Vector2 basePos = (currentTarget != null) ? (Vector2)currentTarget.transform.position : currentReturnTarget;

        for (int i = 0; i < maxPositionAttempts; i++)
        {
            float randomX = Random.Range(-flightPatrolRadius, flightPatrolRadius);
            float randomY = Random.Range(hoverHeight * 0.7f, hoverHeight * 1.3f);
            newTarget = new Vector2(basePos.x + randomX, basePos.y + randomY);

            Collider2D hitWall = Physics2D.OverlapCircle(newTarget, 0.3f, obstacleLayer);
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
            currentReturnTarget = GetNearestIdlePosition();
            currentState = State.Returning;
        }
    }

    // ⭐ เพิ่มกลับมาให้แล้ว: ฟังก์ชันเช็คสิ่งกีดขวาง
    bool IsPathClear(Vector2 from, Vector2 to)
    {
        Vector2 direction = to - from;
        float distance = direction.magnitude;
        RaycastHit2D hit = Physics2D.Raycast(from, direction.normalized, distance, obstacleLayer);
        return hit.collider == null;
    }

    void HandleAttackingState() { }

    void HandlePreparingAttackState()
    {
        if (!isAttacking)
            StartCoroutine(PrepareAndAttackSequence());
    }

    #region Attack System
    IEnumerator PrepareAndAttackSequence()
    {
        isAttacking = true;
        currentSpeed = 0f;

        if (isCurrentlyFalling) { isAttacking = false; yield break; }

        if (currentTarget != null) lockedAttackTarget = currentTarget.transform.position;
        else { isAttacking = false; currentState = State.Flying; yield break; }

        if (showAttackTelegraph && attackLineRenderer != null)
        {
            attackLineRenderer.enabled = true;
            float elapsedTime = 0f;
            while (elapsedTime < attackPrepareTime)
            {
                if (isCurrentlyFalling || currentGlueHitCount >= requiredGlueHitsToFall)
                {
                    attackLineRenderer.enabled = false; isAttacking = false; yield break;
                }
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / attackPrepareTime;
                attackLineRenderer.SetPosition(0, projectileSpawnPoint.position);
                attackLineRenderer.SetPosition(1, lockedAttackTarget);
                Color lerpedColor = Color.Lerp(telegraphStartColor, telegraphEndColor, t);
                attackLineRenderer.startColor = lerpedColor;
                attackLineRenderer.endColor = lerpedColor;
                yield return null;
            }
            attackLineRenderer.enabled = false;
        }
        else
        {
            yield return new WaitForSeconds(attackPrepareTime);
        }

        if (isCurrentlyFalling || currentGlueHitCount >= requiredGlueHitsToFall) { isAttacking = false; yield break; }

        GameObject projectile = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.identity);
        animator.SetTrigger("Hit");
        Vector2 direction = (lockedAttackTarget - (Vector2)projectileSpawnPoint.position).normalized;
        if (projectile.TryGetComponent<Rigidbody2D>(out var projectileRb))
            projectileRb.linearVelocity = direction * projectileSpeed;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        projectile.transform.rotation = Quaternion.Euler(0, 0, angle);

        if (fireRespawnMode == FireRespawnMode.WaitForArrival)
            StartCoroutine(WatchProjectileArrival(projectile, lockedAttackTarget));
        else
            StartCoroutine(ResetCanFireAfterCooldown(attackCooldown));

        yield return new WaitForSeconds(0.3f);
        currentState = State.Flying;
        SetNewFlightTarget();
        isAttacking = false;
    }
    #endregion

    void HandleReturningState()
    {
        currentSpeed = Mathf.Lerp(currentSpeed, flyingSpeed, Time.deltaTime * 2f);
        Vector2 directionToHome = (currentReturnTarget - (Vector2)transform.position).normalized;
        Vector2 avoidanceVec = GetAvoidanceDirection(directionToHome);
        Vector2 finalDirection = (directionToHome + avoidanceVec).normalized;

        if (avoidanceVec == Vector2.zero)
            transform.position = Vector2.MoveTowards(transform.position, currentReturnTarget, currentSpeed * Time.deltaTime);
        else
            transform.position += (Vector3)finalDirection * currentSpeed * Time.deltaTime;

        if (finalDirection.x != 0)
            transform.localScale = new Vector3(Mathf.Sign(finalDirection.x), 1, 1);

        if (Vector2.Distance(transform.position, currentReturnTarget) < 0.15f)
        {
            transform.position = currentReturnTarget;
            rb.linearVelocity = Vector2.zero;
            currentSpeed = 0f;

            // ⭐ แก้ไข 2: เช็คว่าเป็นจุดพักถาวรหรือไม่?
            if (permanentRestPoint != null && Vector2.Distance(transform.position, permanentRestPoint.position) < 0.1f)
            {
                currentState = State.GroundedForever;
                Debug.Log("Reached permanent rest point. Walking forever.");
            }
            else
            {
                currentState = State.Idle;
            }

            currentGlueHitCount = 0;
            attackTimer = 0f;
        }
    }

    private Vector2 GetNearestIdlePosition()
    {
        if (idlePoints == null || idlePoints.Length == 0) return initialPosition;

        Vector2 bestPoint = initialPosition;
        float minWeightedDistance = float.MaxValue;

        List<Vector2> allPoints = new List<Vector2>();
        foreach (var t in idlePoints) if (t != null) allPoints.Add(t.position);
        allPoints.Add(initialPosition);

        foreach (Vector2 pointPos in allPoints)
        {
            float realDist = Vector2.Distance(transform.position, pointPos);
            float weightedDist = realDist;
            Vector2 direction = (pointPos - (Vector2)transform.position).normalized;
            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, realDist, obstacleLayer);
            if (hit.collider != null) weightedDist = realDist * 5f + 100f;

            if (weightedDist < minWeightedDistance)
            {
                minWeightedDistance = weightedDist;
                bestPoint = pointPos;
            }
        }
        return bestPoint;
    }

    IEnumerator WatchProjectileArrival(GameObject projectile, Vector2 target)
    {
        float timer = 0f;
        while (projectile != null && timer < projectileMaxArrivalWait)
        {
            if (projectile == null) break;
            try { if (Vector2.Distance(projectile.transform.position, target) <= arrivalThreshold) break; }
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
        // ⭐ แก้ไข 3: ถ้า GroundedForever แล้ว ห้ามรับกาว
        if (currentState == State.GroundedForever) return;

        if (other.GetComponent<GlueProjectile>() == null) return;
        if (currentState == State.Perched) return;
        if (currentState == State.Idle)
        {
            if (showGlueDebugLogs) Debug.Log($"[{enemyName}] Hit by glue but currently perched (Idle), ignoring glue");
            return;
        }
        if (isCurrentlyFalling) return;
        if (Time.time - lastGlueHitTime < glueHitCooldown) return;

        lastGlueHitTime = Time.time;
        currentGlueHitCount++;
        if (showGlueDebugLogs) Debug.Log($"[{enemyName}] Glue hit registered! Count: {currentGlueHitCount}/{requiredGlueHitsToFall}");

        if (currentGlueHitCount >= requiredGlueHitsToFall)
        {
            StartCoroutine(GroundedByGlueSequence(glueFallDuration));
        }
    }
    private IEnumerator GroundedByGlueSequence(float duration)
    {
        if (showGlueDebugLogs) Debug.Log($"[{enemyName}] Falling due to glue!");
        isCurrentlyFalling = true;
        currentState = State.Falling;

        // ยกเลิกการโจมตีถ้ากำลังทำอยู่
        if (isAttacking) { StopAllCoroutines(); isAttacking = false; }
        if (attackLineRenderer != null) attackLineRenderer.enabled = false;

        // เปิดแรงโน้มถ่วงให้ร่วง
        rb.gravityScale = 1f;
        currentSpeed = 0f;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.5f, rb.linearVelocity.y);

        yield return new WaitForSeconds(duration);

        // หยุดร่วงและนอนมึน
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        yield return new WaitForSeconds(2.0f); // นอนมึน 2 วิ

        // บินขึ้น (Takeoff)
        float takeoffTimer = 0f;
        while (takeoffTimer < 1.0f)
        {
            transform.position += Vector3.up * flyingSpeed * Time.deltaTime;
            takeoffTimer += Time.deltaTime;
            yield return null;
        }

        // ฟื้นตัวเสร็จ รีเซ็ตค่า
        currentGlueHitCount = 0;
        isCurrentlyFalling = false;

        // เช็คว่าควรไปไหนต่อ
        CheckForDoll();
        if (currentState == State.FlyingToDoll)
        {
            // ถ้าเจอตุ๊กตา ก็ไปหาตุ๊กตา
        }
        else
        {
            // ถ้าไม่เจอ ให้กลับไปหาจุดพัก
            currentReturnTarget = GetNearestIdlePosition();
            currentState = State.Returning;
        }
    }
    public void OnPlayerEnterZone(GameObject player)
    {
        // ⭐ แก้ไข 4: ถ้า GroundedForever ห้ามสนใจผู้เล่น
        if (currentState == State.GroundedForever) return;

        if (currentState == State.Perched || currentState == State.FlyingToDoll) return;
        isPlayerInZone = true;
        playerInZone = player;
        currentTarget = player;

        if (currentState == State.Idle || currentState == State.Returning)
        {
            currentState = State.Flying;
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            SetNewFlightTarget();
        }
    }

    public void OnPlayerExitZone()
    {
        // ⭐ แก้ไข 5: ถ้า GroundedForever ไม่ต้องสนตอนผู้เล่นออก
        if (currentState == State.GroundedForever) return;

        isPlayerInZone = false;
        playerInZone = null;
        currentTarget = null;
        currentReturnTarget = GetNearestIdlePosition();
        currentState = State.Returning;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Gizmos.color = Color.cyan;
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
        if (Application.isPlaying && currentGlueHitCount > 0)
        {
            Gizmos.color = Color.magenta;
            float radius = 0.5f + (currentGlueHitCount * 0.2f);
            DrawCircle(transform.position, radius, 20);
        }
        if (idlePoints != null)
        {
            Gizmos.color = Color.blue;
            foreach (var point in idlePoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, 0.5f);
                    Gizmos.DrawLine(transform.position, point.position);
                }
            }
        }
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        Vector3 direction = Vector3.right;
        if (Application.isPlaying && currentState == State.Flying)
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

    // ⭐ เพิ่มกลับมาให้แล้ว: เช็คระยะโจมตี
    bool IsTargetInRange()
    {
        if (currentTarget == null) return false;
        return Vector2.Distance(transform.position, currentTarget.transform.position) <= attackRange;
    }
}