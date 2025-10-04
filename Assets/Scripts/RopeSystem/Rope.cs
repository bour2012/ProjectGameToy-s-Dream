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
    public float maxRopeLength = 20f; // ความยาวสูงสุดของเชือก
    public float minRopeLength = 2f;  // ความยาวต่ำสุดของเชือก
    public float ropeAdjustSpeed = 5f; // ความเร็วในการยืด/หดเชือก

    [Header("Rope Settings")]
    public LineRenderer ropeRenderer;
    public LayerMask ropeLayerMask;
    public float ropeMaxCastDistance = 20f;
    public float ropeStandardDistance = 5f; // ระยะมาตรฐานของเชือก

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
    private Transform attachedTarget; // เก็บวัตถุที่เชือกเกี่ยว
    private Vector2 localHitOffset;   // เก็บ offset จากตำแหน่ง anchor ของวัตถุ

    // Integration with GlueShooting
    private GlueShooting glueShootingScript;

    void Awake()
    {
        // Initialize components
        ropeJoint.enabled = false;
        playerPosition = transform.position;
        ropeHingeAnchorRb = ropeHingeAnchor.GetComponent<Rigidbody2D>();
        ropeHingeAnchorSprite = ropeHingeAnchor.GetComponent<SpriteRenderer>();

        // หา GlueShooting script
        glueShootingScript = GetComponent<GlueShooting>();
    }

    void Update()
    {
        AdjustRopeLength();

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

        // ตรวจสอบว่าเลือกใช้ Thread หรือไม่
        bool usingThread = (glueShootingScript != null &&
                           glueShootingScript.GetSelectedItem() == ItemManager.ItemType.Thread);

        if (!usingThread)
        {
            // ถ้าไม่ได้เลือก Thread และกำลังโหนอยู่ ให้รีเซ็ต
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
        // คลิกซ้าย - ยิงเชือก (เฉพาะเมื่อเลือก Thread)
        if (Input.GetMouseButton(0))
        {
            if (ropeAttached) return;

            // ตรวจสอบว่าเลือกใช้ Thread หรือไม่
            bool usingThread = (glueShootingScript != null &&
                               glueShootingScript.GetSelectedItem() == ItemManager.ItemType.Thread);

            if (!usingThread) return;

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
                ropeAttached = true;

                // ถ้าวัตถุสามารถเคลื่อนที่ได้
                attachedTarget = hit.collider.transform;
                localHitOffset = (Vector2)hit.point - (Vector2)attachedTarget.position;

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

                if (!ropePositions.Contains(hit.point))
                {
                    transform.GetComponent<Rigidbody2D>().AddForce(new Vector2(0f, 1f), ForceMode2D.Impulse);
                    var playerRb = transform.GetComponent<Rigidbody2D>();
                    Vector2 preSwingVelocity = playerRb.linearVelocity;

                    // เพิ่มตำแหน่งปลายเชือก
                    ropePositions.Add(hit.point);

                    // คำนวณระยะจริงและระยะที่ต้องการ
                    float actualDistance = Vector2.Distance(playerPosition, hit.point);
                    float targetDistance;

                    // ตรวจสอบว่าระยะอยู่ในช่วงมาตรฐานหรือไม่
                    if (actualDistance < ropeStandardDistance)
                    {
                        // ถ้าระยะสั้นกว่ามาตรฐาน ให้ใช้ระยะมาตรฐาน
                        targetDistance = ropeStandardDistance;
                        Debug.Log($"Rope distance extended from {actualDistance:F2} to standard distance {ropeStandardDistance}");
                    }
                    else if (actualDistance == ropeStandardDistance)
                    {
                        // ถ้าระยะเท่ากับมาตรฐานพอดี ให้ยาว 75%
                        targetDistance = actualDistance * 0.75f;
                    }
                    else
                    {
                        // ถ้าระยะเกินมาตรฐาน ให้ใช้ระยะมาตรฐานแทน
                        targetDistance = ropeStandardDistance;
                        Debug.Log($"Rope distance clamped from {actualDistance:F2} to standard distance {ropeStandardDistance}");
                    }

                    // ทำให้เชือกสั้นลงตามที่คำนวณ
                    StartCoroutine(SmoothShortenRope(actualDistance, targetDistance, 0.5f));



                    // เปิดใช้งานเชือกและ anchor
                    ropeJoint.enabled = true;
                    ropeHingeAnchorSprite.enabled = true;

                    if (preSwingVelocity.magnitude < 1f)
                    {
                        Vector2 swingDirection = Vector2.Perpendicular((hit.point - (Vector2)transform.position).normalized);
                        if (aimDirection.x > 0) swingDirection *= -1;
                        playerRb.AddForce(swingDirection * 2f, ForceMode2D.Impulse);
                    }

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

        // คลิกขวา - รีเซ็ตเชือก (แต่เฉพาะเมื่อไม่ได้เล็งกาว)
        if (/*Input.GetMouseButton(1)|| */Input.GetKeyDown(KeyCode.Space) && ropeAttached)
        {
            bool isAimingGlue = (glueShootingScript != null &&
                                glueShootingScript.IsAiming() &&
                                glueShootingScript.GetSelectedItem() == ItemManager.ItemType.Glue);

            if (!isAimingGlue)
            {

                var playerRb = transform.GetComponent<Rigidbody2D>();
                Vector2 releaseVelocity = playerRb.linearVelocity;

                if (releaseVelocity.magnitude > 2f)
                {
                    // เพิ่มแรงส่งล่วงในทิศทางที่แกว่งไป
                    Vector2 forwardForce = releaseVelocity.normalized * Mathf.Min(releaseVelocity.magnitude * 0.3f, 3f);
                    playerRb.linearVelocity = releaseVelocity + forwardForce;

                    NotifySwingingEnd();
                    wasSwingingLastFrame = false;
                }
                ResetRope();
            }
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


    private void AdjustRopeLength()
    {
        if (!ropeAttached) return; // ถ้าเชือกไม่ได้เกี่ยวอยู่ ให้ข้ามไป

        // ตรวจจับการกดปุ่ม W (หดเชือก) และ S (ยืดเชือก)
        if (Input.GetKey(KeyCode.W))
        {
            // ลดระยะเชือก (หด)
            ropeJoint.distance = Mathf.Max(ropeJoint.distance - ropeAdjustSpeed * Time.deltaTime, minRopeLength);
        }
        else if (Input.GetKey(KeyCode.S))
        {
            // เพิ่มระยะเชือก (ยืด)
            ropeJoint.distance = Mathf.Min(ropeJoint.distance + ropeAdjustSpeed * Time.deltaTime, maxRopeLength);
        }
    }
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

        // เก็บ velocity ก่อนรีเซ็ต (สำหรับกรณีอื่นๆ ที่เรียก ResetRope)
        var playerRb = GetComponent<Rigidbody2D>();
        Vector2 currentVelocity = Vector2.zero;

        if (ropeAttached && playerRb != null)
        {
            currentVelocity = playerRb.linearVelocity;
        }
        // รีเซ็ตสถานะเชือก
        ropeJoint.enabled = false;
        ropeAttached = false;
        ropeRenderer.positionCount = 2;
        ropeRenderer.SetPosition(0, transform.position);
        ropeRenderer.SetPosition(1, transform.position);
        ropePositions.Clear();
        ropeHingeAnchorSprite.enabled = false;
        distanceSet = false;
        // คืนค่า gravity เป็นปกติ
        if (playerRb != null)
        {
            playerRb.gravityScale = 1f;

            // รักษา momentum ไว้บางส่วน (ถ้ามี)
            if (currentVelocity.magnitude > 1f)
            {
                playerRb.linearVelocity = currentVelocity * 0.8f; // ลดลง 20%
            }
        }

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

        for (int i = 0; i < ropePositions.Count; i++)
        {
            Vector2 ropePoint = ropePositions[i];

            // ถ้าเป็น anchor ที่ปลายสุด และมี target ให้ติดตามวัตถุเคลื่อนที่
            if (i == ropePositions.Count - 1 && attachedTarget != null)
            {
                ropePoint = (Vector2)attachedTarget.position + localHitOffset;
                ropePositions[i] = ropePoint; // อัปเดตตำแหน่ง anchor
            }

            ropeRenderer.SetPosition(i, ropePoint);
        }

        // จุดสุดท้ายของ LineRenderer = player
        ropeRenderer.SetPosition(ropeRenderer.positionCount - 1, transform.position);

        // ตั้ง anchor ของ DistanceJoint ให้ตรงกับ anchor ปลายสุด
        if (ropePositions.Count > 0)
        {
            ropeHingeAnchorRb.transform.position = ropePositions.Last();

            // ตั้งค่า distance ของ Joint ถ้ายังไม่ตั้ง
            if (!distanceSet)
            {
                ropeJoint.distance = Vector2.Distance(transform.position, ropePositions.Last());
                distanceSet = true;
            }
        }

        //if (!ropeAttached) return;

        //ropeRenderer.positionCount = ropePositions.Count + 1;

        //for (var i = ropeRenderer.positionCount - 1; i >= 0; i--)
        //{
        //    if (i != ropeRenderer.positionCount - 1) // ถ้าไม่ใช่จุดสุดท้าย
        //    {
        //        ropeRenderer.SetPosition(i, ropePositions[i]);

        //        // ตั้งค่าตำแหน่ง anchor
        //        if (i == ropePositions.Count - 1 || ropePositions.Count == 1)
        //        {
        //            var ropePosition = ropePositions[ropePositions.Count - 1];
        //            ropeHingeAnchorRb.transform.position = ropePosition;
        //        }
        //        else if (i - 1 == ropePositions.IndexOf(ropePositions.Last()))
        //        {
        //            var ropePosition = ropePositions.Last();
        //            ropeHingeAnchorRb.transform.position = ropePosition;

        //            if (!distanceSet)
        //            {
        //                ropeJoint.distance = Vector2.Distance(transform.position, ropePosition);
        //                distanceSet = true;
        //            }
        //        }
        //    }
        //    else
        //    {
        //        // จุดสุดท้าย = ตำแหน่งผู้เล่น
        //        ropeRenderer.SetPosition(i, transform.position);
        //    }
        //}
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
        // วาดเส้นการเล็งสำหรับ Debug (เฉพาะเมื่อเลือก Thread)
        if (!ropeAttached && Application.isPlaying)
        {
            bool usingThread = (glueShootingScript != null &&
                               glueShootingScript.GetSelectedItem() == ItemManager.ItemType.Thread);

            if (usingThread)
            {
                var worldMousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                worldMousePosition.z = 0;

                var direction = (worldMousePosition - transform.position).normalized;
                var endPoint = transform.position + direction * ropeMaxCastDistance;

                Gizmos.color = Color.blue; // สีน้ำเงินสำหรับเชือก
                Gizmos.DrawLine(transform.position, endPoint);
            }
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
}