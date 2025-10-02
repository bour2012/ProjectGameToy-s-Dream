using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ========================================
// Glue Shooting System - ระบบยิงกาว
// ========================================

public class GlueShooting : MonoBehaviour
{
    [Header("Glue Projectile")]
    public GameObject glueProjectilePrefab; // Prefab ของกาวที่ยิงออกไป
    public Transform firePoint; // จุดยิง (ถ้าไม่ใส่จะใช้ตำแหน่งผู้เล่น)

    [Header("Shooting Settings")]
    public float projectileSpeed = 15f;
    public float maxShootingRange = 20f;
    public float shootCooldown = 1f; // คูลดาวน์การยิง

    [Header("Trajectory Preview")]
    public LineRenderer trajectoryLine;
    public int trajectoryPoints = 30;
    public float trajectoryTimeStep = 0.1f;

    public LayerMask colliderLayers;

    [Header("UI")]
    public GameObject aimingCrosshair;
    public GameObject glueAimIndicator; // UI แสดงว่ากำลังเล็งกาว

    [SerializeField] private Image glueIcon;
    [SerializeField] private Image threadIcon;
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color unavailableColor = Color.gray;

    [Header("Game Manager Integration")]
    public bool respectGameManagerState = true;

    // Private Variables
    private bool isAiming = false;
    private bool canShoot = true;
    private Vector2 aimDirection;
    private Vector2 mouseWorldPos;

    // Item Selection
    private ItemManager.ItemType selectedItem = ItemManager.ItemType.Glue;

    // Components
    private Rope ropeScript;

    private float scrollAccumulator = 0f; // ตัวแปรสะสมการเลื่อนเมาส์
    private float scrollThreshold = 0.2f; // เกณฑ์ที่ต้องถึงเพื่อเปลี่ยนไอเทม


    void Awake()
    {
        //// หา Rope script
        //ropeScript = GetComponent<Rope>();

        //// ตั้งค่าเริ่มต้น
        //if (trajectoryLine != null)
        //{
        //    trajectoryLine.enabled = false;
        //    trajectoryLine.positionCount = trajectoryPoints;
        //}

        //if (aimingCrosshair != null)
        //    aimingCrosshair.SetActive(false);

        //if (glueAimIndicator != null)
        //    glueAimIndicator.SetActive(false);
        ropeScript = GetComponent<Rope>();

        if (trajectoryLine != null)
        {
            trajectoryLine.enabled = false;
            trajectoryLine.positionCount = trajectoryPoints;
        }

        if (aimingCrosshair != null)
            aimingCrosshair.SetActive(false);

        if (glueAimIndicator != null)
            glueAimIndicator.SetActive(false);

        // โหลดไอเทมที่เลือกไว้
        int savedItemIndex = PlayerPrefs.GetInt("SelectedItem", 0); // ค่าเริ่มต้นคือ 0
        selectedItem = (ItemManager.ItemType)savedItemIndex;

    }

    void Update()
    {
        // ตรวจสอบว่าสามารถใช้ระบบได้หรือไม่
        if (!CanUseSystem())
        {
            HideAiming();
            return;
        }

        HandleItemSwitching();
        HandleGlueAiming();
        UpdateUI();
    }

    #region Item Switching System

    /// <summary>
    /// จัดการการสลับไอเทม
    /// </summary>
    /// 
    private void HandleItemSwitching()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) > 0.01f) // ตรวจสอบว่ามีการเลื่อนเมาส์
        {
            scrollAccumulator += scroll; // สะสมค่าการเลื่อน

            if (scrollAccumulator >= scrollThreshold) // ถ้าสะสมถึงเกณฑ์
            {
                SwitchItem(true); // เลื่อนไปข้างหน้า
                scrollAccumulator = 0f; // รีเซ็ตตัวสะสม
            }
            else if (scrollAccumulator <= -scrollThreshold) // ถ้าสะสมถึงเกณฑ์ในทิศทางตรงข้าม
            {
                SwitchItem(false); // เลื่อนไปข้างหลัง
                scrollAccumulator = 0f; // รีเซ็ตตัวสะสม
            }
        }
    }
    //private void HandleItemSwitching()
    //{
    //    //if (Input.GetKeyDown(KeyCode.Tab))
    //    //{
    //    //    SwitchItem();
    //    //}

    //    float scroll = Input.GetAxis("Mouse ScrollWheel");
    //    if (scroll != 0)
    //    {
    //        // scroll > 0 หมุนขึ้น , scroll < 0 หมุนลง
    //        SwitchItem(scroll > 0);
    //    }
    //}

    /// <summary>
    /// สลับระหว่างกาวและด้าย
    /// </summary>
    private void SwitchItem(bool forward)
    {
        int itemCount = System.Enum.GetValues(typeof(ItemManager.ItemType)).Length;
        int currentIndex = (int)selectedItem;

        currentIndex = (currentIndex + (forward ? 1 : -1) + itemCount) % itemCount;

        selectedItem = (ItemManager.ItemType)currentIndex;

        // บันทึกไอเทมที่เลือกไว้
        PlayerPrefs.SetInt("SelectedItem", currentIndex);
        PlayerPrefs.Save();

        Debug.Log($"Switched to: {selectedItem}");

        if (isAiming)
        {
            HideAiming();
        }
    }

    /// <summary>
    /// ดูไอเทมที่เลือกอยู่
    /// </summary>
    public ItemManager.ItemType GetSelectedItem()
    {
        return selectedItem;
    }

    #endregion

    #region Glue Aiming and Shooting

    /// <summary>
    /// จัดการการเล็งและยิงกาว
    /// </summary>
    private void HandleGlueAiming()
    {
        // เฉพาะเมื่อเลือกกาว
        if (selectedItem != ItemManager.ItemType.Glue)
        {
            HideAiming();
            return;
        }

        // ตรวจสอบว่ามีกาวหรือไม่
        if (ItemManager.Instance != null && !ItemManager.Instance.HasItem(ItemManager.ItemType.Glue))
        {
            HideAiming();
            return;
        }

        // คลิกขวา - เริ่มเล็ง
        if (Input.GetMouseButton(1))
        {
            StartAiming();
        }
        else
        {
            HideAiming();
        }

        // คลิกซ้าย - ยิงกาว (ขณะที่เล็งอยู่)
        if (Input.GetMouseButtonDown(0) && isAiming && canShoot)
        {
            ShootGlue();
        }
    }

    /// <summary>
    /// เริ่มการเล็ง
    /// </summary>
    private void StartAiming()
    {
        isAiming = true;

        // ดึงตำแหน่งเมาส์ใน World (2D → Z = 0)
        Vector3 mouseWorldPos3D = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 mouseWorldPos = new Vector2(mouseWorldPos3D.x, mouseWorldPos3D.y);

        // คำนวณทิศทางการเล็ง
        aimDirection = (mouseWorldPos - (Vector2)transform.position).normalized;

        // แสดง UI การเล็ง
        if (aimingCrosshair != null)
        {
            aimingCrosshair.SetActive(true);
            aimingCrosshair.transform.position = mouseWorldPos3D; // ใช้ Vector3 สำหรับตำแหน่ง GameObject
        }

        if (glueAimIndicator != null)
            glueAimIndicator.SetActive(true);

        // แสดงเส้นทางการยิง
        ShowTrajectoryPreview();
    }
    /// <summary>
    /// ซ่อนการเล็ง
    /// </summary>
    private void HideAiming()
    {
        isAiming = false;

        if (trajectoryLine != null)
            trajectoryLine.enabled = false;

        if (aimingCrosshair != null)
            aimingCrosshair.SetActive(false);

        if (glueAimIndicator != null)
            glueAimIndicator.SetActive(false);
    }

    /// <summary>
    /// แสดงเส้นทางการยิงกาว
    /// </summary>
    private void ShowTrajectoryPreview()
    {
        if (trajectoryLine == null) return;

        trajectoryLine.enabled = true;

        Vector3 startPos = firePoint != null ? firePoint.position : transform.position;
        Vector2 velocity = aimDirection * projectileSpeed;

        List<Vector3> points = new List<Vector3>();
        points.Add(startPos);

        Vector2 currentPos = startPos;
        Vector2 currentVel = velocity;

        for (int i = 0; i < trajectoryPoints; i++)
        {
            float dt = trajectoryTimeStep;

            // คำนวณตำแหน่งถัดไป
            Vector2 nextPos = currentPos + currentVel * dt + 0.5f * Physics2D.gravity * dt * dt;

            // ตรวจสอบการชน
            RaycastHit2D hit = Physics2D.Linecast(currentPos, nextPos, colliderLayers);
            if (hit.collider != null)
            {
                // ถ้าโดน collider ให้หยุด trajectory
                points.Add(hit.point);
                break;
            }
            else
            {
                points.Add(nextPos);
            }

            // อัปเดตตำแหน่งและความเร็ว
            currentVel += Physics2D.gravity * dt;
            currentPos = nextPos;

            // ถ้าเกิน max range ให้หยุด
            if (Vector2.Distance(startPos, currentPos) > maxShootingRange)
            {
                break;
            }
        }

        trajectoryLine.positionCount = points.Count;
        trajectoryLine.SetPositions(points.ToArray());
    }

    /// <summary>
    /// ยิงกาว
    /// </summary>
    private void ShootGlue()
    {
        // ตรวจสอบว่ามีกาวหรือไม่
        if (ItemManager.Instance != null)
        {
            if (!ItemManager.Instance.HasItem(ItemManager.ItemType.Glue))
            {
                Debug.Log("Cannot shoot glue - no glue available");
                return;
            }

            // ใช้กาว
            if (!ItemManager.Instance.UseItem(ItemManager.ItemType.Glue))
            {
                Debug.LogWarning("Failed to use glue");
                return;
            }
        }

        // สร้างกระสุนกาว
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject glueProjectile = Instantiate(glueProjectilePrefab, spawnPos, Quaternion.identity);

        // ตั้งค่าความเร็วให้กระสุน
        Rigidbody2D projectileRb = glueProjectile.GetComponent<Rigidbody2D>();
        if (projectileRb != null)
        {
            projectileRb.linearVelocity = aimDirection * projectileSpeed;
        }

        // ตั้งค่าการหมุน
        //float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        //glueProjectile.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        // เริ่มคูลดาวน์
        StartCoroutine(ShootCooldown());

        // ซ่อนการเล็งหลังยิง
        HideAiming();

        Debug.Log($"Glue shot! Direction: {aimDirection}, Speed: {projectileSpeed}");
    }

    /// <summary>
    /// คูลดาวน์การยิง
    /// </summary>
    private IEnumerator ShootCooldown()
    {
        canShoot = false;
        yield return new WaitForSeconds(shootCooldown);
        canShoot = true;
    }

    #endregion

    #region System State Checks

    /// <summary>
    /// ตรวจสอบว่าสามารถใช้ระบบได้หรือไม่
    /// </summary>
    private bool CanUseSystem()
    {
        if (!respectGameManagerState) return true;

        if (GameManager.Instance == null) return true;

        GameState currentState = GameManager.Instance.currentState;
        return currentState == GameState.Normal || currentState == GameState.RopeSwinging;
    }

    /// <summary>
    /// ตรวจสอบว่ามีไอเทมที่เลือกหรือไม่
    /// </summary>
    private bool HasSelectedItem()
    {
        if (ItemManager.Instance == null) return true;
        return ItemManager.Instance.HasItem(selectedItem);
    }

    #endregion

    #region UI Management

    /// <summary>
    /// อัปเดต UI
    /// </summary>
    private void UpdateUI()
    {
        

        if (glueIcon != null)
        {
            bool hasGlue = ItemManager.Instance != null && ItemManager.Instance.HasItem(ItemManager.ItemType.Glue);
            glueIcon.color = hasGlue ? availableColor : unavailableColor;
            glueIcon.transform.localScale = selectedItem == ItemManager.ItemType.Glue ? Vector3.one * selectedScale : Vector3.one * normalScale;
        }

        if (threadIcon != null)
        {
            bool hasThread = ItemManager.Instance != null && ItemManager.Instance.HasItem(ItemManager.ItemType.Thread);
            threadIcon.color = hasThread ? availableColor : unavailableColor;
            threadIcon.transform.localScale = selectedItem == ItemManager.ItemType.Thread ? Vector3.one * selectedScale : Vector3.one * normalScale;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// เซ็ตไอเทมที่เลือก (เรียกจากภายนอก)
    /// </summary>
    public void SetSelectedItem(ItemManager.ItemType itemType)
    {
        selectedItem = itemType;
        HideAiming();
    }

    /// <summary>
    /// ตรวจสอบว่ากำลังเล็งอยู่หรือไม่
    /// </summary>
    public bool IsAiming()
    {
        return isAiming;
    }

    /// <summary>
    /// ตรวจสอบว่ายิงได้หรือไม่
    /// </summary>
    public bool CanShoot()
    {
        return canShoot && HasSelectedItem();
    }

    #endregion

    #region Integration with Rope System

    /// <summary>
    /// ตรวจสอบว่า Rope กำลังใช้งานอยู่หรือไม่
    /// </summary>
    private bool IsRopeActive()
    {
        return ropeScript != null && ropeScript.IsRopeAttached();
    }

    #endregion

    #region Debug

    void OnDrawGizmos()
    {
        if (isAiming && selectedItem == ItemManager.ItemType.Glue)
        {
            // วาดทิศทางการเล็ง
            Vector3 startPos = firePoint != null ? firePoint.position : transform.position;
            Vector3 endPos = startPos + (Vector3)aimDirection * maxShootingRange;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(startPos, endPos);

            // วาดจุดเล็ง
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(mouseWorldPos, 0.3f);
        }
    }

    //// Debug Methods
    //[ContextMenu("Switch Item")]
    //public void Debug_SwitchItem()
    //{
    //    SwitchItem();
    //}

    [ContextMenu("Test Glue Shot")]
    public void Debug_TestGlueShot()
    {
        if (selectedItem == ItemManager.ItemType.Glue && HasSelectedItem())
        {
            aimDirection = Vector2.right;
            ShootGlue();
        }
    }

    #endregion
}