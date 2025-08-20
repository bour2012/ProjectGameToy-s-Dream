using UnityEngine;

public class LadderController : MonoBehaviour
{
    [Header("Ladder Settings")]
    public LayerMask ladderLayer = 8; // Layer สำหรับบันได
    public LayerMask groundLayer = 9; // Layer สำหรับพื้น

    [Header("Layer Numbers (สำหรับ Physics2D.IgnoreLayerCollision)")]
    public int playerLayerNumber = 10; // Layer number ของ Player
    public int groundLayerNumber = 9;  // Layer number ของ Ground
    public int wallLayerNumber = 11;   // Layer number ของกำแพง (ถ้ามี)

    private bool isOnLadder = false;
    private bool isClimbing = false;
    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private GameObject currentLadder;

    [Header("Movement Settings")]
    public float climbSpeed = 3f;
    public float horizontalSpeed = 5f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.1f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
    }

    void Update()
    {
        HandleLadderInput();
        HandleLadderMovement();
    }

    void HandleLadderInput()
    {
        // ตรวจสอบการกดปุ่ม W หรือ ลูกศรขึ้น
        bool climbInput = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        bool downInput = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);

        if (isOnLadder)
        {
            if (climbInput || downInput)
            {
                if (!isClimbing)
                {
                    StartClimbing();
                }
            }
            else
            {
                if (isClimbing)
                {
                    StopClimbing();
                }
            }
        }
    }

    void HandleLadderMovement()
    {
        if (isClimbing)
        {
            // ปิด gravity ขณะปีน
            rb.gravityScale = 0f;

            // การเคลื่อนที่แนวตั้ง
            float verticalInput = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                verticalInput = 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                verticalInput = -1f;

            // การเคลื่อนที่แนวนอน (สำหรับออกจากบันได)
            float horizontalInput = Input.GetAxis("Horizontal");

            Vector2 movement = new Vector2(horizontalInput * horizontalSpeed, verticalInput * climbSpeed);

            // ใช้ MovePosition แทน velocity เพื่อทะลุผ่าน collider
            Vector2 newPosition = rb.position + movement * Time.fixedDeltaTime;
            rb.MovePosition(newPosition);

            // ออกจากบันไดเมื่อเดินไปข้าง
            if (Mathf.Abs(horizontalInput) > 0.1f)
            {
                // ตรวจสอบว่ายังอยู่ในพื้นที่บันไดหรือไม่
                if (!IsOnLadderArea())
                {
                    ExitLadder();
                }
            }
        }
        else
        {
            // การเคลื่อนที่ปกติเมื่อไม่ได้ปีนบันได
            rb.gravityScale = 1f;

            float horizontalInput = Input.GetAxis("Horizontal");
            rb.linearVelocity = new Vector2(horizontalInput * horizontalSpeed, rb.linearVelocity.y);
        }
    }

    void StartClimbing()
    {
        isClimbing = true;
        rb.gravityScale = 0f;

        // วิธีที่ 1: ปิดการชนกับพื้นชั้นบนขณะปีนบันได
        Physics2D.IgnoreLayerCollision(playerLayerNumber, groundLayerNumber, true);

        // วิธีที่ 2: เปลี่ยน Collider เป็น Trigger ชั่วคราว
        Collider2D playerCol = GetComponent<Collider2D>();
        if (playerCol != null)
        {
            playerCol.isTrigger = true;
        }
    }

    void StopClimbing()
    {
        isClimbing = false;
        rb.gravityScale = 1f;

        // เปิดการชนกับพื้นกลับมา
        Physics2D.IgnoreLayerCollision(playerLayerNumber, groundLayerNumber, false);

        // เปลี่ยน Collider กลับเป็นปกติ
        Collider2D playerCol = GetComponent<Collider2D>();
        if (playerCol != null)
        {
            playerCol.isTrigger = false;
        }
    }

    void ExitLadder()
    {
        isOnLadder = false;
        isClimbing = false;
        currentLadder = null;
        rb.gravityScale = 1f;

        // เปิดการชนกับพื้นกลับมาเมื่ออยู่บนพื้น
        if (IsGrounded())
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
    }

    void EnableGroundCollision()
    {
        Physics2D.IgnoreLayerCollision(playerLayerNumber, groundLayerNumber, false);
        if (wallLayerNumber > 0)
        {
            Physics2D.IgnoreLayerCollision(playerLayerNumber, wallLayerNumber, false);
        }
    }

    bool IsGrounded()
    {
        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
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
}