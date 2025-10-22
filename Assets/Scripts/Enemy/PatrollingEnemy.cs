using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class PatrollingEnemy : Enemy
{
    [Header("Behavior Settings")]
    [Tooltip("ระยะเวลาที่ศัตรูจะยังคงไล่ตาม แม้จะคลาดสายตาจากผู้เล่น (วินาที)")]
    public float chaseMemoryDuration = 2f;
    [Tooltip("True: ใช้ระยะลาดตระเวนจากจุดเริ่มต้น (Patrol Offset) | False: เดินไปเรื่อยๆ จนกว่าจะเจอเหวหรือกำแพง")]
    public bool usePatrolRange = false;

    [Header("Patrol Offset (If UsePatrolRange is True)")]
    public float patrolLeftOffset = -3f;
    public float patrolRightOffset = 3f;


    [Header("Attack Settings")]
    [Tooltip("ระยะที่ศัตรูจะหยุดเดินและเริ่มโจมตี")]
    public float attackRange = 1.5f;
    [Tooltip("ระยะเวลาคูลดาวน์ระหว่างการโจมตี (วินาที)")]
    public float attackCooldown = 2f;
    [Tooltip("ระยะเวลาที่ใช้ในการโจมตี (ความยาวของ Animation)")]
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
    }

    protected override void Update()
    {
        isGrounded = IsGrounded();

        // 2. อัปเดต Cooldown
        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }

        // 3. คำนวณ step (ย้ายมาจากโค้ดเก่า)
        step = moveSpeed * Time.deltaTime;

        GameObject target = DetectAndLockTarget();

        if (target != null) // --- ถ้าเจอเป้าหมาย ---
        {
            isChasing = true;
            lastSeenTimer = 0f;
            FaceTarget(target);

            if (isAttacking)
            {
                StopHorizontalMovement(); // ใช้ฟังก์ชันจากคลาสแม่
                return;
            }

            if (isEngaged)
            {
                StopHorizontalMovement(); // ใช้ฟังก์ชันจากคลาสแม่
                if (attackTimer <= 0)
                {
                    StartCoroutine(AttackCoroutine(target));
                }
            }
            else
            {
                Chase(target);
            }
        }
        else // --- ถ้าไม่เจอเป้าหมาย ---
        {
            isEngaged = false;
            if (isAttacking)
            {
                StopCoroutine("AttackCoroutine");
                isAttacking = false;
            }

            if (isChasing)
            {
                lastSeenTimer += Time.deltaTime;
                if (lastSeenTimer >= chaseMemoryDuration)
                {
                    isChasing = false;
                }
            }

            if (!isChasing)
            {
                // "ด่านตรวจ" ที่คุณต้องการ:
                if (isGrounded)
                {
                    Patrol();
                }
                else
                {
                    StopHorizontalMovement(); // หยุดนิ่งถ้าลอยอยู่
                }
            }
        }
    }
    private void OnCollisionStay2D(Collision2D collision)
    {
        // ทำหน้าที่แค่ "รายงาน" ว่ากำลังชนเป้าหมาย
        if (isChasing && collision.gameObject == currentTarget)
        {
            isEngaged = true;
        }
    }
    private void OnCollisionExit2D(Collision2D collision)
    {
        // ทำหน้าที่ "รายงาน" ว่าเลิกชนเป้าหมายแล้ว
        if (collision.gameObject == currentTarget)
        {
            isEngaged = false;
        }
    }
    private IEnumerator AttackCoroutine(GameObject target)
    {
        isAttacking = true;

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
        Debug.Log($"{enemyName} is attacking {target.name}");
        // รอให้ Animation จบ
        yield return new WaitForSeconds(attackDuration);

        // รีเซ็ตคูลดาวน์และสถานะ
        attackTimer = attackCooldown;
        isAttacking = false;
    }

    /// <summary>
    /// ฟังก์ชันใหม่สำหรับหันหน้าหาเป้าหมาย
    /// </summary>
    private void FaceTarget(GameObject target)
    {

        float directionX = target.transform.position.x - transform.position.x;
        float moveDirection = Mathf.Sign(directionX);
        movingRight = moveDirection > 0;
        FlipSprite(moveDirection);

    }

    protected override void Patrol()
    {
        float step = moveSpeed * Time.deltaTime;
        float direction = movingRight ? 1f : -1f;

        FlipSprite(direction);

        if (!usePatrolRange && !CanMoveInDirection(direction))
        {
            movingRight = !movingRight;
            return;
        }

        transform.position += Vector3.right * (step * direction);

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

        bool canMove = CanMoveInDirection(directionX);

        if (canMove)
        {
            Vector3 moveDir = Vector3.right * directionX;
            transform.position += moveDir * moveSpeed * Time.deltaTime;
        }
    }
   
    private bool CanMoveInDirection(float direction)
    {
        Vector2 dirVec = (direction > 0) ? Vector2.right : Vector2.left;

        Vector2 wallCheckOrigin = (Vector2)transform.position + (dirVec * 0.5f);
        RaycastHit2D wallHit = Physics2D.Raycast(wallCheckOrigin, dirVec, wallCheckDistance, obstacleLayers);

        Vector2 groundCheckOrigin = (Vector2)transform.position + (dirVec * 0.5f);
        RaycastHit2D groundHit = Physics2D.Raycast(groundCheckOrigin, Vector2.down, groundCheckDistance, groundLayer);

        Debug.DrawRay(wallCheckOrigin, dirVec * wallCheckDistance, wallHit.collider ? Color.red : Color.green);
        Debug.DrawRay(groundCheckOrigin, Vector2.down * groundCheckDistance, groundHit.collider ? Color.blue : Color.yellow);

        return (wallHit.collider == null && groundHit.collider != null);
    }
}