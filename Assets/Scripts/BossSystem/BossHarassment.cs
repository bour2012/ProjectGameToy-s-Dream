using UnityEngine;
using System.Collections;

public class BossHarassment : MonoBehaviour
{
    [Header("Settings - การมองเห็น")]
    public float sightRange = 10f;
    public LayerMask whatIsPlayer;
    public LayerMask whatIsObstacle;
    public Transform playerTransform;

    [Header("Settings - การโจมตี")]
    public float warningTime = 1.5f;
    public float attackSpeed = 15f;
    public float returnSpeed = 5f;
    public float knockbackForce = 10f;
    
    [Header("Safety Settings (แก้บอสค้าง)")]
    public float maxAttackDuration = 3.0f; // ถ้าพุ่งนานเกินนี้ให้กลับเลย (กันบอสวิ่งชนกำแพงค้าง)

    [Header("References")]
    public GameObject alertIcon;

    // State Machine
    private enum State { Idle, Warning, Attacking, Returning, Cooldown }
    [SerializeField]
    private State currentState = State.Idle;

    private Vector2 startPosition; // จุดที่เริ่มบินออกมา (จุด Patrol เดิม)
    private float warningTimer;
    private Vector2 targetPosition;
    private BossController bossController;
    
    [Header("Tuning")]
    public float attackCooldown = 2f; 
    private float cooldownTimer = 0f;
    private float attackDurationTimer = 0f; // ตัวนับเวลาตอนพุ่ง

    void Start()
    {
        startPosition = transform.position;
        if (alertIcon != null) alertIcon.SetActive(false);
        bossController = GetComponent<BossController>();
    }

    void Update()
    {
        //Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 1.5f);
        //foreach (var hit in hits)
        //{
        //    // เช็คชื่อให้ตรงกับชื่อใน Hierarchy ของคุณเป๊ะๆ นะครับ
        //    if (hit.gameObject.name == "Player")
        //    {
        //        Debug.Log("🔴 เจอ Player ในระยะประชิด! (Overlap เจอ แต่ Trigger อาจพัง)");
        //    }
        //}
        switch (currentState)
        {
            case State.Idle:
                CheckForPlayer();
                break;

            case State.Warning:
                CountDownToAttack();
                break;

            case State.Attacking:
                MoveToTarget();
                break;

            case State.Returning:
                ReturnToStart();
                break;
            
            case State.Cooldown:
                HandleCooldown();
                break;
        }
    }

    // --- Logic ---

    void CheckForPlayer()
    {
        if (Vector2.Distance(transform.position, playerTransform.position) > sightRange) return;

        Vector2 direction = (playerTransform.position - transform.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, sightRange, whatIsPlayer | whatIsObstacle);

        if (hit.collider != null && hit.collider.CompareTag("Player"))
        {
            StartWarning();
        }
    }

    void StartWarning()
    {
        currentState = State.Warning;
        warningTimer = warningTime;

        if (alertIcon != null) alertIcon.SetActive(true);

        // 1. บันทึกจุด Patrol ปัจจุบันไว้ เพื่อให้บินกลับมาถูกที่
        startPosition = transform.position;

        // 2. หยุดระบบ Patrol ของ BossController ชั่วคราว
        if (bossController != null)
        {
            bossController.TakeManualControl(true);
            bossController.SetFacingDirection((playerTransform.position - transform.position));
        }
    }

    void CountDownToAttack()
    {
        // เช็คซ้ำว่าผู้เล่นยังอยู่ไหม (ถ้าหลบหลังกำแพงทัน ให้ยกเลิก)
        Vector2 direction = (playerTransform.position - transform.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, sightRange, whatIsPlayer | whatIsObstacle);

        if (hit.collider == null || !hit.collider.CompareTag("Player"))
        {
            CancelAttack();
            return;
        }

        warningTimer -= Time.deltaTime;
        if (warningTimer <= 0)
        {
            StartAttack();
        }
    }

    void CancelAttack()
    {
        currentState = State.Idle;
        if (alertIcon != null) alertIcon.SetActive(false);
        if (bossController != null) bossController.TakeManualControl(false);
    }

    void StartAttack()
    {
        currentState = State.Attacking;
        if (alertIcon != null) alertIcon.SetActive(false);
        
        targetPosition = playerTransform.position;
        attackDurationTimer = maxAttackDuration; // เริ่มนับถอยหลังกันบอสค้าง

        if (bossController != null)
        {
            bossController.TakeManualControl(true);
            bossController.SetFacingDirection((targetPosition - (Vector2)transform.position));
        }
    }

    void MoveToTarget()
    {
        // 1. ขยับไปหาเป้าหมาย
        transform.position = Vector2.MoveTowards(transform.position, targetPosition, attackSpeed * Time.deltaTime);
        if (bossController != null) bossController.SetFacingDirection((targetPosition - (Vector2)transform.position));

        // 2. ลดเวลา Timeout
        attackDurationTimer -= Time.deltaTime;

        // 3. เช็คว่าถึงเป้า หรือ หมดเวลา (ป้องกันการวิ่งชนผู้เล่นแล้วค้างไม่ยอมกลับ)
        if (Vector2.Distance(transform.position, targetPosition) < 0.1f || attackDurationTimer <= 0f)
        {
            // Debug.Log(attackDurationTimer <= 0f ? "Boss: พุ่งไม่โดน/หมดเวลา (กลับ)" : "Boss: ถึงเป้าหมาย (กลับ)");
            currentState = State.Returning;
            
            // หันหน้ากลับบ้าน
            if (bossController != null) bossController.SetFacingDirection((startPosition - (Vector2)transform.position));
        }
    }

    void ReturnToStart()
    {
        transform.position = Vector2.MoveTowards(transform.position, startPosition, returnSpeed * Time.deltaTime);
        if (bossController != null) bossController.SetFacingDirection((startPosition - (Vector2)transform.position));

        if (Vector2.Distance(transform.position, startPosition) < 0.1f)
        {
            // ถึงบ้านแล้ว -> ปล่อยให้ BossController คุม Patrol ต่อ
            if (bossController != null) bossController.TakeManualControl(false);
            
            currentState = State.Cooldown;
            cooldownTimer = attackCooldown;
        }
    }

    void HandleCooldown()
    {
        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer <= 0f)
        {
            currentState = State.Idle;
        }
    }

    // --- การชน (Trigger) ---
    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Boss ชนกับ: " + other.gameObject.name);
        HandleCollision(other.gameObject, other.transform.position);
    }

    // --- การชน (Physics / Solid) --- เพิ่มอันนี้เผื่อ Collider ไม่ได้เป็น Trigger
    void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log("Boss ชน(Solid)กับ: " + collision.gameObject.name);
        HandleCollision(collision.gameObject, collision.transform.position);
    }

    // Logic การชนรวม (ใช้ได้ทั้ง Trigger และ Collision)
    void HandleCollision(GameObject obj, Vector3 hitPosition)
    {
        // ถ้าชนตอนกำลังพุ่งโจมตี
        if (currentState == State.Attacking)
        {
            // ชนผู้เล่น
            if (obj.CompareTag("Player"))
            {
                PlayerKnockback playerScript = obj.GetComponent<PlayerKnockback>();
                if (playerScript != null)
                {
                    Vector2 knockDirection = (hitPosition - transform.position).normalized;
                    playerScript.ApplyKnockback(knockDirection, knockbackForce);
                }
            
                currentState = State.Returning; // กลับทันที
            }
            // ชนกำแพง/พื้น
            else if (((1 << obj.layer) & whatIsObstacle) != 0)
            {
                currentState = State.Returning; // กลับทันที
            
            }
        }
    }
    public void ResetAI()
    {
        currentState = State.Idle;      // กลับมาสถานะรอดู
        warningTimer = 0f;
        cooldownTimer = 0f;             // ล้าง Cooldown (พร้อมโฉบใหม่เมื่อถึงเวลา)

        // ปิดไอคอนตกใจ
        if (alertIcon != null) alertIcon.SetActive(false);

        // คืนการควบคุมให้ BossController (สำคัญมาก ไม่งั้นบอสจะยืนนิ่ง)
        if (bossController != null) bossController.TakeManualControl(false);

        Debug.Log("Boss Harassment: Reset AI State");
    }
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, sightRange);
    }
}