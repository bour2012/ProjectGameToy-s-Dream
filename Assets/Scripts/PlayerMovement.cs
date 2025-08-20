using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 3f;              // ความเร็วเดินบนพื้น
    public float jumpSpeed = 6f;          // ความแรงกระโดด
    public float airControl = 0.5f;       // การควบคุมในอากaศ (0-1)
    public float maxAirSpeed = 2f;        // ความเร็วสูงสุดในอากาศ
    public float swingForce = 4f;         // แรงแกว่ง

    [Header("Ground Check")]
    public LayerMask groundLayer;         // Layer ของพื้น
    public float rayLength = 0.1f;        // ระยะตรวจพื้น

    [Header("Wall Check")]
    public float wallCheckDistance = 0.2f; // ระยะตรวจกำแพง
    public float wallCheckRadius = 0.3f;   // รัศมีของ CircleCast สำหรับตรวจกำแพง

    [Header("Rope Hook")]
    public bool isSwinging;
    public Vector2 ropeHook;

    private SpriteRenderer playerSprite;
    private Rigidbody2D rBody;
    private Animator animator;
    private float jumpInput;
    private float horizontalInput;
    private bool groundCheck;
    private bool hitWallLeft;    // ชนกำแพงซ้าย
    private bool hitWallRight;   // ชนกำแพงขวา

    void Awake()
    {
        playerSprite = GetComponent<SpriteRenderer>();
        rBody = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // รับ Input
        jumpInput = Input.GetAxisRaw("Jump");
        horizontalInput = Input.GetAxisRaw("Horizontal");

        // ตรวจพื้นด้วย Raycast 3 จุด (ซ้าย, กลาง, ขวา)
        float halfHeight = playerSprite.bounds.extents.y;
        float halfWidth = playerSprite.bounds.extents.x;

        Vector2 leftFoot = new Vector2(transform.position.x - halfWidth * 0.8f, transform.position.y - halfHeight);
        Vector2 midFoot = new Vector2(transform.position.x, transform.position.y - halfHeight);
        Vector2 rightFoot = new Vector2(transform.position.x + halfWidth * 0.8f, transform.position.y - halfHeight);

        bool leftHit = Physics2D.Raycast(leftFoot, Vector2.down, rayLength, groundLayer);
        bool midHit = Physics2D.Raycast(midFoot, Vector2.down, rayLength, groundLayer);
        bool rightHit = Physics2D.Raycast(rightFoot, Vector2.down, rayLength, groundLayer);

        groundCheck = leftHit || midHit || rightHit;

        // ตรวจกำแพงซ้าย-ขวา
        CheckWalls();

        // Debug Line
        Debug.DrawRay(leftFoot, Vector2.down * rayLength, Color.red);
        Debug.DrawRay(midFoot, Vector2.down * rayLength, Color.green);
        Debug.DrawRay(rightFoot, Vector2.down * rayLength, Color.blue);

        // Animation เดิน
        animator.SetFloat("Speed", Mathf.Abs(horizontalInput));
        animator.SetBool("IsGrounded", groundCheck);
        playerSprite.flipX = horizontalInput < 0f;
    }

    void FixedUpdate()
    {
        if (isSwinging)
        {
            HandleSwingMovement();
        }
        else
        {
            HandleGroundMovement();
        }
    }

    void HandleSwingMovement()
    {
        animator.SetBool("IsSwinging", true);

        if (horizontalInput != 0)
        {
            var playerToHookDir = (ropeHook - (Vector2)transform.position).normalized;
            Vector2 perpendicularDir;

            if (horizontalInput < 0)
                perpendicularDir = new Vector2(-playerToHookDir.y, playerToHookDir.x);
            else
                perpendicularDir = new Vector2(playerToHookDir.y, -playerToHookDir.x);

            var force = perpendicularDir * swingForce * Mathf.Abs(horizontalInput);
            rBody.AddForce(force, ForceMode2D.Force);
        }
    }

    void HandleGroundMovement()
    {
        animator.SetBool("IsSwinging", false);

        if (groundCheck)
        {
            // บนพื้น: เดินได้เต็มที่
            rBody.linearVelocity = new Vector2(horizontalInput * speed, rBody.linearVelocity.y);
        }
        else
        {
            // ในอากาศ: ใช้ Air Control
            HandleAirMovement();
        }

        // กระโดด
        if (groundCheck && jumpInput > 0f)
        {
            rBody.linearVelocity = new Vector2(rBody.linearVelocity.x, jumpSpeed);
        }
    }

    void HandleAirMovement()
    {
        if (horizontalInput != 0)
        {
            // ตรวจสอบว่าชนกำแพงหรือไม่
            bool blockedByWall = (horizontalInput < 0 && hitWallLeft) || (horizontalInput > 0 && hitWallRight);

            if (blockedByWall)
            {
                // ถ้าชนกำแพง ไม่ให้เดินไปทางนั้น
                // แต่ยังสามารถเดินกลับได้
                return;
            }

            float targetVelocityX = horizontalInput * maxAirSpeed;
            float currentVelocityX = rBody.linearVelocity.x;

            // คำนวณความแตกต่างระหว่างความเร็วปัจจุบันกับความเร็วที่ต้องการ
            float velocityDifference = targetVelocityX - currentVelocityX;

            // ใช้ Air Control เป็นตัวกำหนดว่าจะปรับความเร็วได้มากแค่ไหน
            float changeAmount = velocityDifference * airControl;

            // จำกัดการเปลี่ยนแปลงไม่ให้เกิน maxAirSpeed
            float newVelocityX = currentVelocityX + changeAmount;
            newVelocityX = Mathf.Clamp(newVelocityX, -maxAirSpeed, maxAirSpeed);

            // ป้องกันการเปลี่ยนทิศทางอย่างรวดเร็วในอากาศ
            if (Mathf.Sign(horizontalInput) != Mathf.Sign(currentVelocityX) && Mathf.Abs(currentVelocityX) > maxAirSpeed * 0.5f)
            {
                // ถ้ากำลังเปลี่ยนทิศและเร็วอยู่ ให้ลดความเร็วก่อน
                newVelocityX = currentVelocityX * (1f - airControl * 0.5f);
            }

            rBody.linearVelocity = new Vector2(newVelocityX, rBody.linearVelocity.y);
        }
        else
        {
            // ไม่กดปุ่มใดๆ ในอากาศ: ลด Air Drag เล็กน้อย
            float currentVelocityX = rBody.linearVelocity.x;
            float newVelocityX = currentVelocityX * (1f - airControl * 0.1f);
            rBody.linearVelocity = new Vector2(newVelocityX, rBody.linearVelocity.y);
        }
    }

    void CheckWalls()
    {
        float halfHeight = playerSprite.bounds.extents.y;

        // ตำแหน่งกลางตัว (ยกเว้นเท้า) - ยกขึ้นมาจากพื้นเล็กน้อย
        Vector2 bodyCenter = new Vector2(transform.position.x, transform.position.y - halfHeight * -0.2f);

        // ตรวจกำแพงซ้ายด้วย CircleCast
        RaycastHit2D leftHit = Physics2D.CircleCast(
            bodyCenter,                    // ตำแหน่งเริ่มต้น
            wallCheckRadius,               // รัศมีวงกลม
            Vector2.left,                  // ทิศทาง
            wallCheckDistance,             // ระยะทาง
            groundLayer                    // Layer
        );

        // ตรวจกำแพงขวาด้วย CircleCast
        RaycastHit2D rightHit = Physics2D.CircleCast(
            bodyCenter,                    // ตำแหน่งเริ่มต้น
            wallCheckRadius,               // รัศมีวงกลม
            Vector2.right,                 // ทิศทาง
            wallCheckDistance,             // ระยะทาง
            groundLayer                    // Layer
        );

        hitWallLeft = leftHit.collider != null;
        hitWallRight = rightHit.collider != null;

        // Debug CircleCast Visualization
        DrawCircleCastDebug(bodyCenter, Vector2.left, wallCheckDistance, wallCheckRadius, hitWallLeft);
        DrawCircleCastDebug(bodyCenter, Vector2.right, wallCheckDistance, wallCheckRadius, hitWallRight);
    }

    void DrawCircleCastDebug(Vector2 origin, Vector2 direction, float distance, float radius, bool hit)
    {
        Color debugColor = hit ? Color.red : Color.yellow;

        // วาดเส้นกลางของ CircleCast
        Debug.DrawRay(origin, direction * distance, debugColor);

        // วาดวงกลมที่จุดเริ่มต้น
        DrawCircleDebug(origin, radius, debugColor, 8);

        // วาดวงกลมที่จุดสิ้นสุด
        Vector2 endPoint = origin + direction * distance;
        DrawCircleDebug(endPoint, radius, debugColor, 8);
    }

    void DrawCircleDebug(Vector2 center, float radius, Color color, int segments)
    {
        float angleStep = 360f / segments;
        Vector2 prevPoint = center + new Vector2(radius, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector2 newPoint = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            Debug.DrawLine(prevPoint, newPoint, color);
            prevPoint = newPoint;
        }
    }
}