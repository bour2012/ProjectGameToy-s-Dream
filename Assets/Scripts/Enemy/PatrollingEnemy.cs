using UnityEngine;

public class PatrollingEnemy : Enemy
{

    public float chaseCooldown = 2f;
    public bool usePatrolRange = false;

    private bool movingRight = true;
    private float chaseTimer;

    [Header("PatrolOffset")]
    private Vector3 patrolStartPos;
    public float patrolLeftOffset = -3f;   // ระยะซ้ายจากจุดเริ่ม
    public float patrolRightOffset = 3f;   // ระยะขวาจากจุดเริ่ม

    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float wallCheckDistance = 0.5f;
    [SerializeField] private float groundCheckDistance = 1f;


    protected override void Start()
    {
        base.Start();
        patrolStartPos = transform.position; // บันทึกจุดเริ่มต้น
    }

    protected override void Update()
    {
        GameObject target = DetectTarget();

        // เช็กว่ามีกำแพงบังหรือไม่ (เหมือน StationaryEnemy)
        if (target != null )
        {


            if (HasLineOfSight(target))
            {
                isChasing = true;
                Chase(target);
                chaseTimer = 0f;
            
            }
            else
            {
                // ผู้เล่นอยู่หลังกำแพง → เหมือนตรวจไม่เจอ
                target = null;
                //Debug.Log("Lost sight due to obstacle");
            }

        }
        if (target == null)
        {
            if (isChasing)
            {
                chaseTimer += Time.deltaTime;
                if (chaseTimer >= chaseCooldown)
                {
                    isChasing = false;
                    //currentTarget = null; // ปลดล็อกเป้าหมายเมื่อหยุดไล่ล่า
                }
            }

            if (!isChasing) Patrol();
        }
    }


    //private bool HasLineOfSight(GameObject target)
    //{
   
    //    Vector2 direction = (target.transform.position - transform.position).normalized;
    //    float distance = Vector2.Distance(transform.position, target.transform.position);

    //    // Raycast ตรวจหาสิ่งกีดขวางระหว่าง Enemy และ Player
    //    RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, obstacleLayers);

    //    if (hit.collider != null)
    //    {
    //        // เจอกำแพงหรือสิ่งกีดขวาง → มองไม่เห็น Player
    //        return false;
    //    }

    //    return true; // ไม่มีสิ่งกีดขวาง → มองเห็น
    
    //}
    protected override void Patrol()
    {
        if (usePatrolRange)
        {
            float step = moveSpeed * Time.deltaTime;
            transform.position += Vector3.right * (step * (movingRight ? 1 : -1));

            float leftBound = patrolStartPos.x + patrolLeftOffset;
            float rightBound = patrolStartPos.x + patrolRightOffset;

            // กลับทิศเมื่อถึงขอบ
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
        else
        {
            // พฤติกรรมเดิม
            Vector3 direction = (initialPosition - transform.position).normalized;
            float step = moveSpeed * Time.deltaTime;
            transform.position += Vector3.right * (step * (movingRight ? 1 : -1));

            Vector2 dir = movingRight ? Vector2.right : Vector2.left;
            RaycastHit2D wallHit = Physics2D.Raycast(transform.position, dir, wallCheckDistance, groundLayer);

            Vector2 downDir = Vector2.down;
            Vector2 frontPos = new Vector2(transform.position.x + (movingRight ? 0.5f : -0.5f), transform.position.y);
            RaycastHit2D groundHit = Physics2D.Raycast(frontPos, downDir, groundCheckDistance, groundLayer);

            if (wallHit.collider != null || groundHit.collider == null)
            {
                movingRight = !movingRight;
            }
        }
    }
}
