using UnityEngine;
using System.Collections;

public class PatrollingEnemy : Enemy
{
    // ▼▼▼ เพิ่ม State "Returning" ▼▼▼
    private enum State { Idle, Patrolling, Chasing, Attacking, Returning , Stuck }
    private State currentState = State.Idle;
    // ▲▲▲ สิ้นสุดส่วนที่เพิ่ม ▲▲▲

    [Header("Behavior Settings")]
    public float chaseMemoryDuration = 2f;
    public bool usePatrolRange = false;
    public float maxChaseHeightDifference = 2f; // สูง/ต่ำ เกินนี้จะไม่ไล่

    [Header("Stuck Detection")]
    public float stuckThreshold = 0.1f; // ระยะเคลื่อนที่ขั้นต่ำ
    public float stuckWaitTime = 1.5f; // ติดอยู่นานเท่าไหร่ถึงจะเลิกเดิน
    private Vector3 lastPosition;
    private float stuckTimer = 0f;

    [Header("Patrol Offset (If UsePatrolRange is True)")]
    public float patrolLeftOffset = -3f;
    public float patrolRightOffset = 3f;

    [Header("Attack Settings")]
    public float attackCooldown = 2f;
    public float attackDuration = 1f;

    [Header("Collision Check Settings")]
    //public float wallCheckDistance = 0.6f;   // ระยะเช็คกำแพง (ปรับใน Inspector ได้)
    //public float groundCheckDistance = 0.6f; // ระยะเช็คพื้น (ปรับใน Inspector ได้)
    public float rayOffsetX = 0.5f;          // จุดปล่อยแสงห่างจากตัวเท่าไหร่

    // Private variables
    private bool movingRight = true;
    private float lastSeenTimer = 0f;
    private Vector3 patrolStartPos;
    private float attackTimer = 0f;
    private bool isAttacking = false;
    private bool isEngaged = false;
    private float step;
    private Collider2D col;
    protected override void Start()
    {
        base.Start();
        patrolStartPos = transform.position;
        currentState = State.Idle; // เริ่มต้นที่ Idle
        col = GetComponent<Collider2D>();
    }

    protected override void Update()
    {
        // 1. ตรวจสอบสถานะพื้นก่อน
        isGrounded = IsGrounded();

        // 2. อัปเดต Cooldown
        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }

        // 3. คำนวณ step
        step = moveSpeed * Time.deltaTime;
        CheckIfStuck();
        // --- State Machine Logic ---
        switch (currentState)
        {
            case State.Idle: HandleIdleState(); break;
            case State.Patrolling: HandlePatrolState(); break;
            case State.Chasing: HandleChaseState(); break;
            case State.Attacking: HandleAttackState(); break;
            case State.Returning: HandleReturnState(); break;
            case State.Stuck: HandleStuckState(); break;
        }
    }

    // --- State Handlers ---

    private void HandleIdleState()
    {
        // หยุดนิ่งและมองหาเป้าหมาย
        StopHorizontalMovement();
        GameObject target = DetectAndLockTarget();
        if (target != null)
        {
            currentState = State.Chasing;
        }
        // ถ้าไม่เจอ ก็จะ Patrolling ในเฟรมถัดไป
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
            float distanceMoved = Vector3.Distance(transform.position, lastPosition);
            if (distanceMoved < stuckThreshold * Time.deltaTime)
            {
                stuckTimer += Time.deltaTime;
            }
            else
            {
                stuckTimer = 0;
            }
            lastPosition = transform.position;
        }
        else
        {
            stuckTimer = 0;
        }
    }
    private void HandleChaseState()
    {
        GameObject target = DetectAndLockTarget();

        if (target != null)
        {
            float heightDiff = Mathf.Abs(target.transform.position.y - transform.position.y);

            // 1. ตรวจสอบว่าเป้าหมายอยู่สูงหรือต่ำเกินไปหรือไม่
            if (heightDiff > maxChaseHeightDifference)
            {
                StopHorizontalMovement();
                FaceTarget(target); // ยืนจ้องแทน
                return;
            }

            // 2. ตรวจสอบว่าติดกำแพงหรือตกเหวหรือไม่
            float directionX = Mathf.Sign(target.transform.position.x - transform.position.x);
            if (stuckTimer >= stuckWaitTime || !CanMoveInDirection(directionX))
            {
                StopHorizontalMovement();
                FaceTarget(target); // ยืนจ้อง
                return;
            }

            isChasing = true;
            lastSeenTimer = 0f;
            FaceTarget(target);

            if (isEngaged && attackTimer <= 0)
            {
                currentState = State.Attacking;
                StartCoroutine(AttackCoroutine(target));
            }
            else if (!isEngaged)
            {
                Chase(target);
            }
        }
        else
        {
            lastSeenTimer += Time.deltaTime;
            if (lastSeenTimer >= chaseMemoryDuration)
            {
                isChasing = false;
                isEngaged = false;
                currentState = usePatrolRange ? State.Returning : State.Patrolling;
            }
        }
    }
    private void HandleStuckState()
    {
        StopHorizontalMovement();
        // รอสักพัก หรือจนกว่าเป้าหมายจะหลุดระยะ
        if (stuckTimer <= 0) currentState = State.Idle;
    }
    private void HandleAttackState()
    {
        // Coroutine จะจัดการเปลี่ยน State กลับไปเป็น Chasing เมื่อโจมตีเสร็จ
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

        // ถ้ากลับถึงบ้านแล้ว (ระยะห่างน้อยกว่า 0.5)
        if (distanceToHome <= 0.5f)
        {
            currentState = State.Patrolling;
            return;
        }

        // คำนวณทิศทางกลับบ้าน
        float direction = Mathf.Sign(patrolStartPos.x - transform.position.x);

        // ตรวจสอบว่าทางกลับบ้านโดนตัดขาดหรือไม่
        if (!CanMoveInDirection(direction) || stuckTimer >= stuckWaitTime)
        {
            // ไม่ต้องรีเซ็ต patrolStartPos; ให้มันหยุดเดิน (Idle) หรือพยายาม Patrol เท่าที่พื้นที่อำนวย
            StopHorizontalMovement();

            // ถ้าติดอยู่นาน ให้ลองเปลี่ยนไป Patrol ดู เผื่อว่าทิศทาง Patrol ปกติจะไปได้
            if (stuckTimer >= stuckWaitTime)
            {
                currentState = State.Patrolling;
                stuckTimer = 0f; // รีเซ็ตตัวนับติดขัด
            }
            return;
        }

        // การเคลื่อนที่กลับบ้าน
        FlipSprite(direction);
        if (rb != null)
            rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        else
            transform.position += Vector3.right * (direction * step);
    }

    // --- End State Handlers ---

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
        Debug.Log($"{enemyName} is attacking {target.name}");
        yield return new WaitForSeconds(attackDuration);

        attackTimer = attackCooldown;
        isAttacking = false;
        currentState = State.Chasing; // กลับไปสถานะไล่ล่า (เพื่อเช็คระยะอีกครั้ง)
    }

    private void FaceTarget(GameObject target)
    {
        float directionX = target.transform.position.x - transform.position.x;
        float moveDirection = Mathf.Sign(directionX);
        movingRight = moveDirection > 0;
        FlipSprite(moveDirection);
    }

    protected override void Patrol()
    {
        float direction = movingRight ? 1f : -1f;
        FlipSprite(direction);
        if (!CanMoveInDirection(direction))
        {
            movingRight = !movingRight;
            return;
        }

        if (rb != null) rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        else transform.position += Vector3.right * (step * direction);

        if (usePatrolRange)
        {
            float leftBound = patrolStartPos.x + patrolLeftOffset;
            float rightBound = patrolStartPos.x + patrolRightOffset;
            if (transform.position.x >= rightBound) movingRight = false;
            else if (transform.position.x <= leftBound) movingRight = true;
        }
    }

    protected override void Chase(GameObject target)
    {
        if (target == null) return;

        float directionX = Mathf.Sign(target.transform.position.x - transform.position.x);
        FlipSprite(directionX);

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

        // 1. เช็คกำแพง (เหมือนเดิม)
        Vector2 wallCheckOrigin = new Vector2(direction > 0 ? bounds.max.x : bounds.min.x, bounds.center.y);
        RaycastHit2D wallHit = Physics2D.Raycast(wallCheckOrigin, dirVec, 0.5f, obstacleLayers);

        // 2. เช็คพื้น (แก้ตรงนี้!)
        // เปลี่ยนจุดเริ่มจาก "เท้า" (min.y) ขึ้นมาที่ "เอว" (center.y) แทน
        // เพื่อให้มั่นใจว่าเส้น Raycast จะพุ่ง "ทะลุ" พื้นแน่นอน ไม่ใช่เริ่มที่ผิวพื้น
        Vector2 groundCheckOrigin = new Vector2(direction > 0 ? bounds.max.x + 0.2f : bounds.min.x - 0.2f, bounds.center.y);

        // ยิงลงมายาวเท่ากับ "ครึ่งตัว + ระยะเช็คเพิ่ม" (bounds.extents.y + 1f)
        float checkDistance = bounds.extents.y + 1f;
        RaycastHit2D groundHit = Physics2D.Raycast(groundCheckOrigin, Vector2.down, checkDistance, groundLayer);

        // --- เพิ่มบรรทัดนี้เพื่อดูเส้นจริงในหน้า Scene ---
        Debug.DrawRay(wallCheckOrigin, dirVec * 0.5f, Color.red); // เส้นแดง = เช็คกำแพง
        Debug.DrawRay(groundCheckOrigin, Vector2.down * checkDistance, Color.green); // เส้นเขียว = เช็คพื้น
        // ---------------------------------------------

        return (wallHit.collider == null && groundHit.collider != null);
    }
}