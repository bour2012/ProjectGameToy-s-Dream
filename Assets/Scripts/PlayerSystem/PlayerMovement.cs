using UnityEngine;
using System.Collections;
using UnityEngine.Audio;
public class PlayerMovement : MonoBehaviour
{
    #region Inspector Fields
    [Header("God Mode Settings")]
    public float godModeSpeed = 10f;
    private bool isGodMode = false;

    [Header("Movement Settings")]
    public float speed = 3f;
    public float jumpSpeed = 6f;
    public float airControl = 0.5f;
    public float maxAirSpeed = 2f;
    public float swingForce = 4f;

    [Header("Jump Assist")]
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.15f;

    [Header("Ground Check")]
    public Transform stompCheck;
    public LayerMask groundLayer;
    public float rayLength = 0.1f;

    [Header("Jumping Check")]
    private bool isJumping;
    public float jumpForce;

    [Header("Wall Check")]
    public float wallCheckDistance = 0.2f;
    public float wallCheckRadius = 0.3f;
    public LayerMask wallLayer;

    [Header("Ladder Settings")]
    public LayerMask ladderLayer = 8;
    public float climbSpeed = 3f;
    public int playerLayerNumber = 6;
    public int groundLayerNumber = 9;
    public int wallLayerNumber = 11;

    [Header("FirePoint Settings")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private Vector2 firePointOffsetRight = new Vector2(0.5f, 0f);
    [SerializeField] private Vector2 firePointOffsetLeft = new Vector2(-0.5f, 0f);

    [Header("Dialog Settings")]
    public float respawnDialogDelay = 1f;

    [Header("Crafting Animation")]
    public string craftingAnimatorBool = "IsCrafting";

    [Header("Audio")]
    public AudioClip jumpSfx;
    [Range(0f,1f)] public float jumpSfxVolume = 1f;
    public AudioClip walkSfx;
    public AudioClip climbSfx; 
    [Range(0f, 1f)] public float climbSfxVolume = 1f; 
    public AudioMixerGroup audioMixerGroup;

    [Header("Rope Hook")]
    public bool isSwinging;
    public Vector2 ropeHook;

    [Header("Effects")]
    public GameObject stompEffectPrefab;
    #endregion

    #region State & Components
    private bool isOnLadder = false;
    public bool isClimbing = false;

    private GameObject currentLadder;

    private SpriteRenderer playerSprite;
    private Rigidbody2D rBody;
    private float originalGravityScale;
    private Animator animator;
    private AudioSource audioSource;
    private AudioSource walkAudioSource;
    private AudioSource climbAudioSource;
    private Collider2D playerCollider;
    private float horizontalInput;
    private bool groundCheck;
    private bool groundCheckWalk;
    private bool hitWallLeft;
    private bool hitWallRight;
    private bool facingRight = true;
    private bool suppressInputUntilRelease = false;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    #endregion

    #region Unity Callbacks
    void Awake()
    {
        playerSprite = GetComponent<SpriteRenderer>();
        rBody = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        playerCollider = GetComponent<Collider2D>();
        originalGravityScale = rBody != null ? rBody.gravityScale : 1f;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        walkAudioSource = gameObject.AddComponent<AudioSource>();
        walkAudioSource.playOnAwake = false;
        walkAudioSource.loop = true;
        walkAudioSource.spatialBlend = 0f;
        walkAudioSource.volume = jumpSfxVolume;

        climbAudioSource = gameObject.AddComponent<AudioSource>();
        climbAudioSource.playOnAwake = false;
        climbAudioSource.loop = true;
        climbAudioSource.spatialBlend = 0f;
        climbAudioSource.volume = climbSfxVolume;
        if (audioMixerGroup != null)
        {
            audioSource.outputAudioMixerGroup = audioMixerGroup;
            walkAudioSource.outputAudioMixerGroup = audioMixerGroup;
            climbAudioSource.outputAudioMixerGroup = audioMixerGroup;
        }
    }

    void Start()
    {
        StartCoroutine(CheckAndPlayRespawnDialogCoroutine());
    }

    void Update()
    {
        if (GameManager.Instance != null)
        {
            GameState state = GameManager.Instance.currentState;
            if (state == GameState.InDialog)
            {
                HandleDialogInputSuppression();
                return;
            }
            else
            {
                if (CheckInputReleaseAfterDialog()) return;
            }
        }

        if (isGodMode)
        {
            HandleGodModeMovement();
            FlipCharacter();
            return;
        }

        bool isCraftingState = GameManager.Instance != null && GameManager.Instance.currentState == GameState.Crafting;
        if (animator != null)
        {
            animator.SetBool(craftingAnimatorBool, isCraftingState);
        }
        if (isCraftingState)
        {
            horizontalInput = 0f;
            if (walkAudioSource != null && walkAudioSource.isPlaying) walkAudioSource.Stop();
            if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
            if (climbAudioSource != null && climbAudioSource.isPlaying) climbAudioSource.Stop(); // <--- [เพิ่มตรงนี้]
            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
            }


            UpdateAnimations();
            return;
        }

        ProcessInput();

        FlipCharacter();

        CheckGround();

        HandleLadderInput();

        DrawGroundCheckDebug();

        bool isWalking = Mathf.Abs(horizontalInput) > 0.01f && groundCheck && !isClimbing && !isSwinging;
        if (walkSfx != null && walkAudioSource != null)
        {
            if (isWalking)
            {
                if (!walkAudioSource.isPlaying)
                {
                    walkAudioSource.clip = walkSfx;
                    walkAudioSource.volume = jumpSfxVolume;
                    walkAudioSource.Play();
                }
            }
            else
            {
                if (walkAudioSource.isPlaying)
                    walkAudioSource.Stop();
            }
        }

        bool isClimbingMoving = isClimbing && Mathf.Abs(rBody.linearVelocity.y) > 0.1f;

        if (climbSfx != null && climbAudioSource != null)
        {
            if (isClimbingMoving)
            {
                if (!climbAudioSource.isPlaying)
                {
                    climbAudioSource.clip = climbSfx;
                    climbAudioSource.volume = climbSfxVolume;
                    climbAudioSource.Play();
                }
            }
            else
            {
                if (climbAudioSource.isPlaying)
                    climbAudioSource.Stop();
            }
        }

        UpdateAnimations();
    }


    void FixedUpdate()
    {
        if (isGodMode) return;

        if (GameManager.Instance != null)
        {
            GameState state = GameManager.Instance.currentState;
            if (state == GameState.InDialog)
            {
                if (rBody.linearVelocity != Vector2.zero && GameManager.Instance.previousState == GameState.ClimbingLadder)
                {
                    rBody.linearVelocity = Vector2.zero;
                }
                else
                {
                    rBody.linearVelocity = new Vector2(0f, rBody.linearVelocity.y);
                }
                horizontalInput = 0f;
                UpdateAnimations();

                return;
            }

            if (state == GameState.Crafting)
            {
                rBody.linearVelocity = new Vector2(0f, rBody.linearVelocity.y);
                horizontalInput = 0f;
                UpdateAnimations();
                return;
            }
        }

        if (isClimbing && !IsOnLadderArea())
        {
            ExitLadder();
        }
        groundCheck = Physics2D.OverlapCircle(stompCheck.position,rayLength,groundLayer);
        DrawDebugCircle(stompCheck.position, rayLength, groundCheck);

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
            Move();
        }
    }
    #endregion

    #region GodMode
    public void SetGodMode(bool enable)
    {
        isGodMode = enable;

        if (rBody != null)
        {
            if (isGodMode)
            {
                rBody.gravityScale = 0;
                rBody.linearVelocity = Vector2.zero;

                gameObject.tag = "Untagged";
                gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
                if (playerCollider != null) playerCollider.isTrigger = true;
                if (playerSprite != null) playerSprite.color = new Color(1, 1, 1, 0.5f);
            }
            else
            {
                rBody.gravityScale = originalGravityScale;
                if (playerCollider != null) playerCollider.isTrigger = false;

                gameObject.tag = "Player";
                gameObject.layer = playerLayerNumber;
                if (playerSprite != null) playerSprite.color = Color.white;
            }
        }
    }

    void HandleGodModeMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector2 move = new Vector2(h, v).normalized * godModeSpeed;

        if (rBody != null)
        {
            rBody.linearVelocity = move;
        }

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsJumping", true);
        }
    }


    void HandleDialogInputSuppression()
    {
        if (!suppressInputUntilRelease)
        {
            suppressInputUntilRelease = true;
            horizontalInput = 0f;
            if (walkAudioSource != null && walkAudioSource.isPlaying) walkAudioSource.Stop();
            if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
            if (climbAudioSource != null && climbAudioSource.isPlaying) climbAudioSource.Stop();
            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
                animator.SetBool("IsJumping", false);
                animator.SetBool("IsSwinging", false);
                animator.SetBool(craftingAnimatorBool, false);
            }
        }
    }

    bool CheckInputReleaseAfterDialog()
    {
        if (suppressInputUntilRelease)
        {
            float rawH = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(rawH) < 0.01f)
            {
                suppressInputUntilRelease = false;
            }
            else
            {
                horizontalInput = 0f;
                if (walkAudioSource != null && walkAudioSource.isPlaying) walkAudioSource.Stop();
                if (animator != null) animator.SetFloat("Speed", 0f);
                return true;
            }
        }
        return false;
    }
    #endregion

    #region Dialog Coroutine
    private IEnumerator CheckAndPlayRespawnDialogCoroutine()
    {
        yield return new WaitForSeconds(respawnDialogDelay);

        if (GameManager.Instance == null) yield break;

        string targetDialogID = GameManager.Instance.dialogIDToPlayOnRespawn;

        if (!string.IsNullOrEmpty(targetDialogID))
        {
            if (GameManager.Instance.playedDialogIDs.Contains(targetDialogID))
            {
                Debug.Log($"Dialog ID '{targetDialogID}' has already been played. Skipping.");
                GameManager.Instance.dialogIDToPlayOnRespawn = "";
                yield break;
            }

            DialogTrigger[] allTriggers = FindObjectsByType<DialogTrigger>(FindObjectsSortMode.None);
            bool foundAndTriggered = false;
            foreach (DialogTrigger trigger in allTriggers)
            {
                if (trigger.dialogID == targetDialogID)
                {
                    trigger.TriggerDialog();
                    foundAndTriggered = true;
                    break;
                }
            }

            if (foundAndTriggered)
            {
                GameManager.Instance.playedDialogIDs.Add(targetDialogID);
            }

            GameManager.Instance.dialogIDToPlayOnRespawn = "";
        }
    }
    #endregion

    #region Movement
    public void FlipCharacter()
    {
        bool canFlip = true;
        if (GameManager.Instance != null)
        {
            canFlip = GameManager.Instance.currentState != GameState.PushingObject;
        }
        if (horizontalInput > 0f && !facingRight && canFlip)
        {
            facingRight = true;
            playerSprite.flipX = false;
            UpdateFirePointPosition();

        }
        else if (horizontalInput < 0f && facingRight && canFlip)
        {
            facingRight = false;
            playerSprite.flipX = true;
            UpdateFirePointPosition();

        }
    }
   
    void DrawDebugCircle(Vector2 position, float radius, bool hit)
    {
        Color color = hit ? Color.green : Color.red;

        Debug.DrawLine(position + Vector2.up * radius, position - Vector2.up * radius, color);
        Debug.DrawLine(position + Vector2.right * radius, position - Vector2.right * radius, color);
        Debug.DrawLine(position + new Vector2(radius, radius), position - new Vector2(radius, radius), color);
        Debug.DrawLine(position + new Vector2(-radius, radius), position - new Vector2(-radius, radius), color);
    }
    public void Move()
    {
        rBody.linearVelocity = new Vector2(horizontalInput * speed, rBody.linearVelocity.y);
        if(isJumping)
        {
            rBody.linearVelocity = new Vector2(0f, jumpSpeed);
        
        }
        isJumping = false;
    }
    public void PlayJumpSound()
    {
        if (jumpSfx != null && audioSource != null)
        {
            audioSource.PlayOneShot(jumpSfx, jumpSfxVolume);
        }
    }
    public void ProcessInput()
    {
        // --- Coyote Time: ให้กระโดดได้แม้ตกจากขอบไปแล้วเล็กน้อย ---
        if (groundCheck)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        // --- Jump Buffer: จำการกด Jump ไว้ เพื่อกระโดดทันทีพอแตะพื้น ---
        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        // --- ตรวจสอบการกระโดดด้วย Coyote Time + Jump Buffer ---
        bool canCoyoteJump = coyoteTimeCounter > 0f;
        if (jumpBufferCounter > 0f && (canCoyoteJump || isSwinging))
        {
            // กระโดดจากพื้น (หรือ coyote) — ไม่ใช่จากเชือก
            if (canCoyoteJump && !isSwinging)
            {
                isJumping = true;
            }

            // ป้องกัน double jump จาก coyote time
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;

            // เล่นเสียงกระโดด (ทั้งจากพื้นและจากเชือก)
            if (jumpSfx != null && audioSource != null)
            {
                audioSource.PlayOneShot(jumpSfx, jumpSfxVolume);
            }
        }

        horizontalInput = Input.GetAxisRaw("Horizontal");
    }
    private void UpdateFirePointPosition()
    {
        if (facingRight)
            firePoint.localPosition = firePointOffsetRight;
        else
            firePoint.localPosition = firePointOffsetLeft;
    }
    #endregion

    #region Ground & Wall Checks
    void CheckGround()
    {
        Bounds bounds = playerCollider.bounds;

        Vector2 boxSize = new Vector2(bounds.size.x * 0.9f, 0.1f);

        Vector2 boxOrigin = new Vector2(bounds.center.x, bounds.min.y - 0.05f);

        RaycastHit2D hit = Physics2D.BoxCast(boxOrigin, boxSize, 0f, Vector2.down, 0.05f, groundLayer);


        groundCheck = hit.collider != null;
        Color debugColor = groundCheck ? Color.green : Color.red;



        if (!isClimbing && hit.collider != null && hit.collider.CompareTag("EnemyHead"))
        {
            Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                enemy.OnStomped(this);
                Debug.Log("Stomped on enemy head!");

                if (stompEffectPrefab != null)
                {
                    GameObject effect = Instantiate(stompEffectPrefab, hit.point, Quaternion.identity);
                    Destroy(effect, 1f);
                }
            }
        }

    }
    void DrawCapsuleDebug(Vector2 center, Vector2 size, Color color)
    {
        float halfWidth = size.x / 2f;
        float halfHeight = size.y / 2f;
        Vector3 top = new Vector3(center.x, center.y + halfHeight - halfWidth);
        Vector3 bottom = new Vector3(center.x, center.y - halfHeight + halfWidth);

        Debug.DrawLine(top + Vector3.left * halfWidth, bottom + Vector3.left * halfWidth, color);
        Debug.DrawLine(top + Vector3.right * halfWidth, bottom + Vector3.right * halfWidth, color);

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

        Vector2 bodyCenter = new Vector2(transform.position.x, transform.position.y - halfHeight * 0.1f);

        hitWallLeft = false;
        hitWallRight = false;

        float smallerRadius = wallCheckRadius * 0.7f;
        float smallerDistance = wallCheckDistance * 0.5f;

        Vector2 capsuleSize = new Vector2(smallerRadius * 2f, halfHeight * 1.2f);

        hitWallLeft = Physics2D.CapsuleCast(
            bodyCenter,
            capsuleSize,
            CapsuleDirection2D.Vertical,
            0f,
            Vector2.left,
            smallerDistance,
            wallLayer
        );

        hitWallRight = Physics2D.CapsuleCast(
            bodyCenter,
            capsuleSize,
            CapsuleDirection2D.Vertical,
            0f,
            Vector2.right,
            smallerDistance,
            wallLayer
        );

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

    void DrawGroundCheckDebug()
    {
        float halfHeight = playerSprite.bounds.extents.y;
        float halfWidth = playerSprite.bounds.extents.x - 0.2f;

        Vector2 leftFoot = new Vector2(transform.position.x - halfWidth * 0.8f, transform.position.y - halfHeight);
        Vector2 rightFoot = new Vector2(transform.position.x + halfWidth * 0.8f, transform.position.y - halfHeight);

        Debug.DrawRay(leftFoot, Vector2.down * rayLength, Color.red);
        Debug.DrawRay(rightFoot, Vector2.down * rayLength, Color.blue);
    }


    #endregion

    #region Animations & State
    void UpdateAnimations()
    {

        if (isGodMode)
        {
            animator.SetBool("IsJumping", true);
            return;
        }

        animator.SetFloat("Speed", Mathf.Abs(horizontalInput));

        animator.SetFloat("yVelocity", rBody.linearVelocity.y);
        bool isFallingOrJumping = !groundCheck && !isClimbing && !isSwinging;
        animator.SetBool("IsJumping", isFallingOrJumping);

        bool isClimbigOnGround = !groundCheck && isClimbing;
        animator.SetBool("IsClimbing", isClimbigOnGround);
        animator.SetBool("IsSwinging", isSwinging);

        GameState state = GameManager.Instance.currentState;
        if (state == GameState.RopeSwinging)
            animator.SetBool("IsClimbThread", true);
        else
            animator.SetBool("IsClimbThread", false);


        float hInput = Input.GetAxisRaw("Horizontal");

        float currentSpeed = Mathf.Abs(hInput);
        if (isFallingOrJumping || currentSpeed > 0.1f)
        {
            animator.SetBool("IsClimbUpDown", false);
        }
    }

    public void ResetMovementState()
    {
        isSwinging = false;
        isOnLadder = false;
        isClimbing = false;
        currentLadder = null;
        ropeHook = Vector2.zero;

    }

    #endregion

    #region EnemyStomp
    public void BounceAfterStomp()
    {
        rBody.linearVelocity = new Vector2(rBody.linearVelocity.x, 5);
         Debug.Log("Player bounced after stomping an enemy!");
    }

    #endregion

    #region Ladder System
    void HandleLadderInput()
    {
        bool climbInput = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        bool downInput = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);

        if (isOnLadder && !isClimbing)
        {
            if (downInput)
            {
                Bounds bounds = playerCollider.bounds;
                Vector2 boxSize = new Vector2(bounds.size.x * 0.9f, 0.1f);
                Vector2 boxOrigin = new Vector2(bounds.center.x, bounds.min.y - 0.05f);
                RaycastHit2D hit = Physics2D.BoxCast(boxOrigin, boxSize, 0f, Vector2.down, 0.05f, groundLayer);

                if (hit.collider != null && hit.collider.GetComponent<PlatformEffector2D>() != null)
                {
                    return;
                }
            }

        

            if (climbInput || downInput)
            {
                StartClimbing();
            }
        }
    }

    void HandleLadderMovement()
    {
        if (!isClimbing)
        {
            rBody.gravityScale = originalGravityScale;
        }
        else
        {
            float verticalInput = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.Space))
                verticalInput = 1f;
            else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                verticalInput = -1f;

            if (verticalInput == 0)
            {
                rBody.linearVelocity = new Vector2(rBody.linearVelocity.x, 0);
            }

            if (verticalInput < 0)
            {
                Bounds bounds = playerCollider.bounds;
                Vector2 boxSize = new Vector2(bounds.size.x * 0.9f, 0.1f);
                Vector2 boxOrigin = new Vector2(bounds.center.x, bounds.min.y - 0.05f);
                RaycastHit2D hit = Physics2D.BoxCast(boxOrigin, boxSize, 0f, Vector2.down, 0.05f, groundLayer);

                if (hit.collider != null && hit.collider.GetComponent<PlatformEffector2D>() != null)
                {
                    ExitLadder();
                    return;
                }
            }
            else if (verticalInput > 0)
            {
                Bounds bounds = playerCollider.bounds;
                Vector2 boxSize = new Vector2(bounds.size.x * 0.9f, 0.1f);
                Vector2 boxOrigin = new Vector2(bounds.center.x, bounds.max.y + 0.05f);
                RaycastHit2D hit = Physics2D.BoxCast(boxOrigin, boxSize, 0f, Vector2.up, 0.1f, groundLayer);

                if (hit.collider != null && hit.collider.GetComponent<PlatformEffector2D>() != null)
                {
                    verticalInput = 0;
                }
            }

            float verticalVelocity = verticalInput * climbSpeed;
            float horizontalVelocity = 0f;

            if (groundCheck)
            {
                horizontalVelocity = horizontalInput * speed;
            }

            rBody.linearVelocity = new Vector2(horizontalVelocity, verticalVelocity);
        }
    }

    void StartClimbing()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartClimbing();
        }

        isClimbing = true;

        Physics2D.IgnoreLayerCollision(playerLayerNumber, groundLayerNumber, true);

        if (playerCollider != null)
        {
            playerCollider.isTrigger = true;
        }
    }

    void ExitLadder()
    {
        if (!isClimbing && !isOnLadder) return;
        if (isGodMode) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.EndClimbing();
        }

        isOnLadder = false;
        isClimbing = false;
        currentLadder = null;
        rBody.gravityScale = originalGravityScale;

        Physics2D.IgnoreLayerCollision(playerLayerNumber, groundLayerNumber, false);
        if (wallLayerNumber > 0)
        {
            Physics2D.IgnoreLayerCollision(playerLayerNumber, wallLayerNumber, false);
        }

        if (playerCollider != null)
        {
            playerCollider.isTrigger = false;
        }
    }

    bool IsOnLadderArea()
    {
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

            float currentSpeed = rBody.linearVelocity.magnitude;
            float distanceFromHook = Vector2.Distance(transform.position, ropeHook);

            Vector2 dirFromHook = ((Vector2)transform.position - ropeHook).normalized;
            float angleFromVertical = Mathf.Acos(-dirFromHook.y) * Mathf.Rad2Deg;
            if (dirFromHook.x < 0) angleFromVertical = 360 - angleFromVertical;

            float positionMultiplier = Mathf.Lerp(0.3f, 1.2f, Mathf.Cos(angleFromVertical * Mathf.Deg2Rad) * 0.5f + 0.5f);

            float speedMultiplier = Mathf.Lerp(1.0f, 0.4f, currentSpeed / 15f);

            float distanceMultiplier = Mathf.Lerp(1.2f, 0.8f, (distanceFromHook - 1f) / 10f);

            float finalForce = swingForce * positionMultiplier * speedMultiplier * distanceMultiplier * Mathf.Abs(horizontalInput);

            var force = perpendicularDir * finalForce;
            rBody.AddForce(force, ForceMode2D.Force);

            if (dirFromHook.y < -0.1f)
            {
                float upwardPenalty = Mathf.Abs(dirFromHook.y) * 2f;
                rBody.AddForce(Vector2.down * upwardPenalty, ForceMode2D.Force);
            }
        }
        else
        {
            Vector2 currentVel = rBody.linearVelocity;
            float airResistance = 0.98f;
            rBody.linearVelocity = currentVel * airResistance;
        }

        if (rBody.linearVelocity.magnitude > 12f)
        {
            rBody.linearVelocity = rBody.linearVelocity.normalized * 12f;
        }
    }

    void HandleGroundMovement()
    {
    }

    void RestoreAirControl()
    {
        airControl = 0.5f;
    }

    void HandlePostSwingMovement()
    {
        float currentSpeed = rBody.linearVelocity.magnitude;

        if (currentSpeed > 3f)
        {
            float originalAirControl = airControl;
            airControl *= 0.3f;
            Invoke("RestoreAirControl", 0.5f);
        }
    }

   

    void DrawCircleCastDebug(Vector2 origin, Vector2 direction, float distance, float radius, bool hit)
    {
        Color debugColor = hit ? Color.red : Color.yellow;

        Debug.DrawRay(origin, direction * distance, debugColor);

        DrawCircleDebug(origin, radius, debugColor, 8);

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

