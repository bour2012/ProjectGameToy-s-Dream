using UnityEngine;

public class PatrollingEnemy : Enemy
{
    public float patrolRange = 5f;
    public float chaseCooldown = 2f;

    private bool movingRight = true;
    private float chaseTimer;

    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float wallCheckDistance = 0.5f;
    [SerializeField] private float groundCheckDistance = 1f;

    protected override void Start()
    {
        base.Start(); // สำคัญ! จะได้ค่า initialPosition จาก Enemy
    }

    protected override void Update()
    {
        GameObject target = DetectTarget();

        if (target != null)
        {
            isChasing = true;
            Chase(target);
            chaseTimer = 0f;
        }
        else if (isChasing)
        {
            chaseTimer += Time.deltaTime;
            if (chaseTimer >= chaseCooldown) isChasing = false;
        }

        if (!isChasing) Patrol();
    }

    protected override void Patrol()
    {
        // เดิน
        transform.position += Vector3.right * (moveSpeed * Time.deltaTime * (movingRight ? 1 : -1));

        // ตรวจสอบกำแพง
        Vector2 dir = movingRight ? Vector2.right : Vector2.left;
        RaycastHit2D wallHit = Physics2D.Raycast(transform.position, dir, wallCheckDistance, groundLayer);

        // ตรวจสอบว่ามีพื้นด้านล่างต่อไปหรือไม่
        Vector2 downDir = Vector2.down;
        Vector2 frontPos = new Vector2(transform.position.x + (movingRight ? 0.5f : -0.5f), transform.position.y);
        RaycastHit2D groundHit = Physics2D.Raycast(frontPos, downDir, groundCheckDistance, groundLayer);

        if (wallHit.collider != null || groundHit.collider == null)
        {
            // ถ้าชนกำแพง หรือ ข้างหน้าไม่มีพื้น → เปลี่ยนทิศ
            movingRight = !movingRight;
        }
    }
}
