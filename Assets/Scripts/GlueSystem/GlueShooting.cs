using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ========================================
// Glue Shooting System - ระบบยิงกาว
// รองรับกล้องแบบ Orthographic และ Perspective
// ========================================

public class GlueShooting : MonoBehaviour
{
    [Header("Glue Projectile")]
    public GameObject glueProjectilePrefab;
    public Transform firePoint;

    [Header("Shooting Settings")]
    public float projectileSpeed = 15f;
    public float maxShootingRange = 20f;
    public float shootCooldown = 1f;

    [Header("Trajectory Preview")]
    public LineRenderer trajectoryLine;
    public int trajectoryPoints = 30;
    public float trajectoryTimeStep = 0.1f;
    public LayerMask colliderLayers;

    [Header("UI")]
    public GameObject aimingCrosshair;
    public GameObject glueAimIndicator;

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
    private Vector3 mouseWorldPos;

    // Item Selection
    private ItemManager.ItemType selectedItem = ItemManager.ItemType.Glue;

    // Components
    private Rope ropeScript;
    private Camera mainCamera;

    private float scrollAccumulator = 0f;
    private float scrollThreshold = 0.2f;

    void Awake()
    {
        ropeScript = GetComponent<Rope>();
        mainCamera = Camera.main;

        if (trajectoryLine != null)
        {
            trajectoryLine.enabled = false;
            trajectoryLine.positionCount = trajectoryPoints;
        }

        if (aimingCrosshair != null)
            aimingCrosshair.SetActive(false);

        if (glueAimIndicator != null)
            glueAimIndicator.SetActive(false);

        int savedItemIndex = PlayerPrefs.GetInt("SelectedItem", 0);
        selectedItem = (ItemManager.ItemType)savedItemIndex;
    }

    void Update()
    {
        if (!CanUseSystem())
        {
            HideAiming();
            return;
        }

        HandleItemSwitching();
        HandleGlueAiming();
        UpdateUI();
    }

    #region Mouse Position Calculation

    /// <summary>
    /// คำนวณตำแหน่งเมาส์ใน World Space รองรับทั้ง Orthographic และ Perspective
    /// </summary>
    private Vector3 GetMouseWorldPosition()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        Vector3 mouseScreenPos = Input.mousePosition;

        if (mainCamera.orthographic)
        {
            // Orthographic Camera
            mouseScreenPos.z = mainCamera.nearClipPlane;
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);
            worldPos.z = transform.position.z; // ใช้ Z ของ Player
            return worldPos;
        }
        else
        {
            // Perspective Camera
            // สร้าง Plane ที่อยู่ที่ตำแหน่ง Z ของ Player
            Plane playerPlane = new Plane(Vector3.forward, transform.position);
            Ray ray = mainCamera.ScreenPointToRay(mouseScreenPos);

            if (playerPlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }
            else
            {
                // Fallback: ใช้ระยะห่างจากกล้อง
                float distanceFromCamera = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
                mouseScreenPos.z = distanceFromCamera;
                return mainCamera.ScreenToWorldPoint(mouseScreenPos);
            }
        }
    }

    /// <summary>
    /// คำนวณตำแหน่งเมาส์สำหรับ UI (World Space หรือ Screen Space)
    /// </summary>
    private Vector3 GetMouseUIPosition()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        Vector3 mouseScreenPos = Input.mousePosition;

        // สำหรับ UI ที่เป็น World Space
        if (aimingCrosshair != null && aimingCrosshair.GetComponent<RectTransform>() == null)
        {
            return GetMouseWorldPosition();
        }

        // สำหรับ UI ที่เป็น Screen Space
        return mouseScreenPos;
    }

    #endregion

    #region Item Switching System

    private void HandleItemSwitching()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) > 0.01f)
        {
            scrollAccumulator += scroll;

            if (scrollAccumulator >= scrollThreshold)
            {
                SwitchItem(true);
                scrollAccumulator = 0f;
            }
            else if (scrollAccumulator <= -scrollThreshold)
            {
                SwitchItem(false);
                scrollAccumulator = 0f;
            }
        }
    }

    private void SwitchItem(bool forward)
    {
        int itemCount = System.Enum.GetValues(typeof(ItemManager.ItemType)).Length;
        int currentIndex = (int)selectedItem;

        currentIndex = (currentIndex + (forward ? 1 : -1) + itemCount) % itemCount;
        selectedItem = (ItemManager.ItemType)currentIndex;

        PlayerPrefs.SetInt("SelectedItem", currentIndex);
        PlayerPrefs.Save();

        Debug.Log($"Switched to: {selectedItem}");

        if (isAiming)
        {
            HideAiming();
        }
    }

    public ItemManager.ItemType GetSelectedItem()
    {
        return selectedItem;
    }

    #endregion

    #region Glue Aiming and Shooting

    private void HandleGlueAiming()
    {
        if (selectedItem != ItemManager.ItemType.Glue)
        {
            HideAiming();
            return;
        }

        if (ItemManager.Instance != null && !ItemManager.Instance.HasItem(ItemManager.ItemType.Glue))
        {
            HideAiming();
            return;
        }

        if (Input.GetMouseButton(1))
        {
            StartAiming();
        }
        else
        {
            HideAiming();
        }

        if (Input.GetMouseButtonDown(0) && isAiming && canShoot)
        {
            ShootGlue();
        }
    }

    private void StartAiming()
    {
        isAiming = true;

        // ใช้ฟังก์ชันใหม่ที่รองรับทั้ง Orthographic และ Perspective
        mouseWorldPos = GetMouseWorldPosition();
        Vector2 mouseWorldPos2D = new Vector2(mouseWorldPos.x, mouseWorldPos.y);

        // คำนวณทิศทางการเล็ง
        Vector2 playerPos2D = new Vector2(transform.position.x, transform.position.y);
        aimDirection = (mouseWorldPos2D - playerPos2D).normalized;

        // แสดง UI การเล็ง
        if (aimingCrosshair != null)
        {
            aimingCrosshair.SetActive(true);

            // ถ้าเป็น World Space UI
            if (aimingCrosshair.GetComponent<RectTransform>() == null)
            {
                aimingCrosshair.transform.position = mouseWorldPos;
            }
            else
            {
                // ถ้าเป็น Screen Space UI
                aimingCrosshair.transform.position = Input.mousePosition;
            }
        }

        if (glueAimIndicator != null)
            glueAimIndicator.SetActive(true);

        ShowTrajectoryPreview();
    }

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

            Vector2 nextPos = currentPos + currentVel * dt + 0.5f * Physics2D.gravity * dt * dt;

            RaycastHit2D hit = Physics2D.Linecast(currentPos, nextPos, colliderLayers);
            if (hit.collider != null)
            {
                points.Add(hit.point);
                break;
            }
            else
            {
                // เก็บค่า Z เดิมไว้สำหรับ Perspective Camera
                Vector3 point3D = nextPos;
                point3D.z = startPos.z;
                points.Add(point3D);
            }

            currentVel += Physics2D.gravity * dt;
            currentPos = nextPos;

            if (Vector2.Distance(startPos, currentPos) > maxShootingRange)
            {
                break;
            }
        }

        trajectoryLine.positionCount = points.Count;
        trajectoryLine.SetPositions(points.ToArray());
    }

    private void ShootGlue()
    {
        if (ItemManager.Instance != null)
        {
            if (!ItemManager.Instance.HasItem(ItemManager.ItemType.Glue))
            {
                Debug.Log("Cannot shoot glue - no glue available");
                return;
            }

            if (!ItemManager.Instance.UseItem(ItemManager.ItemType.Glue))
            {
                Debug.LogWarning("Failed to use glue");
                return;
            }
        }

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject glueProjectile = Instantiate(glueProjectilePrefab, spawnPos, Quaternion.identity);

        Rigidbody2D projectileRb = glueProjectile.GetComponent<Rigidbody2D>();
        if (projectileRb != null)
        {
            projectileRb.linearVelocity = aimDirection * projectileSpeed;
        }

        StartCoroutine(ShootCooldown());
        HideAiming();

        Debug.Log($"Glue shot! Direction: {aimDirection}, Speed: {projectileSpeed}");
    }

    private IEnumerator ShootCooldown()
    {
        canShoot = false;
        yield return new WaitForSeconds(shootCooldown);
        canShoot = true;
    }

    #endregion

    #region System State Checks

    private bool CanUseSystem()
    {
        if (!respectGameManagerState) return true;
        if (GameManager.Instance == null) return true;

        GameState currentState = GameManager.Instance.currentState;
        return currentState == GameState.Normal || currentState == GameState.RopeSwinging;
    }

    private bool HasSelectedItem()
    {
        if (ItemManager.Instance == null) return true;
        return ItemManager.Instance.HasItem(selectedItem);
    }

    #endregion

    #region UI Management

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

    public void SetSelectedItem(ItemManager.ItemType itemType)
    {
        selectedItem = itemType;
        HideAiming();
    }

    public bool IsAiming()
    {
        return isAiming;
    }

    public bool CanShoot()
    {
        return canShoot && HasSelectedItem();
    }

    #endregion

    #region Integration with Rope System

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
            Vector3 startPos = firePoint != null ? firePoint.position : transform.position;
            Vector3 endPos = startPos + (Vector3)aimDirection * maxShootingRange;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(startPos, endPos);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(mouseWorldPos, 0.3f);
        }
    }

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