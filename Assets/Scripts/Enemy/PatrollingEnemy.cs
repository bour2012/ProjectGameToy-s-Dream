using UnityEngine;
using System.Collections;

public class PatrollingEnemy : Enemy
{
    // ▼▼▼ เพิ่ม State "Returning" ▼▼▼
    private enum State { Idle, Patrolling, Chasing, Attacking, Returning }
    private State currentState = State.Idle;
    // ▲▲▲ สิ้นสุดส่วนที่เพิ่ม ▲▲▲

    [Header("Behavior Settings")]
    public float chaseMemoryDuration = 2f;
    public bool usePatrolRange = false;

    [Header("Patrol Offset (If UsePatrolRange is True)")]
    public float patrolLeftOffset = -3f;
    public float patrolRightOffset = 3f;

    [Header("Attack Settings")]
    public float attackCooldown = 2f;
    public float attackDuration = 1f;

    // Private variables
    private bool movingRight = true;
    private float lastSeenTimer = 0f;
    private Vector3 patrolStartPos;
    private float attackTimer = 0f;
    private bool isAttacking = false;
    private bool isEngaged = false;
    private float step;

    protected override void Start()
    {
        base.Start();
        patrolStartPos = transform.position;
        currentState = State.Idle; // เริ่มต้นที่ Idle
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

        // --- State Machine Logic ---
        switch (currentState)
        {
            case State.Idle:
                HandleIdleState();
                break;
            case State.Patrolling:
                HandlePatrolState();
                break;
            case State.Chasing:
                HandleChaseState();
                break;
            case State.Attacking:
                HandleAttackState();
                break;
            case State.Returning:
                HandleReturnState();
                break;
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

    private void HandleChaseState()
    {
        GameObject target = DetectAndLockTarget();
        if (target != null)
        {
            isChasing = true; // (เก็บไว้สำหรับ OnCollisionStay2D)
            lastSeenTimer = 0f;
            FaceTarget(target);

            if (isEngaged)
            {
                StopHorizontalMovement();
                if (attackTimer <= 0)
                {
                    currentState = State.Attacking;
                    StartCoroutine(AttackCoroutine(target));
                }
            }
            else
            {
                Chase(target);
            }
        }
        else // ถ้าไม่เจอเป้าหมาย
        {
            isEngaged = false;
            lastSeenTimer += Time.deltaTime;
            if (lastSeenTimer >= chaseMemoryDuration)
            {
                isChasing = false;
                currentState = State.Returning; // <-- เปลี่ยนเป็น "กลับบ้าน"
            }
        }
    }

    private void HandleAttackState()
    {
        // Coroutine จะจัดการเปลี่ยน State กลับไปเป็น Chasing เมื่อโจมตีเสร็จ
        StopHorizontalMovement();
    }

    private void HandleReturnState()
    {
        // ถ้าเจอผู้เล่นอีกครั้งระหว่างทางกลับบ้าน -> ไล่ล่าทันที
        GameObject target = DetectAndLockTarget();
        if (target != null)
        {
            currentState = State.Chasing;
            return;
        }

        // คำนวณระยะทางถึงบ้าน
        float distanceToHome = Vector2.Distance(transform.position, patrolStartPos);

        if (distanceToHome > 1f)
        {
            // ยังไม่ถึงบ้าน -> เดินกลับบ้าน
            float direction = Mathf.Sign(patrolStartPos.x - transform.position.x);
            if (CanMoveInDirection(direction))
            {
                FlipSprite(direction);
              
                    transform.position += Vector3.right * (direction * step);
                
            }
            else
            {
                StopHorizontalMovement(); // หยุดถ้าเจอทางตันระหว่างทางกลับ
            }
        }
        else
        {
            // ถึงบ้านแล้ว -> กลับไป Idle
            //transform.position = patrolStartPos;
     
            currentState = State.Patrolling;
        }
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

        if (!usePatrolRange && !CanMoveInDirection(direction))
        {
            movingRight = !movingRight;
            return;
        }

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        }
        else
        {
            transform.position += Vector3.right * (step * direction);
        }

        if (usePatrolRange)
        {
            float leftBound = patrolStartPos.x + patrolLeftOffset;
            float rightBound = patrolStartPos.x + patrolRightOffset;

            if (transform.position.x >= rightBound)
            {
                movingRight = false;
                transform.position = new Vector3(rightBound, transform.position.y, transform.position.z);
            }
            else if (transform.position.x <= leftBound)
            {
                movingRight = true;
                transform.position = new Vector3(leftBound, transform.position.y, transform.position.z);
            }
        }
    }

    protected override void Chase(GameObject target)
    {
        if (target == null) return;
        float directionX = Mathf.Sign(target.transform.position.x - transform.position.x);

        if (CanMoveInDirection(directionX))
        {
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(directionX * moveSpeed, rb.linearVelocity.y);
            }
            else
            {
                transform.position += Vector3.right * (directionX * step);
            }
        }
        else
        {
            StopHorizontalMovement();
        }
    }

    private bool CanMoveInDirection(float direction)
    {
        Vector2 dirVec = (direction > 0) ? Vector2.right : Vector2.left;

        Vector2 wallCheckOrigin = (Vector2)transform.position + (dirVec * 0.5f);
        RaycastHit2D wallHit = Physics2D.Raycast(wallCheckOrigin, dirVec, wallCheckDistance, obstacleLayers);

        Vector2 groundCheckOrigin = (Vector2)transform.position + (dirVec * 0.5f);
        RaycastHit2D groundHit = Physics2D.Raycast(groundCheckOrigin, Vector2.down, groundCheckDistance, groundLayer);

#if UNITY_EDITOR
        Debug.DrawRay(wallCheckOrigin, dirVec * wallCheckDistance, wallHit.collider ? Color.red : Color.green);
        Debug.DrawRay(groundCheckOrigin, Vector2.down * groundCheckDistance, groundHit.collider ? Color.blue : Color.yellow);
#endif

        return (wallHit.collider == null && groundHit.collider != null);
    }
}