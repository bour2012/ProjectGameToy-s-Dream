using UnityEngine;
using System.Collections;

public class PatrollingEnemy : Enemy
{
    private enum State { Idle, Patrolling, Chasing, Attacking, Returning, Stuck }
    private State currentState = State.Idle;

    [Header("Visual Settings")]
    public Transform modelTransform;

    [Header("Behavior Settings")]
    public float chaseMemoryDuration = 2f;
    public bool usePatrolRange = false;
    public float maxChaseHeightDifference = 2f;

    [Header("Stuck Detection")]
    public float stuckThreshold = 0.1f;
    public float stuckWaitTime = 1.5f;
    private Vector3 stuckCheckPosition;
    private float stuckTimer = 0f;

    [Header("Patrol Offset")]
    public float patrolLeftOffset = -3f;
    public float patrolRightOffset = 3f;

    [Header("Attack Settings")]
    public float attackCooldown = 2f;
    public float attackDuration = 1f;

    [Header("Collision Check Settings")]
    public float rayOffsetX = 0.5f;

    [Header("Turn Smoothing")]
    [Tooltip("ระยะเวลาขั้นต่ำในการหันกลับตัว (ป้องกันการหันสลับไปมาอย่างรวดเร็ว)")]
    public float minTurnInterval = 0.25f;
    private float lastTurnTime = 0f;

    private bool movingRight = true;
    private float lastSeenTimer = 0f;
    private Vector3 patrolStartPos;
    private float attackTimer = 0f;
    private bool isAttacking = false;
    private bool isEngaged = false;
    private float step;
    private Collider2D col;
    private float currentFacingDirection = 1f;

    protected override void Start()
    {
        base.Start();
        patrolStartPos = transform.position;
        currentState = State.Idle;
        col = GetComponent<Collider2D>();
        stuckCheckPosition = transform.position;

        if (modelTransform != null)
            currentFacingDirection = Mathf.Sign(modelTransform.localScale.x);
    }

    protected void LateUpdate()
    {
        if (modelTransform != null)
        {
            Vector3 scale = modelTransform.localScale;
            scale.x = Mathf.Abs(scale.x) * (currentFacingDirection < 0 ? 1f : -1f);
            modelTransform.localScale = scale;
        }
    }

    protected override void Update()
    {

        isGrounded = IsGrounded();

        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }

        step = moveSpeed * Time.deltaTime;
        CheckIfStuck();

        switch (currentState)
        {
            case State.Idle: HandleIdleState(); break;
            case State.Patrolling: HandlePatrolState(); break;
            case State.Chasing: HandleChaseState(); break;
            case State.Attacking: HandleAttackState(); break;
            case State.Returning: HandleReturnState(); break;
            case State.Stuck: HandleStuckState(); break;
        }
        HandleAnimation();
    }

    protected new void HandleAnimation()
    {
        if (animator == null) return;

        bool isMoving = false;
        if (currentState == State.Patrolling)
        {
            // ในโหมดลาดตระเวน หากทิศทางยังเดินได้ ให้ IsMoving เป็น true ต่อเนื่อง ไม่ตกไป Idle ตอนเลี้ยวกลับ
            isMoving = CanMoveInDirection(movingRight ? 1f : -1f) || (rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.1f);
        }
        else if (currentState == State.Chasing || currentState == State.Returning)
        {
            isMoving = rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.1f;
        }
        else
        {
            isMoving = false;
        }

        animator.SetBool(ANIM_IS_MOVING, isMoving);

        if (isSpecialState)
        {
            animator.SetTrigger(ANIM_IS_SPECIAL02);
            isSpecialState = false;
        }
    }

    private void HandleIdleState()
    {
        StopHorizontalMovement();
        GameObject target = DetectAndLockTarget();
        if (target != null)
        {
            currentState = State.Chasing;
        }
        else if (isGrounded)
        {
            currentState = State.Patrolling;
        }
    }

    private void HandlePatrolState()
    {
        GameObject target = DetectAndLockTarget();
        if (target != null)
        {
            currentState = State.Chasing;
            return;
        }

        if (isGrounded)
        {
            Patrol();
        }
        else
        {
            StopHorizontalMovement();
        }
    }

    private void CheckIfStuck()
    {
        if (currentState == State.Chasing || currentState == State.Returning)
        {
            float distanceMoved = Vector3.Distance(transform.position, stuckCheckPosition);
            if (distanceMoved < stuckThreshold * Time.deltaTime)
            {
                stuckTimer += Time.deltaTime;
            }
            else
            {
                stuckTimer = 0;
            }
            stuckCheckPosition = transform.position;
        }
        else
        {
            stuckTimer = 0;
        }
    }

    private void HandleChaseState()
    {
        GameObject target = DetectAndLockTarget();

        if (target != null && target.activeInHierarchy)
        {
            lastSeenTimer = 0f;
            float heightDiff = Mathf.Abs(target.transform.position.y - transform.position.y);

            if (heightDiff > maxChaseHeightDifference)
            {
                StopHorizontalMovement();
                FaceTarget(target);
                return;
            }

            float distanceX = Mathf.Abs(target.transform.position.x - transform.position.x);
            bool isTooClose = distanceX < 0.5f;

            isChasing = true;

            // หันหน้า (FaceTarget) เฉพาะเมื่อเป้าหมายยังอยู่และไม่ใกล้เกินไป
            if (!isTooClose)
            {
                FaceTarget(target);
            }

            if (isEngaged && attackTimer <= 0)
            {
                currentState = State.Attacking;
                StartCoroutine(AttackCoroutine(target));
            }
            else if (!isEngaged)
            {
                if (isTooClose)
                {
                    StopHorizontalMovement();
                }
                else
                {
                    Chase(target);
                }
            }
        }
        else
        {
            // เป้าหมายคลาดสายตาชั่วคราว: หยุดเดิน และรอให้ครบ chaseMemoryDuration ก่อนเลิกตาม
            StopHorizontalMovement();
            lastSeenTimer += Time.deltaTime;

            if (lastSeenTimer >= chaseMemoryDuration)
            {
                isChasing = false;
                isEngaged = false;
                lastSeenTimer = 0f;
                currentState = usePatrolRange ? State.Returning : State.Patrolling;
            }
        }
    }

    private void HandleStuckState()
    {
        StopHorizontalMovement();
        if (stuckTimer <= 0) currentState = State.Idle;
    }

    private void HandleAttackState()
    {
        StopHorizontalMovement();
    }

    private void HandleReturnState()
    {
        GameObject target = DetectAndLockTarget();
        if (target != null)
        {
            currentState = State.Chasing;
            return;
        }

        float distanceToHome = Vector2.Distance(transform.position, patrolStartPos);

        if (distanceToHome <= 0.5f)
        {
            currentState = State.Patrolling;
            Patrol();
            return;
        }

        float direction = Mathf.Sign(patrolStartPos.x - transform.position.x);

        if (!CanMoveInDirection(direction) || stuckTimer >= stuckWaitTime)
        {
            StopHorizontalMovement();
            if (stuckTimer >= stuckWaitTime)
            {
                currentState = State.Patrolling;
                stuckTimer = 0f;
            }
            return;
        }

        FlipModel(direction);
        movingRight = direction > 0;
        if (rb != null)
            rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        else
            transform.position += Vector3.right * (direction * step);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (currentState == State.Chasing && collision.gameObject == currentTarget)
        {
            isEngaged = true;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject == currentTarget)
        {
            isEngaged = false;
        }
    }

    private IEnumerator AttackCoroutine(GameObject target)
    {
        isAttacking = true;
        if (animator != null) animator.SetTrigger("Attack");
        yield return new WaitForSeconds(attackDuration);

        attackTimer = attackCooldown;
        isAttacking = false;
        currentState = State.Chasing;
    }

    private void FaceTarget(GameObject target)
    {
        if (target == null) return;
        float directionX = target.transform.position.x - transform.position.x;
        if (Mathf.Abs(directionX) < 0.2f) return;
        float moveDirection = Mathf.Sign(directionX);
        movingRight = moveDirection > 0;
        FlipModel(moveDirection);
    }

    protected override void Patrol()
    {
        float direction = movingRight ? 1f : -1f;

        if (!CanMoveInDirection(direction))
        {
            movingRight = !movingRight;
            direction = movingRight ? 1f : -1f;
            FlipModel(direction);

            // ถ้าทิศทางใหม่เดินได้ ให้ใส่ความเร็วเดินต่อทันที ไม่หยุดชะงักความเร็วเป็น 0 เพื่อไม่ให้ตกไปท่า Idle
            if (CanMoveInDirection(direction))
            {
                if (rb != null) rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
                else transform.position += Vector3.right * (step * direction);
            }
            else
            {
                StopHorizontalMovement();
            }
            return;
        }

        FlipModel(direction);

        if (rb != null) rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        else transform.position += Vector3.right * (step * direction);

        if (usePatrolRange)
        {
            float leftBound = patrolStartPos.x + patrolLeftOffset;
            float rightBound = patrolStartPos.x + patrolRightOffset;
            if (transform.position.x >= rightBound && movingRight)
            {
                movingRight = false;
                direction = -1f;
                FlipModel(direction);
                if (rb != null) rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
            }
            else if (transform.position.x <= leftBound && !movingRight)
            {
                movingRight = true;
                direction = 1f;
                FlipModel(direction);
                if (rb != null) rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
            }
        }
    }

    private void FlipModel(float direction)
    {
        if (Mathf.Abs(direction) < 0.01f) return;
        float targetDir = Mathf.Sign(direction);
        if (Mathf.Approximately(targetDir, currentFacingDirection)) return;

        // ป้องกันการสะบัดหันซ้าย-ขวารัวๆ ถี่เกินไป
        if (Time.time - lastTurnTime < minTurnInterval) return;

        currentFacingDirection = targetDir;
        lastTurnTime = Time.time;
    }

    protected override void Chase(GameObject target)
    {
        if (target == null) return;

        float diffX = target.transform.position.x - transform.position.x;
        if (Mathf.Abs(diffX) < 0.2f)
        {
            StopHorizontalMovement();
            return;
        }

        float directionX = Mathf.Sign(diffX);
        FlipModel(directionX);

        if (!CanMoveInDirection(directionX))
        {
            StopHorizontalMovement();
            return;
        }

        if (rb != null)
            rb.linearVelocity = new Vector2(directionX * moveSpeed, rb.linearVelocity.y);
        else
            transform.position += Vector3.right * (step * directionX);
    }

    private bool CanMoveInDirection(float direction)
    {
        Bounds bounds = col.bounds;
        Vector2 dirVec = (direction > 0) ? Vector2.right : Vector2.left;

        Vector2 wallCheckOrigin = new Vector2(direction > 0 ? bounds.max.x : bounds.min.x, bounds.center.y);
        RaycastHit2D wallHit = Physics2D.Raycast(wallCheckOrigin, dirVec, 0.5f, obstacleLayers);

        Vector2 groundCheckOrigin = new Vector2(direction > 0 ? bounds.max.x + 0.2f : bounds.min.x - 0.2f, bounds.center.y);
        float checkDistance = bounds.extents.y + 1f;
        RaycastHit2D groundHit = Physics2D.Raycast(groundCheckOrigin, Vector2.down, checkDistance, groundLayer);

        return (wallHit.collider == null && groundHit.collider != null);
    }

    protected override GameObject DetectAndLockTarget()
    {
        // ถ้าล็อคเป้าหมายเดิมอยู่แล้ว (กำลังไล่ตาม) ให้เช็คระยะและสายตา
        if (currentTarget != null)
        {
            float dist = Vector2.Distance(transform.position, currentTarget.transform.position);

            if (((1 << currentTarget.layer) & detectionLayers) != 0 &&
                dist <= detectionRange &&
                HasLineOfSightFromCenter(currentTarget))
            {
                return currentTarget;
            }
            else
            {
                currentTarget = null;
            }
        }

        // ตรวจจับเป้าหมายใหม่: ต้องอยู่ "ด้านหน้า" ที่มอนสเตอร์กำลังหันไปเท่านั้น (มองไม่เห็นข้างหลัง)
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRange, detectionLayers);
        foreach (var hit in hits)
        {
            GameObject candidate = hit.gameObject;
            if (IsTargetInFront(candidate) && HasLineOfSightFromCenter(candidate))
            {
                currentTarget = candidate;
                return currentTarget;
            }
        }
        return null;
    }

    private bool IsTargetInFront(GameObject target)
    {
        if (target == null) return false;
        float diffX = target.transform.position.x - transform.position.x;
        // currentFacingDirection: 1f = ขวา, -1f = ซ้าย
        // ต้องอยู่ทิศเดียวกับที่มอนสเตอร์หันหน้าไปเท่านั้น
        return (diffX > 0 && currentFacingDirection > 0) || (diffX < 0 && currentFacingDirection < 0);
    }

    private bool HasLineOfSightFromCenter(GameObject target)
    {
        if (target == null) return false;
        Vector2 origin = (col != null) ? (Vector2)col.bounds.center : (Vector2)transform.position + Vector2.up * 0.5f;

        Vector2 targetPos = target.transform.position;
        if (target.TryGetComponent<Collider2D>(out var targetCol))
        {
            targetPos = targetCol.bounds.center;
        }

        Vector2 direction = (targetPos - origin).normalized;
        float distance = Vector2.Distance(origin, targetPos);

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, distance, obstacleLayers);
        return hit.collider == null;
    }
}