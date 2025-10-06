using UnityEngine;

public class PlayerMovement : MonoBehaviour
{


    [Header("Movement Settings")]
    public float speed = 3f;              // ความเร็วเดินบนพื้น
    public float jumpSpeed = 6f;          // ความแรงกระโดด
    public float airControl = 0.5f;       // การควบคุมในอากาศ (0-1)
    public float maxAirSpeed = 2f;        // ความเร็วสูงสุดในอากาศ
    public float swingForce = 4f;         // แรงแกว่ง

    [Header("Ground Check")]
    public Collider2D stompCheck; // collider ที่ติดใต้เท้า player
    public LayerMask groundLayer;         // Layer ของพื้น
    public float rayLength = 0.1f;        // ระยะตรวจพื้น

    [Header("Wall Check")]
    public float wallCheckDistance = 0.2f; // ระยะตรวจกำแพง
    public float wallCheckRadius = 0.3f;   // รัศมีของ CircleCast สำหรับตรวจกำแพง
    public LayerMask wallLayer;

    [Header("Ladder Settings")]
    public LayerMask ladderLayer = 8;     // Layer สำหรับบันได
    public float climbSpeed = 3f;         // ความเร็วปีนบันได
    public int playerLayerNumber = 10;    // Layer number ของ Player
    public int groundLayerNumber = 9;     // Layer number ของ Ground
    public int wallLayerNumber = 11;      // Layer number ของกำแพง

    [Header("FirePoint Settings")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private Vector2 firePointOffsetRight = new Vector2(0.5f, 0f);
    [SerializeField] private Vector2 firePointOffsetLeft = new Vector2(-0.5f, 0f);

    [Header("Rope Hook")]
    public bool isSwinging;
    public Vector2 ropeHook;

    // Ladder variables
    private bool isOnLadder = false;
    public bool isClimbing = false;

    private GameObject currentLadder;

    private SpriteRenderer playerSprite;
    private Rigidbody2D rBody;
    private Animator animator;
    private Collider2D playerCollider;
    private float horizontalInput;
    private bool groundCheck;
    private bool groundCheckWalk;
    private bool hitWallLeft;    // ชนกำแพงซ้าย
    private bool hitWallRight;   // ชนกำแพงขวา
    private bool facingRight = true;


    void Awake()
    {
        playerSprite = GetComponent<SpriteRenderer>();
        rBody = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        playerCollider = GetComponent<Collider2D>();
    }

    void Update()
    {
        // รับ Input
        bool jumpPressed = Input.GetButtonDown("Jump");
        // กระโดด
        if (jumpPressed && groundCheck  && (!hitWallLeft || !hitWallRight) && !isOnLadder)
        {
            float xSpeed = true ? rBody.linearVelocity.x : 0f;
            rBody.linearVelocity = new Vector2(xSpeed, jumpSpeed);
        }

        horizontalInput = Input.GetAxisRaw("Horizontal");

        // กดขวา → หันขวา (ถ้ายังไม่หัน)
        if (horizontalInput > 0f && !facingRight)
        {
            facingRight = true;
            playerSprite.flipX = false;
            UpdateFirePointPosition();

        }
        // กดซ้าย → หันซ้าย (ถ้ายังไม่หัน)
        else if (horizontalInput < 0f && facingRight)
        {
            facingRight = false;
            playerSprite.flipX = true;
            UpdateFirePointPosition();

        }

        // ตรวจพื้นด้วย Raycast 3 จุด (ซ้าย, กลาง, ขวา)
        CheckGround();

        // ตรวจกำแพงซ้าย-ขวา
        CheckWalls();

        // จัดการ Ladder Input
        HandleLadderInput();

        // Debug Line
        DrawGroundCheckDebug();

        // Animation
        UpdateAnimations();
    }



    void FixedUpdate()
    {

        HandleLadderPhysics();

        if (isClimbing)
        {
            HandleLadderMovement();
        }
        else if (isSwinging)
        {
            HandleSwingMovement();
        }
        else
        {
            HandleGroundMovement();
        }
    }

    private void UpdateFirePointPosition()
    {
        if (facingRight)
            firePoint.localPosition = firePointOffsetRight;
        else
            firePoint.localPosition = firePointOffsetLeft;
    }

    #region Check Walls & Ground
    void CheckGround()
    {
        Bounds bounds = playerCollider.bounds;

        // กำหนดขนาดกล่องตรวจสอบ (ความกว้าง = collider ของ player)
        Vector2 boxSize = new Vector2(bounds.size.x * 0.9f, 0.1f);

        // จุดเริ่มยิง (ใต้เท้าเล็กน้อย)
        Vector2 boxOrigin = new Vector2(bounds.center.x, bounds.min.y - 0.05f);

        // ตรวจด้วย BoxCast ลงไป
        RaycastHit2D hit = Physics2D.BoxCast(boxOrigin, boxSize, 0f, Vector2.down, 0.05f, groundLayer);


        groundCheck = hit.collider != null;

        // Debug
        Color debugColor = groundCheck ? Color.green : Color.red;



        // ตรวจสอบว่าชนกับ Head Collider ของศัตรูหรือไม่
        if (hit.collider != null && hit.collider.CompareTag("EnemyHead"))
        {
            Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                enemy.OnStomped(this); // เรียกฟังก์ชัน OnStomped ของศัตรู
                Debug.Log("Stomped on enemy head!");
            }
        }


        // วาดสี่เหลี่ยมให้เห็นตำแหน่ง BoxCast
        Debug.DrawLine(new Vector3(boxOrigin.x - boxSize.x / 2, boxOrigin.y, 0),
                       new Vector3(boxOrigin.x + boxSize.x / 2, boxOrigin.y, 0), debugColor);

        Debug.DrawLine(new Vector3(boxOrigin.x - boxSize.x / 2, boxOrigin.y - boxSize.y, 0),
                       new Vector3(boxOrigin.x + boxSize.x / 2, boxOrigin.y - boxSize.y, 0), debugColor);

        Debug.DrawLine(new Vector3(boxOrigin.x - boxSize.x / 2, boxOrigin.y, 0),
                       new Vector3(boxOrigin.x - boxSize.x / 2, boxOrigin.y - boxSize.y, 0), debugColor);

        Debug.DrawLine(new Vector3(boxOrigin.x + boxSize.x / 2, boxOrigin.y, 0),
                       new Vector3(boxOrigin.x + boxSize.x / 2, boxOrigin.y - boxSize.y, 0), debugColor);

        // ถ้ามี hit จริง ๆ จะวาดเส้นลงไปหา Collider ที่ชน
        if (hit.collider != null)
        {
            Debug.DrawRay(boxOrigin, Vector2.down * 0.05f, Color.yellow);
        }


        float halfHeight = playerSprite.bounds.extents.y;
        float halfWidth = playerSprite.bounds.extents.x - 0.2f;

        Vector2 leftFoot = new Vector2(transform.position.x - halfWidth * 0.8f, transform.position.y - halfHeight);
        //Vector2 midFoot = new Vector2(transform.position.x, transform.position.y - halfHeight);
        Vector2 rightFoot = new Vector2(transform.position.x + halfWidth * 0.8f, transform.position.y - halfHeight);

        bool leftHit = Physics2D.Raycast(leftFoot, Vector2.down, rayLength, groundLayer);
        //bool midHit = Physics2D.Raycast(midFoot, Vector2.down, rayLength, groundLayer);
        bool rightHit = Physics2D.Raycast(rightFoot, Vector2.down, rayLength, groundLayer);

        groundCheckWalk = (horizontalInput < 0 && leftHit) || (horizontalInput > 0 && rightHit);
    }
    void DrawCapsuleDebug(Vector2 center, Vector2 size, Color color)
    {
        float halfWidth = size.x / 2f;
        float halfHeight = size.y / 2f;
        Vector3 top = new Vector3(center.x, center.y + halfHeight - halfWidth);
        Vector3 bottom = new Vector3(center.x, center.y - halfHeight + halfWidth);

        // เส้นตรงกลาง
        Debug.DrawLine(top + Vector3.left * halfWidth, bottom + Vector3.left * halfWidth, color);
        Debug.DrawLine(top + Vector3.right * halfWidth, bottom + Vector3.right * halfWidth, color);

        // ส่วนโค้ง (ครึ่งวงกลมบน-ล่าง)
        int segments = 12;
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.PI * i / segments;
            Vector3 topArc = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * halfWidth + top;
            Vector3 topArcNext = new Vector3(Mathf.Cos(angle + Mathf.PI / segments), Mathf.Sin(angle + Mathf.PI / segments)) * halfWidth + top;
            Debug.DrawLine(topArc, topArcNext, color);

            Vector3 bottomArc = new Vector3(Mathf.Cos(angle), -Mathf.Sin(angle)) * halfWidth + bottom;
            Vector3 bottomArcNext = new Vector3(Mathf.Cos(angle + Mathf.PI / segments), -Mathf.Sin(angle + Mathf.PI / segments)) * halfWidth + bottom;
            Debug.DrawLine(bottomArc, bottomArcNext, color);
        }
    }

    void CheckWalls()
    {
        float halfHeight = playerSprite.bounds.extents.y;

        // ปรับให้ต่ำลงมาก (ใกล้พื้น)
        Vector2 bodyCenter = new Vector2(transform.position.x, transform.position.y - halfHeight * 0.1f);

        // รีเซ็ตค่าก่อน
        hitWallLeft = false;
        hitWallRight = false;

        // ลดขนาดรัศมีและระยะตรวจสอบ
        float smallerRadius = wallCheckRadius * 0.7f;
        float smallerDistance = wallCheckDistance * 0.5f;

        // ขนาดของแคปซูล (กว้าง, สูง)
        Vector2 capsuleSize = new Vector2(smallerRadius * 2f, halfHeight * 1.2f);

        // ✅ ตรวจสอบทั้ง 2 ด้านเสมอ (ไม่ว่ากำลังเดินไปทางไหน)

        // ตรวจสอบด้านซ้าย
        hitWallLeft = Physics2D.CapsuleCast(
            bodyCenter,
            capsuleSize,
            CapsuleDirection2D.Vertical,
            0f,
            Vector2.left,
            smallerDistance,
            wallLayer
        );

        // ตรวจสอบด้านขวา
        hitWallRight = Physics2D.CapsuleCast(
            bodyCenter,
            capsuleSize,
            CapsuleDirection2D.Vertical,
            0f,
            Vector2.right,
            smallerDistance,
            wallLayer
        );

        // Debug draw (แสดงเฉพาะด้านที่กำลังเดิน)
        if (horizontalInput < 0)
        {
            Color leftColor = hitWallLeft ? Color.red : Color.green;
            Debug.DrawLine(bodyCenter, bodyCenter + Vector2.left * smallerDistance, leftColor);
            DrawCapsuleDebug(bodyCenter, capsuleSize, leftColor);
        }
        else if (horizontalInput > 0)
        {
            Color rightColor = hitWallRight ? Color.red : Color.green;
            Debug.DrawLine(bodyCenter, bodyCenter + Vector2.right * smallerDistance, rightColor);
            DrawCapsuleDebug(bodyCenter, capsuleSize, rightColor);
        }
    }

    //void CheckWalls()
    //{
    //    float halfHeight = playerSprite.bounds.extents.y;

    //    // ปรับให้ต่ำลงมาก (ใกล้พื้น)
    //    Vector2 bodyCenter = new Vector2(transform.position.x, transform.position.y - halfHeight * 0.2f);

    //    // รีเซ็ตค่าก่อน
    //    hitWallLeft = false;
    //    hitWallRight = false;

    //    // ลดขนาดรัศมีและระยะตรวจสอบ
    //    float smallerRadius = wallCheckRadius * 0.7f;
    //    float smallerDistance = wallCheckDistance * 0.5f;

    //    // ขนาดของแคปซูล (กว้าง, สูง)
    //    Vector2 capsuleSize = new Vector2(smallerRadius * 2f, halfHeight * 1.2f);

    //    // ตรวจสอบด้านซ้าย
    //    if (horizontalInput < 0)
    //    {
    //        hitWallLeft = Physics2D.CapsuleCast(
    //            bodyCenter,
    //            capsuleSize,
    //            CapsuleDirection2D.Vertical,
    //            0f,                 // หมุน 0 องศา
    //            Vector2.left,
    //            smallerDistance,
    //            groundLayer
    //        );

    //        Color leftColor = hitWallLeft ? Color.red : Color.green;

    //        // Debug draw
    //        Debug.DrawLine(bodyCenter, bodyCenter + Vector2.left * smallerDistance, leftColor);
    //        DrawCapsuleDebug(bodyCenter, capsuleSize, leftColor);
    //    }

    //    // ตรวจสอบด้านขวา
    //    else if (horizontalInput > 0)
    //    {
    //        hitWallRight = Physics2D.CapsuleCast(
    //            bodyCenter,
    //            capsuleSize,
    //            CapsuleDirection2D.Vertical,
    //            0f,
    //            Vector2.right,
    //            smallerDistance,
    //            groundLayer
    //        );

    //        Color rightColor = hitWallRight ? Color.red : Color.green;

    //        Debug.DrawLine(bodyCenter, bodyCenter + Vector2.right * smallerDistance, rightColor);
    //        DrawCapsuleDebug(bodyCenter, capsuleSize, rightColor);
    //    }

    //    //float halfHeight = playerSprite.bounds.extents.y;

    //    //// ตำแหน่งกลางตัว (ยกเว้นเท้า) - ยกขึ้นมาจากพื้นเล็กน้อย
    //    //Vector2 bodyCenter = new Vector2(transform.position.x, transform.position.y - halfHeight * -0.2f);

    //    //// ตรวจกำแพงซ้ายด้วย CircleCast
    //    //RaycastHit2D leftHit = Physics2D.CircleCast(
    //    //    bodyCenter,                    // ตำแหน่งเริ่มต้น
    //    //    wallCheckRadius,               // รัศมีวงกลม
    //    //    Vector2.left,                  // ทิศทาง
    //    //    wallCheckDistance,             // ระยะทาง
    //    //    groundLayer                    // Layer
    //    //);

    //    //// ตรวจกำแพงขวาด้วย CircleCast
    //    //RaycastHit2D rightHit = Physics2D.CircleCast(
    //    //    bodyCenter,                    // ตำแหน่งเริ่มต้น
    //    //    wallCheckRadius,               // รัศมีวงกลม
    //    //    Vector2.right,                 // ทิศทาง
    //    //    wallCheckDistance,             // ระยะทาง
    //    //    groundLayer                    // Layer
    //    //);

    //    //hitWallLeft = leftHit.collider != null;
    //    //hitWallRight = rightHit.collider != null;

    //    //// Debug CircleCast Visualization
    //    //DrawCircleCastDebug(bodyCenter, Vector2.left, wallCheckDistance, wallCheckRadius, hitWallLeft);
    //    //DrawCircleCastDebug(bodyCenter, Vector2.right, wallCheckDistance, wallCheckRadius, hitWallRight);
    //}
    void DrawGroundCheckDebug()
    {
        float halfHeight = playerSprite.bounds.extents.y;
        float halfWidth = playerSprite.bounds.extents.x - 0.2f;

        Vector2 leftFoot = new Vector2(transform.position.x - halfWidth * 0.8f, transform.position.y - halfHeight);
        //Vector2 midFoot = new Vector2(transform.position.x, transform.position.y - halfHeight);
        Vector2 rightFoot = new Vector2(transform.position.x + halfWidth * 0.8f, transform.position.y - halfHeight);

        Debug.DrawRay(leftFoot, Vector2.down * rayLength, Color.red);
        //Debug.DrawRay(midFoot, Vector2.down * rayLength, Color.green);
        Debug.DrawRay(rightFoot, Vector2.down * rayLength, Color.blue);
    }



    void UpdateAnimations()
    {
        animator.SetFloat("Speed", Mathf.Abs(horizontalInput));
        //animator.SetBool("IsGrounded", groundCheck);
        animator.SetBool("IsSwinging", isSwinging);
        //playerSprite.flipX = horizontalInput < 0f;

        // เพิ่ม Animation สำหรับปีนบันได (ถ้ามี)
        if (animator.parameters.Length > 0)
        {
            foreach (var param in animator.parameters)
            {
                if (param.name == "IsClimbing")
                {
                    animator.SetBool("IsClimbing", isClimbing);
                    break;
                }
            }
        }
    }

    public void ResetMovementState()
    {
        isSwinging = false;
        isOnLadder = false;
        isClimbing = false;
        currentLadder = null;
        ropeHook = Vector2.zero;

        //// คืนค่า Physics
        //if (rBody != null)
        //{
        //    //rBody.gravityScale = 1f;
        //    rBody.linearVelocity = Vector2.zero;
        //    rBody.angularVelocity = 0f;
        //}

        //// คืนค่า Collision
        //Physics2D.IgnoreLayerCollision(playerLayerNumber, groundLayerNumber, false);
        //if (wallLayerNumber > 0)
        //{
        //    Physics2D.IgnoreLayerCollision(playerLayerNumber, wallLayerNumber, false);
        //}

        //// คืนค่า Collider
        //if (playerCollider != null)
        //{
        //    playerCollider.isTrigger = false;
        //}
    }

    #endregion

    #region EnemyStomp
    public void BounceAfterStomp()
    {
        // ให้ผู้เล่นกระโดดใหม่หลังจากเหยียบศัตรู
        rBody.linearVelocity = new Vector2(rBody.linearVelocity.x, jumpSpeed);
        Debug.Log("Player bounced after stomping an enemy!");
    }

    #endregion

    #region Ladder System
    void HandleLadderInput()
    {
        // ตรวจสอบการกดปุ่ม W หรือ ลูกศรขึ้น
        bool climbInput = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        bool downInput = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);

        if (isOnLadder)
        {
            // ต้องกด W หรือ S ก่อนถึงจะเริ่มปีน
            if ((climbInput || downInput) && !isClimbing)
            {
                StartClimbing();
            }
            //// ถ้าปีนอยู่แล้ว แต่ไม่ได้กดปุ่มใดๆ ให้หยุดปีน
            //else if (isClimbing && !climbInput && !downInput)
            //{
            //    StopClimbing();
            //}
        }
    }

    void HandleLadderPhysics()
    {
        // ตรวจสอบว่าผู้เล่นยังอยู่บนบันได
        bool onLadderArea = IsOnLadderArea();

        if (onLadderArea)
        {
            //// ถ้าอยู่บนบันได -> ปิด gravity
            //rBody.gravityScale = 0f;

            //// ถ้ายังปีนอยู่ -> สามารถผ่านพื้น/กำแพงชั้นบน
            //if (isClimbing)
            //{
            //    Physics2D.IgnoreLayerCollision(playerLayerNumber, groundLayerNumber, true);
            //    if (playerCollider != null)
            //        playerCollider.isTrigger = true;
            //}
        }
        else
        {
            // ถ้าออกจากบันไดบางส่วนแล้ว -> เปิด collision
           // rBody.gravityScale = 1f;
           // Physics2D.IgnoreLayerCollision(playerLayerNumber, groundLayerNumber, false);
           // if (playerCollider != null)
           //     playerCollider.isTrigger = false;

           // ออกจากบันได
           //isOnLadder = false;
           // isClimbing = false;
           // currentLadder = null;
        }
    }

    void HandleLadderMovement()
    {

        if (!isClimbing)
        {
            rBody.gravityScale = 1f;

        }
        else
        {

            //// ปิด gravity ขณะปีน
            //rBody.gravityScale = 0f;

            // การเคลื่อนที่แนวตั้ง
            float verticalInput = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)||Input.GetKey(KeyCode.Space))
                verticalInput = 1f;
            else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                verticalInput = -1f;

            // ถ้าไม่ได้กดอะไร ให้ vertical movement = 0 เพื่อหยุดนิ่ง
            float verticalVelocity = verticalInput * climbSpeed;

            // การเคลื่อนที่แนวนอน (สำหรับออกจากบันได)
            float horizontalVelocity = horizontalInput * speed;

            // รวมเป็น movement vector
            Vector2 movement = new Vector2(horizontalVelocity, verticalVelocity);

            // ใช้ MovePosition แทน velocity
            Vector2 newPosition = rBody.position + movement * Time.fixedDeltaTime;
            rBody.MovePosition(newPosition);

            // ออกจากบันไดเมื่อเดินไปข้าง
            if (Mathf.Abs(horizontalInput) > 0.1f)
            {
                if (!IsOnLadderArea())
                {
                    ExitLadder();
                }
            }
        }
    }


    void StartClimbing()
    {
        isClimbing = true;
        //rBody.gravityScale = 0f;

        // ปิดการชนกับพื้นชั้นบนขณะปีนบันได
        Physics2D.IgnoreLayerCollision(playerLayerNumber, groundLayerNumber, true);

        // เปลี่ยน Collider เป็น Trigger ชั่วคราว
        if (playerCollider != null)
        {
            playerCollider.isTrigger = true;
        }
    }

    void StopClimbing()
    {
        isClimbing = false;
        rBody.gravityScale = 1f;

        // เปิดการชนกับพื้นกลับมา
        Physics2D.IgnoreLayerCollision(playerLayerNumber, groundLayerNumber, false);

        // เปลี่ยน Collider กลับเป็นปกติ
        if (playerCollider != null)
        {
            playerCollider.isTrigger = false;
        }
    }

    void ExitLadder()
    {
        isOnLadder = false;
        isClimbing = false;
        currentLadder = null;
        rBody.gravityScale = 1f;

        // เปิดการชนกับพื้นกลับมาเมื่ออยู่บนพื้น
        if (groundCheck)
        {
            Physics2D.IgnoreLayerCollision(playerLayerNumber, groundLayerNumber, false);
            if (wallLayerNumber > 0)
            {
                Physics2D.IgnoreLayerCollision(playerLayerNumber, wallLayerNumber, false);
            }
        }
        else
        {
            // รอจนกว่าจะแตะพื้นก่อนเปิด collision กลับมา
            Invoke("EnableGroundCollision", 0.1f);
        }

        // เปลี่ยน Collider กลับเป็นปกติ
        if (playerCollider != null)
        {
            playerCollider.isTrigger = false;
        }
    }

    void EnableGroundCollision()
    {
        Physics2D.IgnoreLayerCollision(playerLayerNumber, groundLayerNumber, false);
        if (wallLayerNumber > 0)
        {
            Physics2D.IgnoreLayerCollision(playerLayerNumber, wallLayerNumber, false);
        }
    }

    bool IsOnLadderArea()
    {
        // ตรวจสอบว่ายังอยู่ในพื้นที่บันไดหรือไม่
        Collider2D ladderCollider = Physics2D.OverlapBox(
            transform.position,
            playerCollider.bounds.size,
            0f,
            ladderLayer
        );

        return ladderCollider != null;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & ladderLayer) != 0)
        {
            isOnLadder = true;
            currentLadder = other.gameObject;
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & ladderLayer) != 0)
        {
            isOnLadder = true;
            currentLadder = other.gameObject;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject == currentLadder)
        {
            // ออกจากบันไดเมื่อไม่ได้ปีนอยู่
            if (!isClimbing)
            {
                ExitLadder();
            }
        }
    }
    #endregion

    #region Original Movement System
    void HandleSwingMovement()
    {
        if (horizontalInput != 0)
        {
            var playerToHookDir = (ropeHook - (Vector2)transform.position).normalized;
            Vector2 perpendicularDir;

            if (horizontalInput < 0)
                perpendicularDir = new Vector2(-playerToHookDir.y, playerToHookDir.x);
            else
                perpendicularDir = new Vector2(playerToHookDir.y, -playerToHookDir.x);

            // ปรับแรงให้ขึ้นอยู่กับความเร็วปัจจุบันและมุม
            float currentSpeed = rBody.linearVelocity.magnitude;
            float distanceFromHook = Vector2.Distance(transform.position, ropeHook);

            // คำนวณมุมจากจุดแขวนเชือก (0 = ตรงด้านล่าง, ±180 = ด้านบน)
            Vector2 dirFromHook = ((Vector2)transform.position - ropeHook).normalized;
            float angleFromVertical = Mathf.Acos(-dirFromHook.y) * Mathf.Rad2Deg;
            if (dirFromHook.x < 0) angleFromVertical = 360 - angleFromVertical;

            // ปรับแรงตามตำแหน่ง (แรงมากที่สุดเมื่ออยู่ด้านล่าง)
            float positionMultiplier = Mathf.Lerp(0.3f, 1.2f, Mathf.Cos(angleFromVertical * Mathf.Deg2Rad) * 0.5f + 0.5f);

            // ปรับแรงตามความเร็วปัจจุบัน (ป้องกันความเร็วเกินจริง)
            float speedMultiplier = Mathf.Lerp(1.0f, 0.4f, currentSpeed / 15f);

            // ปรับแรงตามระยะจากจุดแขวน (ใกล้ = แรงมาก)
            float distanceMultiplier = Mathf.Lerp(1.2f, 0.8f, (distanceFromHook - 1f) / 10f);

            // รวมแรงทั้งหมด
            float finalForce = swingForce * positionMultiplier * speedMultiplier * distanceMultiplier * Mathf.Abs(horizontalInput);

            var force = perpendicularDir * finalForce;
            rBody.AddForce(force, ForceMode2D.Force);

            // เพิ่มแรงโน้มถ่วงเสมือนเมื่อแกว่งขึ้น (เพื่อความสมจริง)
            if (dirFromHook.y < -0.1f) // กำลังแกว่งขึ้น
            {
                float upwardPenalty = Mathf.Abs(dirFromHook.y) * 2f;
                rBody.AddForce(Vector2.down * upwardPenalty, ForceMode2D.Force);
            }
        }
        else
        {
            // ไม่กดปุ่ม = ลดแรงเสียดทานในอากาศเล็กน้อย (Air Resistance)
            Vector2 currentVel = rBody.linearVelocity;
            float airResistance = 0.98f; // ลด 2% ต่อเฟรม
            rBody.linearVelocity = currentVel * airResistance;
        }

        // จำกัดความเร็วสูงสุด (ป้องกันความเร็วผิดปกติ)
        if (rBody.linearVelocity.magnitude > 12f)
        {
            rBody.linearVelocity = rBody.linearVelocity.normalized * 12f;
        }
    }

    void HandleGroundMovement()
    {
        bool blockedByWall = (horizontalInput < 0 && hitWallLeft) || (horizontalInput > 0 && hitWallRight);

        if (groundCheck)
        {
            // บนพื้น
            if (!blockedByWall)
            {
                // ทางโล่ง ✅ เดินได้เต็มที่
                rBody.linearVelocity = new Vector2(horizontalInput * speed, rBody.linearVelocity.y);
            }
            else
            {
                // ชนกำแพง ❌ หยุด
                rBody.linearVelocity = new Vector2(0, rBody.linearVelocity.y);
            }
        }
        else
        {
            // กลางอากาศ
            if (!blockedByWall)
            {
                // ทางโล่ง ✅ เดินได้ (ลดความเร็ว 20%)
                rBody.linearVelocity = new Vector2(horizontalInput * speed * 0.8f, rBody.linearVelocity.y);
            }
            else
            {
                // ชนกำแพง ❌ หยุดการเคลื่อนที่แนวนอน
                rBody.linearVelocity = new Vector2(0, rBody.linearVelocity.y);
            }
        }
    }

    void HandleAirMovement()
    {
       // if (!groundCheck)
       // {
            // ไม่อนุญาตให้เดินในอากาศ
            rBody.linearVelocity = new Vector2(0, rBody.linearVelocity.y);
       // }
        //if (horizontalInput != 0)
        //{
        //    bool blockedByWall = (horizontalInput < 0 && hitWallLeft) || (horizontalInput > 0 && hitWallRight);
        //    if (blockedByWall) return;

        //    float targetVelocityX = horizontalInput * maxAirSpeed;
        //    float currentVelocityX = rBody.linearVelocity.x;
        //    float velocityDifference = targetVelocityX - currentVelocityX;

        //    // ปรับ Air Control แบบ Progressive (ยิ่งใกล้ target ยิ่งนุ่ม)
        //    float progressiveControl = airControl * (1f - Mathf.Abs(currentVelocityX) / (maxAirSpeed * 2f));
        //    progressiveControl = Mathf.Clamp(progressiveControl, airControl * 0.2f, airControl);

        //    float changeAmount = velocityDifference * progressiveControl;
        //    float newVelocityX = currentVelocityX + changeAmount;
        //    newVelocityX = Mathf.Clamp(newVelocityX, -maxAirSpeed, maxAirSpeed);

        //    // Smooth direction change (การเปลี่ยนทิศทางนุ่มขึ้น)
        //    if (Mathf.Sign(horizontalInput) != Mathf.Sign(currentVelocityX) && Mathf.Abs(currentVelocityX) > maxAirSpeed * 0.3f)
        //    {
        //        float smoothFactor = 1f - (Mathf.Abs(currentVelocityX) / maxAirSpeed) * 0.3f;
        //        newVelocityX = Mathf.Lerp(currentVelocityX, targetVelocityX, progressiveControl * smoothFactor);
        //    }

        //    rBody.linearVelocity = new Vector2(newVelocityX, rBody.linearVelocity.y);
        //}
        //else
        //{
        //    // ไม่กดปุ่มใดๆ: Air Drag ที่นุ่มนวล
        //    float currentVelocityX = rBody.linearVelocity.x;
        //    float dragFactor = 1f - (airControl * 0.15f); // ลด drag ลงเล็กน้อย
        //    float newVelocityX = currentVelocityX * dragFactor;
        //    rBody.linearVelocity = new Vector2(newVelocityX, rBody.linearVelocity.y);
        //}
    }
    void RestoreAirControl()
    {
        // คืนค่า airControl เป็นค่าเริ่มต้น
        // ค่านี้ควรเก็บไว้ใน variable แยก
        airControl = 0.5f; // หรือค่าที่ตั้งใน Inspector
    }

    void HandlePostSwingMovement()
    {
        // ใช้ในกรณีพิเศษที่ต้องการควบคุมการเคลื่อนไหวหลังปล่อยเชือก
        // สามารถเรียกจาก Rope script ได้

        float currentSpeed = rBody.linearVelocity.magnitude;

        if (currentSpeed > 3f) // ถ้ามีความเร็วพอ
        {
            // ลดการควบคุมในอากาศชั่วคราว เพื่อรักษา momentum
            float originalAirControl = airControl;
            airControl *= 0.3f; // ลดการควบคุมลง 70%

            // คืนค่าการควบคุมกลับมาหลัง 0.5 วินาที
            Invoke("RestoreAirControl", 0.5f);
        }
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
    #endregion
}