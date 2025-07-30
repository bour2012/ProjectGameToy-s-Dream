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

    [Header("Rope Settings")]
    public LineRenderer ropeRenderer;
    public LayerMask ropeLayerMask;
    private float ropeMaxCastDistance = 20f;

    [Header("Game Manager Integration")]
    public bool respectGameManagerState = true; // เปิด/ปิดการใช้ GameManager

    // Private Variables
    private bool ropeAttached;
    private Vector2 playerPosition;
    private Rigidbody2D ropeHingeAnchorRb;
    private SpriteRenderer ropeHingeAnchorSprite;
    private List<Vector2> ropePositions = new List<Vector2>();
    private bool distanceSet;
    private bool wasSwingingLastFrame = false;

    void Awake()
    {
        // Initialize components
        ropeJoint.enabled = false;
        playerPosition = transform.position;
        ropeHingeAnchorRb = ropeHingeAnchor.GetComponent<Rigidbody2D>();
        ropeHingeAnchorSprite = ropeHingeAnchor.GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // ตรวจสอบว่าสามารถใช้เชือกได้หรือไม่
        if (!CanUseRope())
        {
            // หากไม่สามารถใช้เชือกได้ และกำลังโหนอยู่ ให้รีเซ็ต
            if (ropeAttached)
            {
                ResetRope();
            }
            return;
        }

        // คำนวณทิศทางการเล็ง
        var worldMousePosition = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0f));
        var facingDirection = worldMousePosition - transform.position;
        var aimAngle = Mathf.Atan2(facingDirection.y, facingDirection.x);
        if (aimAngle < 0f)
        {
            aimAngle = Mathf.PI * 2 + aimAngle;
        }

        var aimDirection = Quaternion.Euler(0, 0, aimAngle * Mathf.Rad2Deg) * Vector2.right;
        playerPosition = transform.position;

        // จัดการสถานะเชือก
        if (!ropeAttached)
        {
            SetCrosshairPosition(aimAngle);

            // แจ้ง GameManager ว่าหยุดโหนแล้ว (หากเพิ่งหยุด)
            if (wasSwingingLastFrame)
            {
                NotifySwingingEnd();
                wasSwingingLastFrame = false;
            }

            if (playerMovement != null)
                playerMovement.isSwinging = false;
        }
        else
        {
            crosshairSprite.enabled = false;

            // แจ้ง GameManager ว่าเริ่มโหนแล้ว (หากเพิ่งเริ่ม)
            if (!wasSwingingLastFrame)
            {
                NotifySwingingStart();
                wasSwingingLastFrame = true;
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

    /// <summary>
    /// ตรวจสอบว่ามี Thread สำหรับใช้เชือกหรือไม่
    /// </summary>
    public bool HasThreadForRope()
    {
        if (ItemManager.Instance == null) return true; // ถ้าไม่มี ItemManager ให้ใช้ได้
        return ItemManager.Instance.HasItem(ItemManager.ItemType.Thread);
    }

    /// <summary>
    /// ดูจำนวน Thread ที่เหลือ
    /// </summary>
    public int GetRemainingThread()
    {
        if (ItemManager.Instance == null) return -1; // -1 = unlimited
        return ItemManager.Instance.GetItemCount(ItemManager.ItemType.Thread);
    }

    #region GameManager Integration

    /// <summary>
    /// ตรวจสอบว่าสามารถใช้เชือกได้หรือไม่ตามสถานะของ GameManager และไอเทม
    /// </summary>
    private bool CanUseRope()
    {
        if (!respectGameManagerState) return true;

        if (GameManager.Instance == null) return true;

        // ตรวจสอบสถานะเกม
        GameState currentState = GameManager.Instance.currentState;
        bool stateAllowed = currentState == GameState.Normal || currentState == GameState.RopeSwinging;

        if (!stateAllowed) return false;

        // ตรวจสอบไอเทม Thread (ใช้ร่วมกันระหว่างซ่อมและโหนเชือก)
        if (ItemManager.Instance != null)
        {
            bool hasThread = ItemManager.Instance.HasItem(ItemManager.ItemType.Thread);
            if (!hasThread && !ropeAttached) // ถ้าไม่มีด้ายและยังไม่ได้โหนอยู่
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// แจ้ง GameManager ว่าเริ่มโหนเชือกแล้ว
    /// </summary>
    private void NotifySwingingStart()
    {
        if (!respectGameManagerState || GameManager.Instance == null) return;

        bool success = GameManager.Instance.StartRopeSwinging();

        if (!success)
        {
            Debug.LogWarning("Failed to start rope swinging - GameManager rejected state change");
            // หากไม่สามารถเปลี่ยนสถานะได้ ให้รีเซ็ตเชือก
            ResetRope();
        }
    }

    /// <summary>
    /// แจ้ง GameManager ว่าหยุดโหนเชือกแล้ว
    /// </summary>
    private void NotifySwingingEnd()
    {
        if (!respectGameManagerState || GameManager.Instance == null) return;

        GameManager.Instance.EndRopeSwinging();
    }

    /// <summary>
    /// ตรวจสอบว่าสามารถเริ่มโหนเชือกใหม่ได้หรือไม่
    /// </summary>
    private bool CanStartSwinging()
    {
        if (!respectGameManagerState) return true;

        if (GameManager.Instance == null) return true;

        return GameManager.Instance.currentState == GameState.Normal;
    }

    #endregion

    #region Crosshair Management

    private void SetCrosshairPosition(float aimAngle)
    {
        if (!crosshairSprite.enabled)
        {
            crosshairSprite.enabled = true;
        }

        var x = transform.position.x + 1f * Mathf.Cos(aimAngle);
        var y = transform.position.y + 1f * Mathf.Sin(aimAngle);

        var crossHairPosition = new Vector3(x, y, 0);
        crosshair.transform.position = crossHairPosition;
    }

    #endregion

    #region Input Handling

    private void HandleInput(Vector2 aimDirection)
    {
        // คลิกซ้าย - ยิงเชือก
        if (Input.GetMouseButton(0))
        {
            if (ropeAttached) return;

            // ตรวจสอบว่าสามารถเริ่มโหนได้หรือไม่
            if (!CanStartSwinging()) return;

            // ตรวจสอบไอเทม Thread
            if (ItemManager.Instance != null && !ItemManager.Instance.HasItem(ItemManager.ItemType.Thread))
            {
                Debug.Log("Cannot use rope - no thread available");
                return;
            }

            ropeRenderer.enabled = true;
            var hit = Physics2D.Raycast(playerPosition, aimDirection, ropeMaxCastDistance, ropeLayerMask);

            if (hit.collider != null)
            {
                // ใช้ Thread
                if (ItemManager.Instance != null)
                {
                    if (!ItemManager.Instance.UseItem(ItemManager.ItemType.Thread))
                    {
                        Debug.LogWarning("Failed to use thread for rope swinging");
                        ropeRenderer.enabled = false;
                        return;
                    }
                }

                ropeAttached = true;

                if (!ropePositions.Contains(hit.point))
                {
                    // เพิ่มแรงกระตุ้นเล็กน้อย
                    transform.GetComponent<Rigidbody2D>().AddForce(new Vector2(0f, 1f), ForceMode2D.Impulse);

                    // เพิ่มตำแหน่งปลายเชือก
                    ropePositions.Add(hit.point);

                    // ตั้งค่าเชือกให้สั้นลงเล็กน้อยทันที
                    float actualDistance = Vector2.Distance(playerPosition, hit.point);
                    StartCoroutine(SmoothShortenRope(actualDistance, actualDistance * 0.65f, 0.5f));

                    // เปิดใช้งานเชือกและ anchor
                    ropeJoint.enabled = true;
                    ropeHingeAnchorSprite.enabled = true;

                    Debug.Log("Rope attached successfully - Thread consumed");
                }
            }
            else
            {
                // ยิงไม่โดน - รีเซ็ตเชือก (ไม่เสียไอเทม)
                ropeRenderer.enabled = false;
                ropeAttached = false;
                ropeJoint.enabled = false;
            }
        }

        // คลิกขวา - รีเซ็ตเชือก
        if (Input.GetMouseButton(1))
        {
            ResetRope();
        }

        // ESC - รีเซ็ตเชือกฉุกเฉิน (สำหรับกรณีติดขัด)
        if (Input.GetKeyDown(KeyCode.Escape) && ropeAttached)
        {
            Debug.Log("Emergency rope reset");
            ResetRope();
        }
    }

    #endregion

    #region Rope Management

    private IEnumerator SmoothShortenRope(float fromDistance, float toDistance, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (!ropeAttached) yield break; // หยุดหากเชือกถูกรีเซ็ต

            // ค่อยๆ ลดระยะ
            ropeJoint.distance = Mathf.Lerp(fromDistance, toDistance, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ตั้งค่าระยะสุดท้ายอีกครั้งเพื่อความชัวร์
        if (ropeAttached)
            ropeJoint.distance = toDistance;
    }

    private void ResetRope()
    {
        // รีเซ็ตสถานะเชือก
        ropeJoint.enabled = false;
        ropeAttached = false;
        ropeRenderer.positionCount = 2;
        ropeRenderer.SetPosition(0, transform.position);
        ropeRenderer.SetPosition(1, transform.position);
        ropePositions.Clear();
        ropeHingeAnchorSprite.enabled = false;
        distanceSet = false;

        // รีเซ็ตแรงโน้มถ่วง
        GetComponent<Rigidbody2D>().gravityScale = 1f;

        // รีเซ็ตสถานะ PlayerMovement
        if (playerMovement != null)
            playerMovement.isSwinging = false;

        Debug.Log("Rope reset completed");
    }

    #endregion

    #region Rope Rendering

    private void UpdateRopePositions()
    {
        if (!ropeAttached) return;

        ropeRenderer.positionCount = ropePositions.Count + 1;

        for (var i = ropeRenderer.positionCount - 1; i >= 0; i--)
        {
            if (i != ropeRenderer.positionCount - 1) // ถ้าไม่ใช่จุดสุดท้าย
            {
                ropeRenderer.SetPosition(i, ropePositions[i]);

                // ตั้งค่าตำแหน่ง anchor
                if (i == ropePositions.Count - 1 || ropePositions.Count == 1)
                {
                    var ropePosition = ropePositions[ropePositions.Count - 1];
                    ropeHingeAnchorRb.transform.position = ropePosition;
                }
                else if (i - 1 == ropePositions.IndexOf(ropePositions.Last()))
                {
                    var ropePosition = ropePositions.Last();
                    ropeHingeAnchorRb.transform.position = ropePosition;

                    if (!distanceSet)
                    {
                        ropeJoint.distance = Vector2.Distance(transform.position, ropePosition);
                        distanceSet = true;
                    }
                }
            }
            else
            {
                // จุดสุดท้าย = ตำแหน่งผู้เล่น
                ropeRenderer.SetPosition(i, transform.position);
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// บังคับรีเซ็ตเชือก (เรียกจาก GameManager หรือสคริปต์อื่น)
    /// </summary>
    public void ForceResetRope()
    {
        ResetRope();
    }

    /// <summary>
    /// ตรวจสอบว่าเชือกกำลังใช้งานอยู่หรือไม่
    /// </summary>
    public bool IsRopeAttached()
    {
        return ropeAttached;
    }

    /// <summary>
    /// เปิด/ปิดการใช้ GameManager
    /// </summary>
    public void SetGameManagerIntegration(bool enabled)
    {
        respectGameManagerState = enabled;
    }

    #endregion

    #region Event Handlers

    void OnEnable()
    {
        // Subscribe to GameManager events if needed
        if (GameManager.Instance != null)
        {
            GameManager.OnGameStateChanged += OnGameStateChanged;
        }
    }

    void OnDisable()
    {
        // Unsubscribe from events
        if (GameManager.Instance != null)
        {
            GameManager.OnGameStateChanged -= OnGameStateChanged;
        }
    }

    private void OnGameStateChanged(GameState newState)
    {
        // ตอบสนองต่อการเปลี่ยนสถานะเกม
        switch (newState)
        {
            case GameState.RepairingGlue:
            case GameState.RepairingThread:
            case GameState.Menu:
            case GameState.Cutscene:
                // รีเซ็ตเชือกเมื่อเข้าสถานะที่ไม่สามารถใช้เชือกได้
                if (ropeAttached)
                {
                    ResetRope();
                }
                break;

            case GameState.Normal:
                // สามารถใช้เชือกได้อีกครั้ง
                break;
        }
    }

    #endregion

    #region Debug

    void OnDrawGizmos()
    {
        // วาดเส้นการเล็งสำหรับ Debug
        if (!ropeAttached && Application.isPlaying)
        {
            var worldMousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            worldMousePosition.z = 0;

            var direction = (worldMousePosition - transform.position).normalized;
            var endPoint = transform.position + direction * ropeMaxCastDistance;

            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, endPoint);
        }

        // วาดจุดยึดเชือก
        if (ropePositions.Count > 0)
        {
            Gizmos.color = Color.green;
            foreach (var pos in ropePositions)
            {
                Gizmos.DrawWireSphere(pos, 0.2f);
            }
        }
    }

    #endregion
}