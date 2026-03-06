using UnityEngine;
using UnityEngine.Events;

public class PlayerPush : MonoBehaviour
{
    [Header("Push Settings")]
    public float detectionDistance = 1.5f;
    public LayerMask boxMask;
    public KeyCode pushKey = KeyCode.E;

    [Header("Detection Settings")]
    public float detectionWidth = 0.8f;
    public float detectionHeight = 1.2f;
    public float minDistanceToBox = 0.3f; // ระยะห่างขั้นต่ำที่ต้องอยู่ใกล้กล่อง

    [Header("Visual Feedback")]
    public GameObject pushIndicator;
    public Color gizmoColor = Color.red;
    public bool showDebugGizmos = true;

    [Header("Audio (Optional)")]
    public AudioSource audioSource;
    public AudioClip grabSound;
    // Removed separate release sound: use grabSound while dragging only

    [Header("Direction Settings")]
    public bool maintainLastDirection = true;

    [Header("Unity Event")]
    public UnityEvent onPickup;

    // Private Variables
    private GameObject targetBox;
    private GameObject availableBox;
    private bool isHolding = false;
    private Rigidbody2D playerRb;
    private SpriteRenderer playerSprite;
    private PlayerController playerController;

    private float lastFacingDirection = 1f;
    private float detectionCooldown = 0f;
    private const float DETECTION_COOLDOWN_TIME = 0.1f; // ช่วงเวลาที่รอก่อนตรวจจับใหม่
    public Animator anim;

    // การอ้างอิงระบบอื่นๆ
    private GameManager gameManager;

    void Start()
    {
        InitializeComponents();
        SetupReferences();
        anim = GetComponent<Animator>();
        // ซ่อน Push Indicator ตอนเริ่มต้น
        if (pushIndicator != null)
            pushIndicator.SetActive(false);
    }

    void Update()
    {
    
        UpdateFacingDirection();

        if (detectionCooldown > 0)
            detectionCooldown -= Time.deltaTime;


        if (isHolding)
        {
            // ถ้ากล่องหายไประหว่างผลัก ให้หยุดผลัก
            if (targetBox == null || !targetBox.activeInHierarchy)
            {
                StopPushing();
                return;
            }
            // อัปเดต Animation ตามความเร็วกล่อง
            UpdatePushAnimationByVelocity();
        }

   
        if (!CanUsePushSystem())
        {
            return;
        }

        CheckForPushableObjects();
        HandlePushInput();
        UpdateVisualFeedback();
    }

    void FixedUpdate()
    {
        // ใช้ FixedUpdate สำหรับการตรวจจับที่เสถียรกว่า
        if (detectionCooldown <= 0 && !isHolding)
        {
            DetectNearbyBoxes();
        }
    }

    #region Initialization

    void UpdatePushAnimationByVelocity()
    {
        if (!isHolding || targetBox == null || anim == null) return;

        Rigidbody2D boxRb = targetBox.GetComponent<Rigidbody2D>();
        if (boxRb == null) return;
        if (GameManager.Instance != null && GameManager.Instance.currentState == GameState.InDialog)
        {
            anim.SetBool("IsPush", false);
            anim.SetBool("IsPull", false);
            anim.SetBool("IsIdlePush", true);
            return;
        }

        float boxVelocityX = boxRb.linearVelocity.x;
        float velocityThreshold = 0.1f;
        bool isBoxMoving = Mathf.Abs(boxVelocityX) >= velocityThreshold;

        // --- Logic การตัดสินใจ Animation ใหม่ ---
        if (!isBoxMoving)
        {
            // กรณีที่ 1: กล่องหยุดนิ่ง -> ต้องเป็นท่า Idle Push
            anim.SetBool("IsIdlePush", true);
            anim.SetBool("IsPush", false);
            anim.SetBool("IsPull", false);
            // หยุดเสียงลากเมื่อกล่องหยุดนิ่ง
            if (audioSource != null && audioSource.isPlaying && audioSource.clip == grabSound)
            {
                audioSource.Stop();
                audioSource.loop = false;
                audioSource.clip = null;
            }
        }
        else
        {
            // กรณีที่ 2: กล่องกำลังเคลื่อนที่ -> ต้องเป็นท่า Push หรือ Pull
            anim.SetBool("IsIdlePush", false);

            bool facingRight = !playerSprite.flipX;
            bool boxMovingRight = boxVelocityX > 0;

            // ตรวจสอบทิศทางเพื่อกำหนดว่าเป็น Push หรือ Pull
            bool isPushing = (facingRight && boxMovingRight) || (!facingRight && !boxMovingRight);
            bool isPulling = !isPushing;

            anim.SetBool("IsPush", isPushing);
            anim.SetBool("IsPull", isPulling);
            // เล่นเสียงลากเฉพาะเมื่อกล่องเคลื่อนที่
            if (audioSource != null && grabSound != null)
            {
                if (!audioSource.isPlaying || audioSource.clip != grabSound)
                {
                    audioSource.clip = grabSound;
                    audioSource.loop = true;
                    audioSource.Play();
                }
            }
        }
        //// Debug Log (สามารถลบออกได้)
        //if (showDebugGizmos)
        //{
        //    string state = isMoving ? (isPushing ? "PUSH" : "PULL") : "IDLE";
        //    //Debug.Log($"[Anim] Facing: {(facingRight ? "Right" : "Left")}, " +
        //    //          $"BoxVel: {boxVelocityX:F2}, State: {state}");
        //}
    }

    void InitializeComponents()
    {
        playerRb = GetComponent<Rigidbody2D>();
        playerSprite = GetComponent<SpriteRenderer>();
        playerController = GetComponent<PlayerController>();

        // ให้ `PlayerPush` มี AudioSource ของตัวเอง (สร้างถ้าไม่พบ)
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound
        }

        //// กำหนดทิศทางเริ่มต้นจาก Sprite
        //if (playerSprite != null)
        //{
        //    lastFacingDirection = playerSprite.flipX ? -1f : 1f;
        //}
    }

    void SetupReferences()
    {
        gameManager = GameManager.Instance;
        if (gameManager == null)
            gameManager = Object.FindFirstObjectByType<GameManager>();

        if (gameManager == null)
            Debug.LogWarning("PlayerPush: GameManager not found!");
    }

    #endregion

    #region State Checking

    bool CanUsePushSystem()
    {
        if (gameManager != null)
        {
            return gameManager.currentState == GameState.Normal ||
                   gameManager.currentState == GameState.PushingObject;
        }

        if (playerController != null)
        {
            return playerController.interactionEnabled;
        }

        return true;
    }

    bool CanStartPushing()
    {
        if (gameManager != null)
        {
            return gameManager.currentState == GameState.Normal;
        }
        return !isHolding;
    }

    bool CanStopPushing()
    {
        if (gameManager != null)
        {
            return gameManager.currentState == GameState.PushingObject || gameManager.currentState == GameState.InDialog;
        }
        return isHolding;
    }

    #endregion

    #region Improved Detection

    void DetectNearbyBoxes()
    {
        float facingDir = GetFacingDirection();
        Vector2 playerPos = transform.position;

        // ใช้ OverlapBox แทน Raycast เพื่อความแม่นยำกว่า
        Vector2 boxCenter = new Vector2(
            playerPos.x + (detectionDistance * 0.5f * facingDir),
            playerPos.y
        );

        Vector2 boxSize = new Vector2(detectionDistance, detectionHeight);

        // หากล่องที่ใกล้ที่สุด
        Collider2D[] nearbyBoxes = Physics2D.OverlapBoxAll(boxCenter, boxSize, 0f, boxMask);

        GameObject closestBox = null;
        float closestDistance = float.MaxValue;

        foreach (var box in nearbyBoxes)
        {
            if (box.CompareTag("Pushable"))
            {
                // ตรวจสอบระยะห่างจริง
                float distance = Vector2.Distance(playerPos, box.transform.position);

                // ตรวจสอบว่าอยู่ในทิศทางที่ถูกต้องหรือไม่
                Vector2 directionToBox = (box.transform.position - transform.position).normalized;
                float dot = Vector2.Dot(Vector2.right * facingDir, directionToBox);

                if (distance < closestDistance && distance <= detectionDistance && dot > 0.3f)
                {
                    closestDistance = distance;
                    closestBox = box.gameObject;
                }
            }
        }

        // อัพเดท availableBox
        if (closestBox != availableBox)
        {
            if (availableBox != null)
            {
                Debug.Log($"[Push Debug] Lost box: {availableBox.name}");
            }

            availableBox = closestBox;

            if (availableBox != null)
            {
                Debug.Log($"[Push Debug] Found box: {availableBox.name} at distance: {closestDistance:F2}");
            }
        }
    }

    void CheckForPushableObjects()
    {
        // เก็บการตรวจจับแบบเก่าไว้เป็น backup
        if (availableBox == null || !availableBox.activeInHierarchy)
        {
            DetectNearbyBoxes();
        }

        // ตรวจสอบว่ากล่องที่มียังอยู่ในระยะหรือไม่
        if (availableBox != null)
        {
            float distance = Vector2.Distance(transform.position, availableBox.transform.position);
            if (distance > detectionDistance * 1.2f) // ให้ buffer เล็กน้อย
            {
                Debug.Log("[Push Debug] Box too far away, removing from available");
                availableBox = null;
            }
        }
    }

    #endregion

    #region Input Handling

    void HandlePushInput()
    {
        if (Input.GetKeyDown(pushKey))
        {
            Debug.Log($"[Push Debug] Key pressed - isHolding: {isHolding}, availableBox: {(availableBox?.name ?? "None")}");

            // เพิ่มการตรวจจับอีกครั้งก่อนทำการกดเพื่อความแน่ใจ
            if (!isHolding && availableBox == null)
            {
                DetectNearbyBoxes();
                Debug.Log($"[Push Debug] Re-detected - availableBox: {(availableBox?.name ?? "None")}");
            }

            if (!isHolding && availableBox != null && CanStartPushing())
            {
                // ตรวจสอบระยะห่างอีกครั้งก่อนเริ่มจับ
                float distance = Vector2.Distance(transform.position, availableBox.transform.position);
                if (distance <= detectionDistance)
                {

                    StartPushing();
                    onPickup.Invoke();
                }
                else
                {
                    Debug.Log($"[Push Debug] Box too far: {distance:F2} > {detectionDistance}");
                }
            }
            else if (isHolding && CanStopPushing())
            {
            
                StopPushing();
            }
            else
            {
                Debug.Log("[Push Debug] Cannot perform push action - conditions not met");
            }

            // เซ็ต cooldown หลังกดปุ่ม
            detectionCooldown = DETECTION_COOLDOWN_TIME;
        }
    }

    void UpdatePushAnimation()
    {
       
        // ดึงค่า Input
        float horizontal = Input.GetAxisRaw("Horizontal");

        // ตรวจสอบทิศทางที่ตัวละครหัน
        bool facingRight = !playerSprite.flipX;

        //// ถ้าไม่กดปุ่มใดๆ -> ไม่เล่น Animation
        //if (Mathf.Abs(horizontal) < 0.01f)
        //{
        //    anim.SetBool("IsPush", false);
        //    anim.SetBool("IsPull", false);
        //    return;
        //}

        // Logic: ถ้าทิศทางการกดตรงกับทิศทางการหัน = Push
        //        ถ้าทิศทางการกดตรงข้ามกับทิศทางการหัน = Pull

        bool isPushing = false;
        bool isPulling = false;

        if (facingRight) // หันขวา
        {
            if (horizontal > 0) // กดขวา
            {
                isPushing = true; // ดันไปข้างหน้า
                isPulling = false;
            }
            else if (horizontal < 0) // กดซ้าย
            {
                isPushing = false;
                isPulling = true; // ดึงกลับมา
            }
        }
        else // หันซ้าย
        {
            if (horizontal < 0) // กดซ้าย
            {
                isPushing = true; // ดันไปข้างหน้า
                isPulling = false;
            }
            else if (horizontal > 0) // กดขวา
            {
                isPushing = false;
                isPulling = true; // ดึงกลับมา
            }
        }

        // อัปเดต Animator
        anim.SetBool("IsPush", isPushing);
        anim.SetBool("IsPull", isPulling);

        Debug.Log($"[Anim] Facing: {(facingRight ? "Right" : "Left")}, Input: {horizontal:F1}, Push: {isPushing}, Pull: {isPulling}");
    }


    #endregion

    #region Push Actions

    void StartPushing()
    {
        if (!CanStartPushing() || availableBox == null)
        {

            return;
        }
        PushableBox boxScript = availableBox.GetComponent<PushableBox>();
         if (boxScript != null)
         {
        boxScript.UnlockBox();
         }
        PushableBox specialBox = availableBox.GetComponent<PushableBox>();
        if (specialBox != null)
        {
            // ถ้ามี, ให้ไปเรียกฟังก์ชันเพื่อ Trigger Event ของกล่องนั้น
            specialBox.NotifyPlayerIsPushing();
        }
        // แจ้ง GameManager ว่าเริ่มดันของ
        bool stateChanged = true;
        if (gameManager != null)
        {
            stateChanged = gameManager.StartPushingObject();
        }

        if (!stateChanged)
        {
            Debug.LogWarning("Cannot start pushing - GameManager rejected state change");
            return;
        }

        // เริ่มการจับกล่อง
        targetBox = availableBox;
        FixedJoint2D joint = targetBox.GetComponent<FixedJoint2D>();

        if (joint == null)
        {
            joint = targetBox.AddComponent<FixedJoint2D>();
        }


        // ตั้งค่า Joint
        joint.enabled = true;
        joint.connectedBody = playerRb;
        joint.enableCollision = false;
        joint.breakForce = Mathf.Infinity; // ป้องกันการหลุด
        joint.breakTorque = Mathf.Infinity;

        isHolding = true;
        // ตั้งค่า Animation เริ่มต้นเป็น Idle
        if (anim != null)
        {
            anim.SetBool("IsIdlePush", true);
            anim.SetBool("IsPush", false);
            anim.SetBool("IsPull", false);
        }
        // เสียงจะถูกควบคุมโดย UpdatePushAnimationByVelocity (เล่นเมื่อมีการเคลื่อนที่)

        Debug.Log($"[Push Debug] Successfully started pushing: {targetBox.name}");
    }

    void StopPushing()
    {
        // Safety check: ถ้าไม่ได้อยู่ในสถานะจับอยู่แล้ว ก็ไม่ต้องทำอะไร
        if (!isHolding) return;

        // ตรวจสอบว่ากล่องที่เคยจับยังอยู่หรือไม่
        if (targetBox != null)
        {
            // ถ้ากล่องยังอยู่: ให้ทำ Logic ปกติ
            // 1. สั่งให้กล่อง "กลับไปใส่เบรกมือ"
            PushableBox boxScript = targetBox.GetComponent<PushableBox>();
            if (boxScript != null)
            {
                boxScript.LockBox();
            }

            // 2. ปลด Joint ออก
            FixedJoint2D joint = targetBox.GetComponent<FixedJoint2D>();
            if (joint != null)
            {
                joint.enabled = false;
                joint.connectedBody = null;
            }

            // หยุดเสียงลากเมื่อปล่อย
            if (audioSource != null)
            {
                // หยุดเฉพาะถ้าเป็นคลิปลากปัจจุบัน
                if (audioSource.isPlaying && audioSource.clip == grabSound)
                {
                    audioSource.Stop();
                    audioSource.loop = false;
                    audioSource.clip = null;
                }
            }
            Debug.Log($"[Push Debug] Successfully stopped pushing: {targetBox.name}");
        }
        else
        {
            // ถ้ากล่องหายไปแล้ว: แค่ Log บอกไว้ก็พอ
            Debug.Log("[Push Debug] Stopped pushing because target box was already destroyed.");
        }

        // --- ส่วนนี้จะทำงานเสมอ ไม่ว่ากล่องจะอยู่หรือไม่ ---
        // เพื่อรีเซ็ตสถานะของ "ผู้เล่น" กลับสู่ปกติ

        // 1. แจ้ง GameManager ว่าหยุดดันของ
        if (gameManager != null)
        {
            // ใช้ CanStopPushing() เพื่อความปลอดภัย แม้ว่าสถานะอาจจะเป็น InDialog
            if (CanStopPushing())
            {
                gameManager.EndPushingObject();
            }
        }

        // 2. รีเซ็ต Animation ของผู้เล่น
        if (anim != null)
        {
            anim.SetBool("IsIdlePush", false);
            anim.SetBool("IsPull", false);
            anim.SetBool("IsPush", false);
        }

        // 3. รีเซ็ตสถานะภายในของ PlayerPush
        targetBox = null;
        isHolding = false;
        detectionCooldown = DETECTION_COOLDOWN_TIME;
    }

    #endregion

    #region Visual & Audio Feedback

    void UpdateVisualFeedback()
    {
        if (pushIndicator != null)
        {
            bool shouldShow = availableBox != null && !isHolding && CanStartPushing();

            if (pushIndicator.activeSelf != shouldShow)
            {
                pushIndicator.SetActive(shouldShow);

                if (shouldShow && availableBox != null)
                {
                    Vector3 indicatorPos = availableBox.transform.position;
                    Bounds bounds = availableBox.GetComponent<Collider2D>().bounds;
                    indicatorPos.y += bounds.size.y / 2 + 0.5f;
                    pushIndicator.transform.position = indicatorPos;
                }
            }
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    #endregion

    #region Helper Methods

    float GetFacingDirection()
    {
        if (maintainLastDirection)
        {
            return lastFacingDirection;
        }
        else
        {
            return playerSprite != null && playerSprite.flipX ? -1f : 1f;
        }
    }

    #endregion

    #region Direction Management

    void UpdateFacingDirection()
    {
        float currentHorizontalInput = Input.GetAxis("Horizontal");

        if (Mathf.Abs(currentHorizontalInput) > 0.01f)
        {
            float newDirection = currentHorizontalInput > 0 ? 1f : -1f;

            // เปลี่ยนทิศทางเฉพาะเมื่อทิศทางเปลี่ยนจริงๆ
            if (newDirection != lastFacingDirection)
            {
                lastFacingDirection = newDirection;

                //if (playerSprite != null)
                //{
                //    playerSprite.flipX = lastFacingDirection < 0;
                //}

                // เมื่อเปลี่ยนทิศทาง ให้ตรวจจับกล่องใหม่
                if (!isHolding)
                {
                    availableBox = null; // ล้างการตรวจจับเก่า
                    DetectNearbyBoxes(); // ตรวจจับใหม่
                }
            }
        }
    }

    #endregion

    #region Public Methods

    public bool IsCurrentlyPushing()
    {
        return isHolding;
    }

    public GameObject GetCurrentPushTarget()
    {
        return targetBox;
    }

    public void ForceStopPushing()
    {
        if (isHolding)
        {
            StopPushing();
        }
    }

    public bool HasAvailablePushTarget()
    {
        return availableBox != null;
    }

    public float GetLastFacingDirection()
    {
        return lastFacingDirection;
    }

    public void SetFacingDirection(float direction)
    {
        lastFacingDirection = direction > 0 ? 1f : -1f;
        if (playerSprite != null)
        {
            playerSprite.flipX = lastFacingDirection < 0;
        }
    }

    #endregion

    #region Event Handlers

    void OnEnable()
    {
        if (GameManager.OnGameStateChanged != null)
            GameManager.OnGameStateChanged += OnGameStateChanged;
    }

    void OnDisable()
    {
        if (GameManager.OnGameStateChanged != null)
            GameManager.OnGameStateChanged -= OnGameStateChanged;
    }

    void OnGameStateChanged(GameState newState)
    {
        switch (newState)
        {
            case GameState.Normal:
                if (isHolding)
                {
                    ForceStopPushing();
                }
                break;

            case GameState.Menu:
            case GameState.Cutscene:
                if (pushIndicator != null)
                    pushIndicator.SetActive(false);
                break;
        }
    }

    #endregion

    //#region Debug & Gizmos

    //void OnDrawGizmos()
    //{
    //    if (!showDebugGizmos) return;

    //    if (playerSprite == null)
    //        playerSprite = GetComponent<SpriteRenderer>();

    //    float facingDir = GetFacingDirection();
    //    Vector2 playerPos = transform.position;

    //    // วาดพื้นที่ตรวจจับ
    //    Vector2 boxCenter = new Vector2(
    //        playerPos.x + (detectionDistance * 0.5f * facingDir),
    //        playerPos.y
    //    );

    //    Vector2 boxSize = new Vector2(detectionDistance, detectionHeight);

    //    Gizmos.color = gizmoColor;
    //    Gizmos.DrawWireCube(boxCenter, boxSize);

    //    // แสดงสถานะปัจจุบัน
    //    if (isHolding && targetBox != null)
    //    {
    //        Gizmos.color = Color.blue;
    //        Gizmos.DrawWireCube(targetBox.transform.position, targetBox.GetComponent<Collider2D>().bounds.size);
    //    }
    //    else if (availableBox != null)
    //    {
    //        Gizmos.color = Color.yellow;
    //        Gizmos.DrawWireCube(availableBox.transform.position, availableBox.GetComponent<Collider2D>().bounds.size);
    //    }

    //    // วาดทิศทางที่กำลังหน้า
    //    Gizmos.color = Color.green;
    //    Gizmos.DrawLine(playerPos, playerPos + Vector2.right * facingDir * 0.5f);
    //}

    ////void OnGUI()
    ////{
    ////    if (!showDebugGizmos) return;

    ////    GUILayout.BeginArea(new Rect(Screen.width - 250, 10, 240, 150));
    ////    GUILayout.Label("=== PlayerPush Debug ===", GUI.skin.box);
    ////    GUILayout.Label($"Is Holding: {isHolding}");
    ////    GUILayout.Label($"Available: {(availableBox != null ? availableBox.name : "None")}");
    ////    GUILayout.Label($"Target: {(targetBox != null ? targetBox.name : "None")}");
    ////    GUILayout.Label($"Can Use: {CanUsePushSystem()}");
    ////    GUILayout.Label($"Facing: {(lastFacingDirection > 0 ? "Right" : "Left")}");
    ////    GUILayout.Label($"Cooldown: {detectionCooldown:F2}");

    ////    if (availableBox != null)
    ////    {
    ////        float distance = Vector2.Distance(transform.position, availableBox.transform.position);
    ////        GUILayout.Label($"Distance: {distance:F2}");
    ////    }

    ////    GUILayout.EndArea();
    ////}

    //#endregion
}