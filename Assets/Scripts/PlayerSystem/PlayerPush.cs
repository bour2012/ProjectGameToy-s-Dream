using UnityEngine;

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
    public AudioClip releaseSound;

    [Header("Direction Settings")]
    public bool maintainLastDirection = true;

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

    // การอ้างอิงระบบอื่นๆ
    private GameManager gameManager;

    void Start()
    {
        InitializeComponents();
        SetupReferences();

        // ซ่อน Push Indicator ตอนเริ่มต้น
        if (pushIndicator != null)
            pushIndicator.SetActive(false);
    }

    void Update()
    {
        UpdateFacingDirection();

        // อัพเดท cooldown
        if (detectionCooldown > 0)
            detectionCooldown -= Time.deltaTime;

        // ตรวจสอบว่าสามารถใช้งานได้หรือไม่
        if (!CanUsePushSystem()) return;

        // ตรวจสอบว่ากล่องที่กำลังจับยังอยู่หรือไม่
        if (isHolding && (targetBox == null || !targetBox.activeInHierarchy))
        {
            StopPushing();
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

    void InitializeComponents()
    {
        playerRb = GetComponent<Rigidbody2D>();
        playerSprite = GetComponent<SpriteRenderer>();
        playerController = GetComponent<PlayerController>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // กำหนดทิศทางเริ่มต้นจาก Sprite
        if (playerSprite != null)
        {
            lastFacingDirection = playerSprite.flipX ? -1f : 1f;
        }
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
            return gameManager.currentState == GameState.PushingObject;
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

    #endregion

    #region Push Actions

    void StartPushing()
    {
        if (!CanStartPushing() || availableBox == null)
        {
            Debug.Log("[Push Debug] Cannot start pushing - conditions not met");
            return;
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

        // เล่นเสียง
        PlaySound(grabSound);

        Debug.Log($"[Push Debug] Successfully started pushing: {targetBox.name}");
    }

    void StopPushing()
    {
        if (!CanStopPushing())
        {
            Debug.Log("[Push Debug] Cannot stop pushing - conditions not met");
            return;
        }

        // ปล่อยกล่อง
        if (targetBox != null)
        {
            FixedJoint2D joint = targetBox.GetComponent<FixedJoint2D>();
            if (joint != null)
            {
                joint.enabled = false;
                joint.connectedBody = null;
                // ไม่ต้อง destroy joint ทิ้ง เพื่อให้สามารถใช้ใหม่ได้
            }

            PlaySound(releaseSound);
            Debug.Log($"[Push Debug] Successfully stopped pushing: {targetBox.name}");
        }
        else
        {
            Debug.Log("[Push Debug] Stopped pushing: (box destroyed or missing)");
        }

        // แจ้ง GameManager ว่าหยุดดันของ
        if (gameManager != null)
        {
            gameManager.EndPushingObject();
        }

        targetBox = null;
        isHolding = false;

        // เซ็ต cooldown หลังจากหยุดดัน
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

                if (playerSprite != null)
                {
                    playerSprite.flipX = lastFacingDirection < 0;
                }

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

    #region Debug & Gizmos

    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        if (playerSprite == null)
            playerSprite = GetComponent<SpriteRenderer>();

        float facingDir = GetFacingDirection();
        Vector2 playerPos = transform.position;

        // วาดพื้นที่ตรวจจับ
        Vector2 boxCenter = new Vector2(
            playerPos.x + (detectionDistance * 0.5f * facingDir),
            playerPos.y
        );

        Vector2 boxSize = new Vector2(detectionDistance, detectionHeight);

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(boxCenter, boxSize);

        // แสดงสถานะปัจจุบัน
        if (isHolding && targetBox != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(targetBox.transform.position, targetBox.GetComponent<Collider2D>().bounds.size);
        }
        else if (availableBox != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(availableBox.transform.position, availableBox.GetComponent<Collider2D>().bounds.size);
        }

        // วาดทิศทางที่กำลังหน้า
        Gizmos.color = Color.green;
        Gizmos.DrawLine(playerPos, playerPos + Vector2.right * facingDir * 0.5f);
    }

    //void OnGUI()
    //{
    //    if (!showDebugGizmos) return;

    //    GUILayout.BeginArea(new Rect(Screen.width - 250, 10, 240, 150));
    //    GUILayout.Label("=== PlayerPush Debug ===", GUI.skin.box);
    //    GUILayout.Label($"Is Holding: {isHolding}");
    //    GUILayout.Label($"Available: {(availableBox != null ? availableBox.name : "None")}");
    //    GUILayout.Label($"Target: {(targetBox != null ? targetBox.name : "None")}");
    //    GUILayout.Label($"Can Use: {CanUsePushSystem()}");
    //    GUILayout.Label($"Facing: {(lastFacingDirection > 0 ? "Right" : "Left")}");
    //    GUILayout.Label($"Cooldown: {detectionCooldown:F2}");

    //    if (availableBox != null)
    //    {
    //        float distance = Vector2.Distance(transform.position, availableBox.transform.position);
    //        GUILayout.Label($"Distance: {distance:F2}");
    //    }

    //    GUILayout.EndArea();
    //}

    #endregion
}