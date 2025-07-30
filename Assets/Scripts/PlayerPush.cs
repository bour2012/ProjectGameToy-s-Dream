using UnityEngine;

public class PlayerPush : MonoBehaviour
{
    [Header("Push Settings")]
    public float distanceToPush = 1.0f;
    public LayerMask boxMask;
    public KeyCode pushKey = KeyCode.E;

    [Header("Visual Feedback")]
    public GameObject pushIndicator; // UI หรือ Icon บอกว่าสามารถดันได้
    public Color raycastColor = Color.red;
    public bool showDebugRay = true;

    [Header("Audio (Optional)")]
    public AudioSource audioSource;
    public AudioClip grabSound;
    public AudioClip releaseSound;

    // Private Variables
    private GameObject targetBox; // กล่องที่กำลังจับ
    private GameObject availableBox; // กล่องที่สามารถจับได้
    private bool isHolding = false;
    private Rigidbody2D playerRb;
    private SpriteRenderer playerSprite;
    private PlayerController playerController;

    // การอ้างอิงระบบอื่นๆ
    private GameManager gameManager;

    void Start()
    {
        InitializeComponents();
        SetupReferences();

        //// ซ่อน Push Indicator ตอนเริ่มต้น
        //if (pushIndicator != null)
        //    pushIndicator.SetActive(false);

        //// ป้องกัน raycast ชน collider ของตัวเอง
        //Physics2D.queriesStartInColliders = false;
    }

    void Update()
    {
        // ตรวจสอบว่าสามารถใช้งานได้หรือไม่
        if (!CanUsePushSystem()) return;

        CheckForPushableObjects();
        HandlePushInput();
        UpdateVisualFeedback();
    }

    #region Initialization

    void InitializeComponents()
    {
        playerRb = GetComponent<Rigidbody2D>();
        playerSprite = GetComponent<SpriteRenderer>();
        playerController = GetComponent<PlayerController>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    void SetupReferences()
    {
        // ค้นหา GameManager
        gameManager = GameManager.Instance;
        if (gameManager == null)
            gameManager = Object.FindFirstObjectByType<GameManager>(); // Updated to use FindFirstObjectByType

        if (gameManager == null)
            Debug.LogWarning("PlayerPush: GameManager not found! Push system may not work correctly.");
    }

    #endregion

    #region State Checking

    bool CanUsePushSystem()
    {
        // ตรวจสอบว่า GameManager อนุญาตให้ใช้งานหรือไม่
        if (gameManager != null)
        {
            return gameManager.currentState == GameState.Normal ||
                   gameManager.currentState == GameState.PushingObject;
        }

        // หาก GameManager ไม่มี ให้ตรวจสอบจาก PlayerController
        if (playerController != null)
        {
            return playerController.interactionEnabled;
        }

        return true; // Default ให้ใช้งานได้
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

    #region Detection & Input

    void CheckForPushableObjects()
    {
        float facingDir = GetFacingDirection();
        Vector2 origin = GetRaycastOrigin(facingDir);
        Vector2 direction = Vector2.right * facingDir;

        // ยิง Raycast เพื่อหาวัตถุที่สามารถดันได้
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, distanceToPush, boxMask);

        if (hit.collider != null && hit.collider.CompareTag("Pushable"))
        {
            availableBox = hit.collider.gameObject;
        }
        else
        {
            availableBox = null;
        }

        // Debug Ray
        if (showDebugRay)
        {
            Debug.DrawRay(origin, direction * distanceToPush, raycastColor, 0.1f);
        }
    }

    void HandlePushInput()
    {
        if (Input.GetKeyDown(pushKey))
        {
            if (!isHolding && availableBox != null)
            {
                StartPushing();
            }
            else if (isHolding)
            {
                StopPushing();
            }
        }
    }

    #endregion

    #region Push Actions

    void StartPushing()
    {
        if (!CanStartPushing() || availableBox == null) return;

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
            // สร้าง FixedJoint2D ใหม่ถ้าไม่มี
            joint = targetBox.AddComponent<FixedJoint2D>();
        }

        joint.enabled = true;
        joint.connectedBody = playerRb;
        joint.enableCollision = false; // ป้องกันการชนกัน

        isHolding = true;

        // เล่นเสียง
        PlaySound(grabSound);

        Debug.Log($"Started pushing: {targetBox.name}");
    }

    void StopPushing()
    {
        if (!CanStopPushing() || targetBox == null) return;

        // ปล่อยกล่อง
        FixedJoint2D joint = targetBox.GetComponent<FixedJoint2D>();
        if (joint != null)
        {
            joint.enabled = false;
            joint.connectedBody = null;
        }

        // แจ้ง GameManager ว่าหยุดดันของ
        if (gameManager != null)
        {
            gameManager.EndPushingObject();
        }

        // เล่นเสียง
        PlaySound(releaseSound);

        Debug.Log($"Stopped pushing: {targetBox.name}");

        targetBox = null;
        isHolding = false;
    }

    #endregion

    #region Visual & Audio Feedback

    void UpdateVisualFeedback()
    {
        // แสดง/ซ่อน Push Indicator
        if (pushIndicator != null)
        {
            bool shouldShow = availableBox != null && !isHolding && CanStartPushing();

            if (pushIndicator.activeSelf != shouldShow)
            {
                pushIndicator.SetActive(shouldShow);

                // ตำแหน่ง Indicator ให้อยู่เหนือกล่อง
                if (shouldShow && availableBox != null)
                {
                    Vector3 indicatorPos = availableBox.transform.position;
                    indicatorPos.y += availableBox.GetComponent<Collider2D>().bounds.size.y / 2 + 0.5f;
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
        return playerSprite != null && playerSprite.flipX ? -1f : 1f;
    }

    Vector2 GetRaycastOrigin(float facingDir)
    {
        return new Vector2(
            transform.position.x + 0.2f * facingDir,
            transform.position.y
        );
    }

    #endregion

    #region Public Methods (สำหรับระบบอื่นเรียกใช้)

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

    #endregion

    #region Event Handlers

    void OnEnable()
    {
        // สมัครรับ Event จาก GameManager
        if (GameManager.OnGameStateChanged == null)
            GameManager.OnGameStateChanged += OnGameStateChanged;
    }

    void OnDisable()
    {
        // ยกเลิกการสมัครรับ Event
        if (GameManager.OnGameStateChanged != null)
            GameManager.OnGameStateChanged -= OnGameStateChanged;
    }

    void OnGameStateChanged(GameState newState)
    {
        // จัดการเมื่อ GameState เปลี่ยน
        switch (newState)
        {
            case GameState.Normal:
                // ถ้ากำลังดันอยู่และกลับมาเป็น Normal แปลว่าถูกยกเลิกจากภายนอก
                if (isHolding)
                {
                    ForceStopPushing();
                }
                break;

            case GameState.Menu:
            case GameState.Cutscene:
                // ซ่อน Visual Feedback
                if (pushIndicator != null)
                    pushIndicator.SetActive(false);
                break;
        }
    }

    #endregion

    #region Debug & Gizmos

    void OnDrawGizmos()
    {
        if (!showDebugRay) return;

        if (playerSprite == null)
            playerSprite = GetComponent<SpriteRenderer>();

        float facingDir = GetFacingDirection();
        Vector2 origin = GetRaycastOrigin(facingDir);
        Vector2 direction = Vector2.right * facingDir;

        // วาด Ray
        Gizmos.color = raycastColor;
        Gizmos.DrawLine(origin, origin + direction * distanceToPush);

        // วาด Sphere ที่จุดเริ่มต้นของ Ray
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(origin, 0.1f);

        // แสดงสถานะปัจจุบัน
        if (isHolding)
        {
            Gizmos.color = Color.blue;
            if (targetBox != null)
            {
                Gizmos.DrawWireCube(targetBox.transform.position, targetBox.GetComponent<Collider2D>().bounds.size);
            }
        }
        else if (availableBox != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(availableBox.transform.position, availableBox.GetComponent<Collider2D>().bounds.size);
        }
    }

    void OnGUI()
    {
        if (!showDebugRay) return;

        GUILayout.BeginArea(new Rect(Screen.width - 200, 10, 190, 100));
        GUILayout.Label("=== PlayerPush Debug ===", GUI.skin.box);
        GUILayout.Label($"Is Holding: {isHolding}");
        GUILayout.Label($"Available: {(availableBox != null ? availableBox.name : "None")}");
        GUILayout.Label($"Target: {(targetBox != null ? targetBox.name : "None")}");
        GUILayout.Label($"Can Use: {CanUsePushSystem()}");
        GUILayout.EndArea();
    }

    #endregion
}