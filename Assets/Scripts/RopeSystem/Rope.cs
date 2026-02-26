using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Rope : MonoBehaviour
{
    [Header("Rope Components")]
    public GameObject ropeHingeAnchor;
    public DistanceJoint2D ropeJoint;
    public Transform crosshair;
    public SpriteRenderer crosshairSprite;
    public PlayerMovement playerMovement;

    [Header("Rope Length Settings")]
    public float maxRopeLength = 20f;
    public float minRopeLength = 2f;
    public float ropeAdjustSpeed = 5f;

    [Header("Rope Settings")]
    public LineRenderer ropeRenderer;
    public LayerMask ropeLayerMask;
    public float ropeMaxCastDistance = 20f;
    public float ropeStandardDistance = 5f;

    [Header("Game Manager Integration")]
    public bool respectGameManagerState = true;

    [Header("Audio")]
    public AudioClip swingStartSound;
    public AudioSource audioSource;

    // Private Variables
    private bool ropeAttached;
    private Vector2 playerPosition;
    private Rigidbody2D ropeHingeAnchorRb;
    private SpriteRenderer ropeHingeAnchorSprite;
    private List<Vector2> ropePositions = new List<Vector2>();
    private bool distanceSet;
    private bool wasSwingingLastFrame = false;
    private Transform attachedTarget;
    private Vector2 localHitOffset;
    private float savedGravity;

    private GlueShooting glueShootingScript;
    public float shootAnimationDuration = 0.5f;
    public string shootRopeTriggerName = "ShootRope";
    private Animator animator;

    void Awake()
    {
        ropeJoint.enabled = false;
        playerPosition = transform.position;
        ropeHingeAnchorRb = ropeHingeAnchor.GetComponent<Rigidbody2D>();
        ropeHingeAnchorSprite = ropeHingeAnchor.GetComponent<SpriteRenderer>();
        animator = GetComponentInParent<Animator>();
        glueShootingScript = GetComponent<GlueShooting>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
    }

    void Update()
    {
        AdjustRopeLength();

        if (!CanUseRope())
        {
            if (ropeAttached) ResetRope();
            return;
        }

        bool usingThread = (glueShootingScript != null && glueShootingScript.GetSelectedItem() == ItemManager.ItemType.Thread);
        if (!usingThread)
        {
            if (ropeAttached) ResetRope();
            return;
        }

        // คำนวณทิศทางการเล็ง
        float distanceFromCamera = Mathf.Abs(Camera.main.transform.position.z - transform.position.z);
        var worldMousePosition = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, distanceFromCamera));
        var facingDirection = worldMousePosition - transform.position;
        var aimAngle = Mathf.Atan2(facingDirection.y, facingDirection.x);

        if (aimAngle < 0f) aimAngle = Mathf.PI * 2 + aimAngle;

        var aimDirection = Quaternion.Euler(0, 0, aimAngle * Mathf.Rad2Deg) * Vector2.right;
        playerPosition = transform.position;

        // จัดการสถานะเชือก
        if (!ropeAttached)
        {
            SetCrosshairPosition(aimAngle);

            if (wasSwingingLastFrame)
            {
                NotifySwingingEnd();
                wasSwingingLastFrame = false;
            }

            if (playerMovement != null) playerMovement.isSwinging = false;
        }
        else
        {
            crosshairSprite.enabled = false;

            if (!wasSwingingLastFrame)
            {
                NotifySwingingStart();
                wasSwingingLastFrame = true;
                if (audioSource != null && swingStartSound != null)
                {
                    audioSource.PlayOneShot(swingStartSound);
                }
            }

            if (playerMovement != null)
            {
                playerMovement.isSwinging = true;
                playerMovement.ropeHook = ropePositions.Last();
            }
        }

        HandleInput(aimDirection);
        UpdateRopePositions();
    }

    #region Public Methods (Used by other scripts)
    public bool HasThreadForRope()
    {
        if (ItemManager.Instance == null) return true;
        return ItemManager.Instance.HasItem(ItemManager.ItemType.Thread);
    }

    public int GetRemainingThread()
    {
        if (ItemManager.Instance == null) return -1;
        return ItemManager.Instance.GetItemCount(ItemManager.ItemType.Thread);
    }

    public void ForceResetRope()
    {
        ResetRope();
    }

    public bool IsRopeAttached()
    {
        return ropeAttached;
    }

    public void SetGameManagerIntegration(bool enabled)
    {
        respectGameManagerState = enabled;
    }
    #endregion

    private void SetCrosshairPosition(float aimAngle)
    {
        if (!crosshairSprite.enabled) crosshairSprite.enabled = true;
        var x = transform.position.x + 1f * Mathf.Cos(aimAngle);
        var y = transform.position.y + 1f * Mathf.Sin(aimAngle);
        crosshair.transform.position = new Vector3(x, y, 0);
    }

    private void HandleInput(Vector2 aimDirection)
    {
        // แก้ไข: ใช้ GetMouseButtonDown(0) เพื่อคลิกยิงครั้งเดียว ไม่กดค้าง
        if (Input.GetMouseButtonDown(0))
        {
            if (ropeAttached) return;

            bool usingThread = (glueShootingScript != null && glueShootingScript.GetSelectedItem() == ItemManager.ItemType.Thread);
            if (!usingThread) return;
            if (!CanStartSwinging()) return;

            if (ItemManager.Instance != null && !ItemManager.Instance.HasItem(ItemManager.ItemType.Thread))
            {
                Debug.Log("Cannot use rope - no thread available");
                return;
            }

            ropeRenderer.enabled = true;

            // แก้ไข: ขยับจุดเริ่ม Raycast ออกจากตัวละครเล็กน้อย เพื่อป้องกัน Raycast ชนตัวผู้เล่นเอง
            Vector2 rayOrigin = playerPosition + aimDirection * 0.2f;
            var hit = Physics2D.Raycast(rayOrigin, aimDirection, ropeMaxCastDistance, ropeLayerMask);

            if (hit.collider != null)
            {
                ropeAttached = true;
                attachedTarget = hit.collider.transform;
                localHitOffset = (Vector2)hit.point - (Vector2)attachedTarget.position;

                if (ItemManager.Instance != null)
                {
                    if (!ItemManager.Instance.UseItem(ItemManager.ItemType.Thread))
                    {
                        ropeRenderer.enabled = false;
                        return;
                    }
                }

                if (!ropePositions.Contains(hit.point))
                {
                    var playerRb = transform.GetComponent<Rigidbody2D>();
                    playerRb.AddForce(new Vector2(0f, 1f), ForceMode2D.Impulse);

                    Vector2 preSwingVelocity = playerRb.linearVelocity;
                    savedGravity = playerRb.gravityScale;
                    ropePositions.Add(hit.point);

                    float actualDistance = Vector2.Distance(playerPosition, hit.point);

                    // แก้ไข: ปรับลอจิกการคำนวณระยะเชือกให้ง่ายและไม่บั๊ก
                    float targetDistance = Mathf.Clamp(actualDistance * 0.8f, minRopeLength, ropeStandardDistance);

                    StartCoroutine(SmoothShortenRope(actualDistance, targetDistance, 0.5f));

                    ropeJoint.enabled = true;
                    ropeHingeAnchorSprite.enabled = true;

                    if (preSwingVelocity.magnitude < 1f)
                    {
                        Vector2 swingDirection = Vector2.Perpendicular((hit.point - (Vector2)transform.position).normalized);
                        if (aimDirection.x > 0) swingDirection *= -1;
                        playerRb.AddForce(swingDirection * 2f, ForceMode2D.Impulse);
                    }
                }
            }
            else
            {
                ropeRenderer.enabled = false;
                ropeAttached = false;
                ropeJoint.enabled = false;
            }
        }

        // รีเซ็ตเชือกด้วย Spacebar หรือคลิกขวา (ลอจิกเดิม)
        if (Input.GetKeyDown(KeyCode.Space) && ropeAttached)
        {
            bool isAimingGlue = (glueShootingScript != null && glueShootingScript.IsAiming() && glueShootingScript.GetSelectedItem() == ItemManager.ItemType.Glue);
            if (!isAimingGlue)
            {
                var playerRb = transform.GetComponent<Rigidbody2D>();
                Vector2 releaseVelocity = playerRb.linearVelocity;

                if (releaseVelocity.magnitude > 2f)
                {
                    Vector2 forwardForce = releaseVelocity.normalized * Mathf.Min(releaseVelocity.magnitude * 0.3f, 3f);
                    playerRb.linearVelocity = releaseVelocity + forwardForce;
                }
                if (playerMovement != null)
                {
                    playerMovement.PlayJumpSound();
                }
                ResetRope();
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape) && ropeAttached)
        {
            ResetRope();
        }
    }

    private void AdjustRopeLength()
    {
        if (!ropeAttached) return;

        if (Input.GetKey(KeyCode.W))
        {
            ropeJoint.distance = Mathf.Max(ropeJoint.distance - ropeAdjustSpeed * Time.deltaTime, minRopeLength);
            if (animator != null) animator.SetBool("IsClimbUpDown", true);
        }
        else if (Input.GetKey(KeyCode.S))
        {
            ropeJoint.distance = Mathf.Min(ropeJoint.distance + ropeAdjustSpeed * Time.deltaTime, maxRopeLength);
            if (animator != null) animator.SetBool("IsClimbUpDown", true);
        }
        else
        {
            if (animator != null) animator.SetBool("IsClimbUpDown", false);
        }
    }

    private IEnumerator SmoothShortenRope(float fromDistance, float toDistance, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!ropeAttached) yield break;
            ropeJoint.distance = Mathf.Lerp(fromDistance, toDistance, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (ropeAttached) ropeJoint.distance = toDistance;
    }

    private void ResetRope()
    {
        if (!ropeAttached) return;
        var playerRb = GetComponent<Rigidbody2D>();

        if (wasSwingingLastFrame)
        {
            NotifySwingingEnd();
            wasSwingingLastFrame = false;
        }

        ropeJoint.enabled = false;
        ropeAttached = false;
        ropeRenderer.positionCount = 2;
        ropeRenderer.SetPosition(0, transform.position);
        ropeRenderer.SetPosition(1, transform.position);
        ropePositions.Clear();
        ropeHingeAnchorSprite.enabled = false;
        distanceSet = false;

        if (playerRb != null) playerRb.gravityScale = savedGravity;

        if (playerMovement != null)
        {
            playerMovement.isSwinging = false;
            playerMovement.ropeHook = Vector2.zero;
        }
    }

    private void UpdateRopePositions()
    {
        if (!ropeAttached) return;

        ropeRenderer.positionCount = ropePositions.Count + 1;

        for (int i = 0; i < ropePositions.Count; i++)
        {
            Vector2 ropePoint = ropePositions[i];

            if (i == ropePositions.Count - 1 && attachedTarget != null)
            {
                ropePoint = (Vector2)attachedTarget.position + localHitOffset;
                ropePositions[i] = ropePoint;
            }

            ropeRenderer.SetPosition(i, ropePoint);
        }

        ropeRenderer.SetPosition(ropeRenderer.positionCount - 1, transform.position);

        if (ropePositions.Count > 0)
        {
            ropeHingeAnchorRb.transform.position = ropePositions.Last();

            if (!distanceSet)
            {
                ropeJoint.distance = Vector2.Distance(transform.position, ropePositions.Last());
                distanceSet = true;
            }
        }

        // หมายเหตุ: โค้ดคอมเมนต์ที่ไม่ได้ใช้งานถูกลบทิ้งไปเพื่อความสะอาด
    }

    #region GameManager Integration & Events

    void OnEnable()
    {
        if (GameManager.Instance != null) GameManager.OnGameStateChanged += OnGameStateChanged;
    }

    void OnDisable()
    {
        if (GameManager.Instance != null) GameManager.OnGameStateChanged -= OnGameStateChanged;
    }

    private void OnGameStateChanged(GameState newState)
    {
        switch (newState)
        {
            case GameState.RepairingGlue:
            case GameState.RepairingThread:
            case GameState.Menu:
            case GameState.Cutscene:
                if (ropeAttached) ResetRope();
                break;
        }
    }

    private bool CanUseRope()
    {
        if (!respectGameManagerState || GameManager.Instance == null) return true;

        GameState currentState = GameManager.Instance.currentState;
        bool stateAllowed = currentState == GameState.Normal || currentState == GameState.RopeSwinging;
        if (!stateAllowed) return false;

        if (ItemManager.Instance != null)
        {
            bool hasThread = ItemManager.Instance.HasItem(ItemManager.ItemType.Thread);
            if (!hasThread && !ropeAttached) return false;
        }
        return true;
    }

    private void NotifySwingingStart()
    {
        if (!respectGameManagerState || GameManager.Instance == null) return;
        if (!GameManager.Instance.StartRopeSwinging()) ResetRope();
    }

    private void NotifySwingingEnd()
    {
        if (!respectGameManagerState || GameManager.Instance == null) return;
        GameManager.Instance.EndRopeSwinging();
    }

    private bool CanStartSwinging()
    {
        if (!respectGameManagerState || GameManager.Instance == null) return true;
        return GameManager.Instance.currentState == GameState.Normal;
    }

    #endregion

    void OnDrawGizmos()
    {
        if (!ropeAttached && Application.isPlaying)
        {
            bool usingThread = (glueShootingScript != null && glueShootingScript.GetSelectedItem() == ItemManager.ItemType.Thread);
            if (usingThread)
            {
                var worldMousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                worldMousePosition.z = 0;
                var direction = (worldMousePosition - transform.position).normalized;
                var endPoint = transform.position + direction * ropeMaxCastDistance;

                Gizmos.color = Color.blue;
                Gizmos.DrawLine(transform.position, endPoint);
            }
        }

        if (ropePositions.Count > 0)
        {
            Gizmos.color = Color.green;
            foreach (var pos in ropePositions)
            {
                Gizmos.DrawWireSphere(pos, 0.2f);
            }
        }
    }
}