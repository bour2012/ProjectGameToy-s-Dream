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

    [Header("Aim Assist & Smart Snap")]
    [Tooltip("เปิดใช้งานระบบช่วยดูดเป้าหมายในทิศทางที่เล็ง")]
    public bool enableAimAssist = true;
    public bool snapToHookCenter = true;
    [Tooltip("มุมกว้างสูงสุดในการค้นหาเป้าหมายตามทิศทางเมาส์ (องศา เช่น 60 - 90 องศา)")]
    [Range(15f, 180f)]
    public float snapMaxAngle = 75f;
    [Tooltip("Layer ของสิ่งกีดขวางที่บังสายตา (เช่น Ground, Wall) ถ้าไม่ระบุจะไม่มีการเช็คสิ่งกีดขวาง")]
    public LayerMask obstacleLayerMask;

    [Header("Crosshair Feedback")]
    public Color crosshairNormalColor = Color.white;
    public Color crosshairHighlightColor = Color.green;
    [Tooltip("ให้ Crosshair กระโดดไปครอบจุดโหนที่ล็อคเป้าหมายไว้หรือไม่")]
    public bool snapCrosshairToTarget = true;

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
    private RaycastHit2D currentTargetHit;
    private Camera mainCamera;

    private GlueShooting glueShootingScript;
    public float shootAnimationDuration = 0.5f;
    public string shootRopeTriggerName = "ShootRope";
    private Animator animator;

    void Awake()
    {
        mainCamera = Camera.main;
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
            if (crosshairSprite != null) crosshairSprite.enabled = false;
            currentTargetHit = default;
            return;
        }

        bool usingThread = (glueShootingScript != null && glueShootingScript.GetSelectedItem() == ItemManager.ItemType.Thread);
        if (!usingThread)
        {
            if (ropeAttached) ResetRope();
            if (crosshairSprite != null) crosshairSprite.enabled = false;
            currentTargetHit = default;
            return;
        }

        if (mainCamera == null) mainCamera = Camera.main;

        // คำนวณทิศทางการเล็ง
        float distanceFromCamera = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        var worldMousePosition = mainCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, distanceFromCamera));
        var facingDirection = worldMousePosition - transform.position;
        var aimAngle = Mathf.Atan2(facingDirection.y, facingDirection.x);

        if (aimAngle < 0f) aimAngle = Mathf.PI * 2 + aimAngle;

        var aimDirection = Quaternion.Euler(0, 0, aimAngle * Mathf.Rad2Deg) * Vector2.right;
        playerPosition = transform.position;

        // จัดการสถานะเชือก
        if (!ropeAttached)
        {
            Vector2 aimDir2D = new Vector2(Mathf.Cos(aimAngle), Mathf.Sin(aimAngle));
            Vector2 previewOrigin = playerPosition + aimDir2D * 0.2f;
            currentTargetHit = FindBestRopeTarget(previewOrigin, aimDir2D);

            SetCrosshairPosition(aimAngle, currentTargetHit);

            if (wasSwingingLastFrame)
            {
                NotifySwingingEnd();
                wasSwingingLastFrame = false;
            }

            if (playerMovement != null) playerMovement.isSwinging = false;
        }
        else
        {
            if (crosshairSprite != null) crosshairSprite.enabled = false;
            currentTargetHit = default;

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
    public bool HasValidTarget()
    {
        if (ropeAttached) return false;
        if (!CanUseRope()) return false;
        if (glueShootingScript != null && glueShootingScript.GetSelectedItem() != ItemManager.ItemType.Thread) return false;
        return currentTargetHit.collider != null;
    }

    public RaycastHit2D GetCurrentTargetHit()
    {
        return currentTargetHit;
    }

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

    private void SetCrosshairPosition(float aimAngle, RaycastHit2D targetHit)
    {
        if (crosshair == null || crosshairSprite == null) return;

        bool usingThread = (glueShootingScript != null && glueShootingScript.GetSelectedItem() == ItemManager.ItemType.Thread);
        if (!usingThread)
        {
            crosshairSprite.enabled = false;
            return;
        }

        if (!crosshairSprite.enabled) crosshairSprite.enabled = true;

        if (targetHit.collider != null)
        {
            crosshairSprite.color = crosshairHighlightColor;

            if (snapCrosshairToTarget)
            {
                Vector3 targetPos = targetHit.collider.bounds.center;
                targetPos.z = 0f;
                crosshair.transform.position = targetPos;
            }
            else
            {
                var x = transform.position.x + 1f * Mathf.Cos(aimAngle);
                var y = transform.position.y + 1f * Mathf.Sin(aimAngle);
                crosshair.transform.position = new Vector3(x, y, 0);
            }
        }
        else
        {
            crosshairSprite.color = crosshairNormalColor;
            var x = transform.position.x + 1f * Mathf.Cos(aimAngle);
            var y = transform.position.y + 1f * Mathf.Sin(aimAngle);
            crosshair.transform.position = new Vector3(x, y, 0);
        }
    }

    private void HandleInput(Vector2 aimDirection)
    {
        if (Input.GetMouseButtonDown(0))
        {
            // ถ้าคลิกโดนกรอบเล็ก (Sub Bubble) เพื่อสลับไอเทม ไม่ต้องยิงเชือก
            if (glueShootingScript != null && glueShootingScript.IsMouseOverSubBubble()) return;

            if (ropeAttached) return;

            bool usingThread = (glueShootingScript != null && glueShootingScript.GetSelectedItem() == ItemManager.ItemType.Thread);
            if (!usingThread) return;
            if (!CanStartSwinging()) return;

            if (ItemManager.Instance != null && !ItemManager.Instance.HasItem(ItemManager.ItemType.Thread))
            {
                Debug.Log("Cannot use rope - no thread available");
                return;
            }

            // Smart Snap: ค้นหาเป้าหมายที่ดีที่สุดในทิศทางที่เล็ง
            Vector2 rayOrigin = playerPosition + aimDirection * 0.2f;
            var hit = FindBestRopeTarget(rayOrigin, aimDirection);

            if (hit.collider != null)
            {
                ropeRenderer.enabled = true;
                ropeAttached = true;
                attachedTarget = hit.collider.transform;

                // Snap to center: ใช้จุดกลางของ hook object แทน hit point ดิบ เพื่อให้เชือกดูสวย
                Vector2 snapPoint = snapToHookCenter ? (Vector2)attachedTarget.position : (Vector2)hit.point;
                localHitOffset = snapPoint - (Vector2)attachedTarget.position;

                if (ItemManager.Instance != null)
                {
                    if (!ItemManager.Instance.UseItem(ItemManager.ItemType.Thread))
                    {
                        ropeRenderer.enabled = false;
                        ropeAttached = false;
                        return;
                    }
                }

                if (!ropePositions.Contains(snapPoint))
                {
                    var playerRb = transform.GetComponent<Rigidbody2D>();
                    playerRb.AddForce(new Vector2(0f, 1f), ForceMode2D.Impulse);

                    Vector2 preSwingVelocity = playerRb.linearVelocity;
                    savedGravity = playerRb.gravityScale;
                    ropePositions.Add(snapPoint);

                    float actualDistance = Vector2.Distance(playerPosition, snapPoint);
                    float targetDistance = Mathf.Clamp(actualDistance * 0.8f, minRopeLength, ropeStandardDistance);

                    StartCoroutine(SmoothShortenRope(actualDistance, targetDistance, 0.5f));

                    ropeJoint.enabled = true;
                    ropeHingeAnchorSprite.enabled = true;

                    if (preSwingVelocity.magnitude < 1f)
                    {
                        Vector2 swingDirection = Vector2.Perpendicular((snapPoint - (Vector2)transform.position).normalized);
                        if (aimDirection.x > 0) swingDirection *= -1;
                        playerRb.AddForce(swingDirection * 2f, ForceMode2D.Impulse);
                    }
                }
            }
            else
            {
                // ไม่โดนเป้าหมาย: ไม่เปิดเชือก ไม่เล่นเสียงใดๆ ตามต้องการ
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

    #region Aim Assist & Smart Snap Helpers
    /// <summary>
    /// ค้นหาจุดโหนที่ดีที่สุดรอบตัวผู้เล่น โดยให้น้ำหนักกับจุดที่อยู่ในทิศทางที่ผู้เล่นเล็งเมาส์ (Smart Directional Snap)
    /// </summary>
    private RaycastHit2D FindBestRopeTarget(Vector2 origin, Vector2 aimDir)
    {
        if (!enableAimAssist)
        {
            return Physics2D.Raycast(origin, aimDir, ropeMaxCastDistance, ropeLayerMask);
        }

        // 1. ค้นหาจุดโหนทั้งหมดในรัศมีระยะโหนรอบตัวผู้เล่น
        Collider2D[] candidates = Physics2D.OverlapCircleAll(origin, ropeMaxCastDistance, ropeLayerMask);
        if (candidates == null || candidates.Length == 0)
        {
            return default;
        }

        Collider2D bestCollider = null;
        RaycastHit2D bestHit = default;
        float bestScore = float.MinValue;

        LayerMask losMask = (obstacleLayerMask.value != 0) ? (ropeLayerMask | obstacleLayerMask) : ropeLayerMask;
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.SetLayerMask(ropeLayerMask);
        RaycastHit2D[] hitBuffer = new RaycastHit2D[1];

        for (int i = 0; i < candidates.Length; i++)
        {
            Collider2D col = candidates[i];
            if (col == null) continue;

            Vector2 targetPos = col.bounds.center;
            Vector2 toTarget = targetPos - origin;
            float distance = toTarget.magnitude;

            if (distance < 0.1f || distance > ropeMaxCastDistance) continue;

            Vector2 dirToTarget = toTarget / distance;

            // 2. คำนวณมุมระหว่างทิศทางที่เล็ง กับ ทิศทางของจุดโหน
            float angle = Vector2.Angle(aimDir, dirToTarget);
            if (angle > snapMaxAngle) continue; // อยู่นอกกรวยที่อนุญาตให้ snap

            // 3. ตรวจสอบ Line of Sight (มีกำแพงหรือสิ่งกีดขวางบังอยู่หรือไม่)
            if (obstacleLayerMask.value != 0)
            {
                RaycastHit2D losHit = Physics2D.Raycast(origin, dirToTarget, distance + 0.1f, losMask);
                if (losHit.collider != null && losHit.collider != col)
                {
                    if (losHit.distance < distance - 0.15f)
                    {
                        continue; // มีสิ่งกีดขวางบังอยู่
                    }
                }
            }

            // 4. คำนวณคะแนน (Scoring): เน้นทิศทางเมาส์ 75% และความใกล้ 25%
            float angleFactor = 1f - (angle / snapMaxAngle);
            float distanceFactor = 1f - (distance / ropeMaxCastDistance);
            float score = (angleFactor * 0.75f) + (distanceFactor * 0.25f);

            if (score > bestScore)
            {
                bestScore = score;
                bestCollider = col;

                // ตรวจหา RaycastHit2D ที่ตรงกับ Collider นี้
                int hitCount = Physics2D.Raycast(origin, dirToTarget, filter, hitBuffer, distance + 0.5f);
                if (hitCount > 0 && hitBuffer[0].collider == col)
                {
                    bestHit = hitBuffer[0];
                }
                else
                {
                    bestHit = default;
                }
            }
        }

        // กรณีเจอ Collider แต่ Raycast ไม่ได้ hit (เช่น จุดศูนย์กลางเป็น Trigger พิเศษ)
        // สร้าง fallback Hit เพื่อให้มีข้อมูล collider และ point ที่ถูกต้อง
        if (bestCollider != null && bestHit.collider == null)
        {
            bestHit = Physics2D.Linecast(origin, bestCollider.bounds.center, ropeLayerMask);
            if (bestHit.collider == null)
            {
                Vector2 targetCenter = bestCollider.bounds.center;
                int count = Physics2D.Raycast(origin, (targetCenter - origin).normalized, filter, hitBuffer, ropeMaxCastDistance);
                if (count > 0)
                {
                    bestHit = hitBuffer[0];
                }
            }
        }

        return bestHit;
    }
    #endregion

    void OnDrawGizmos()
    {
        if (!ropeAttached && Application.isPlaying)
        {
            bool usingThread = (glueShootingScript != null && glueShootingScript.GetSelectedItem() == ItemManager.ItemType.Thread);
            if (usingThread)
            {
                // วาดขอบเขตรัศมีการหาเป้าหมาย
                Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
                Gizmos.DrawWireSphere(transform.position, ropeMaxCastDistance);

                if (currentTargetHit.collider != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(transform.position, currentTargetHit.collider.bounds.center);
                    Gizmos.DrawWireSphere(currentTargetHit.collider.bounds.center, 0.35f);
                }
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