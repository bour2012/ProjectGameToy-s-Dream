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

        if (target != null)
        {
            float heightDiff = Mathf.Abs(target.transform.position.y - transform.position.y);

            if (heightDiff > maxChaseHeightDifference)
            {
                StopHorizontalMovement();
                FaceTarget(target);
                return;
            }

            float directionX = Mathf.Sign(target.transform.position.x - transform.position.x);
            if (stuckTimer >= stuckWaitTime || !CanMoveInDirection(directionX))
            {
                StopHorizontalMovement();
                FaceTarget(target);
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
        float directionX = target.transform.position.x - transform.position.x;
        float moveDirection = Mathf.Sign(directionX);
        movingRight = moveDirection > 0;
        FlipModel(moveDirection);
    }

    protected override void Patrol()
    {
        float direction = movingRight ? 1f : -1f;
        FlipModel(direction);

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

    private void FlipModel(float direction)
    {
        if (Mathf.Abs(direction) < 0.01f) return;
        currentFacingDirection = direction;
    }

    protected override void Chase(GameObject target)
    {
        if (target == null) return;

        float directionX = Mathf.Sign(target.transform.position.x - transform.position.x);
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
}