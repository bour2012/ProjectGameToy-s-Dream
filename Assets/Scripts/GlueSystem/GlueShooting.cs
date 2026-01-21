using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    [Tooltip("ปรับความโค้งของเส้นเล็ง ยิ่งน้อยยิ่งโค้งสูง, ยิ่งมากยิ่งพุ่งตรง")]
    [Range(0.1f, 2f)]
    public float trajectoryAimFactor = 1f;
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

    [Header("Audio")]
    public AudioClip switchSound;
    private AudioSource audioSource;

    // Private Variables
    private bool isAiming = false;
    private bool canShoot = true;
    private Vector2 aimDirection;
    private Vector3 mouseWorldPos;
    private ItemManager.ItemType selectedItem = ItemManager.ItemType.Glue;
    private Rope ropeScript;
    private Camera mainCamera;
    private float scrollAccumulator = 0f;
    private float scrollThreshold = 0.2f;
    private Animator animator;
    void Awake()
    {
        ropeScript = GetComponent<Rope>();
        mainCamera = Camera.main;
        animator = GetComponentInParent<Animator>();
        // Ensure local AudioSource exists for UI/feedback SFX
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D
        }
        if (trajectoryLine != null)
        {
            trajectoryLine.enabled = false;
            trajectoryLine.positionCount = trajectoryPoints;
        }
        if (aimingCrosshair != null) aimingCrosshair.SetActive(false);
        if (glueAimIndicator != null) glueAimIndicator.SetActive(false);
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
    private Vector3 GetMouseWorldPosition()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        Vector3 mouseScreenPos = Input.mousePosition;
        if (mainCamera.orthographic)
        {
            mouseScreenPos.z = mainCamera.nearClipPlane;
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);
            worldPos.z = transform.position.z;
            return worldPos;
        }
        else
        {
            Plane playerPlane = new Plane(Vector3.forward, transform.position);
            Ray ray = mainCamera.ScreenPointToRay(mouseScreenPos);
            if (playerPlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }
            else
            {
                float fallbackDistance = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
                mouseScreenPos.z = fallbackDistance;
                return mainCamera.ScreenToWorldPoint(mouseScreenPos);
            }
        }
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

            if (scrollAccumulator >= scrollThreshold || scrollAccumulator <= -scrollThreshold)
            {
                SwitchItem(scrollAccumulator >= scrollThreshold);
                scrollAccumulator = 0f;

                if (isAiming)
                {
                    HideAiming();
                }
             
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
        if (isAiming)
        {
            HideAiming();
        }
        // Play switch sound
        if (audioSource != null && switchSound != null)
        {
            audioSource.PlayOneShot(switchSound);
        }
    }

    public ItemManager.ItemType GetSelectedItem()
    {
        return selectedItem;
    }
    #endregion

    #region Glue Aiming and Shooting

    /// <summary>
    /// ฟังก์ชันกลางสำหรับคำนวณความเร็วที่ต้องใช้เพื่อยิงไปยังเป้าหมาย
    /// </summary>
    private Vector2 CalculateLaunchVelocity()
    {
        Vector2 startPoint = firePoint != null ? firePoint.position : transform.position;
        Vector2 endPoint = mouseWorldPos;
        Vector2 displacement = endPoint - startPoint;

        // ... (จำกัดระยะทางเหมือนเดิม) ...

        Vector2 gravity = Physics2D.gravity;


        float timeX = Mathf.Abs(displacement.x) / (projectileSpeed * trajectoryAimFactor);


        if (Mathf.Abs(displacement.x) < 0.1f)
        {
            timeX = Vector2.Distance(startPoint, endPoint) / projectileSpeed;
            if (Mathf.Approximately(timeX, 0f)) return Vector2.zero;
        }

        float initialVelocityY = (displacement.y / timeX) - (0.5f * gravity.y * timeX);

        // การคำนวณความเร็วแกน X แบบใหม่ จะได้ทิศทางที่ถูกต้องเองโดยอัตโนมัติ
        float initialVelocityX = displacement.x / timeX;

        Vector2 calculatedVelocity = new Vector2(initialVelocityX, initialVelocityY);

       
        if (calculatedVelocity.magnitude > projectileSpeed * 2f)
        {
            calculatedVelocity = calculatedVelocity.normalized * (projectileSpeed * 2f);
        }

        return calculatedVelocity;
    }

    private void HandleGlueAiming()
    {
        if (selectedItem != ItemManager.ItemType.Glue || (ItemManager.Instance != null && !ItemManager.Instance.HasItem(ItemManager.ItemType.Glue)))
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
        if (animator != null)
        {
            animator.SetBool("IsUseGlue", true);
        }
        mouseWorldPos = GetMouseWorldPosition();

        if (aimingCrosshair != null)
        {
            aimingCrosshair.SetActive(true);
            aimingCrosshair.transform.position = mouseWorldPos;
        }
        if (glueAimIndicator != null) glueAimIndicator.SetActive(true);
        ShowTrajectoryPreview();
    }

    private void HideAiming()
    {
        isAiming = false;
        if (animator != null)
        {
            animator.SetBool("IsUseGlue", false);
        }
        if (trajectoryLine != null) trajectoryLine.enabled = false;
        if (aimingCrosshair != null) aimingCrosshair.SetActive(false);
        if (glueAimIndicator != null) glueAimIndicator.SetActive(false);
    }

    private void ShowTrajectoryPreview()
    {
        if (trajectoryLine == null) return;

        Vector2 launchVelocity = CalculateLaunchVelocity();
        if (launchVelocity == Vector2.zero)
        {
            trajectoryLine.enabled = false;
            return;
        }

        trajectoryLine.enabled = true;

        List<Vector3> points = new List<Vector3>();
        Vector3 startPos = firePoint != null ? firePoint.position : transform.position;
        Vector3 targetPos = mouseWorldPos;
        float distanceToTarget = Vector2.Distance(startPos, targetPos);
        points.Add(startPos);

        for (int i = 1; i < trajectoryPoints; i++)
        {
            float t = i * trajectoryTimeStep;
            Vector2 posAtTimeT = (Vector2)startPos + launchVelocity * t + 0.5f * Physics2D.gravity * t * t;

            Vector3 lastPoint = points[points.Count - 1];
            RaycastHit2D hit = Physics2D.Linecast(lastPoint, posAtTimeT, colliderLayers);

            if (hit.collider != null)
            {
                points.Add(hit.point);
                break;
            }

            float distanceTravelled = Vector2.Distance(startPos, posAtTimeT);
            if (distanceTravelled > distanceToTarget || distanceTravelled > maxShootingRange)
            {
                points.Add(targetPos);
                break;
            }

            points.Add(posAtTimeT);
        }

        trajectoryLine.positionCount = points.Count;
        trajectoryLine.SetPositions(points.ToArray());
    }

    private void ShootGlue()
    {
        if (ItemManager.Instance != null && !ItemManager.Instance.UseItem(ItemManager.ItemType.Glue))
        {
            Debug.LogWarning("Failed to use glue");
            return;
        }

        Vector2 launchVelocity = CalculateLaunchVelocity();

        if (launchVelocity == Vector2.zero)
        {
            if (ItemManager.Instance != null) { ItemManager.Instance.CollectItem(ItemManager.ItemType.Glue, 1); }
            return;
        }

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject glueProjectile = Instantiate(glueProjectilePrefab, spawnPos, Quaternion.identity);
        Rigidbody2D projectileRb = glueProjectile.GetComponent<Rigidbody2D>();

        if (projectileRb != null)
        {
            projectileRb.linearVelocity = launchVelocity;
        }

        StartCoroutine(ShootCooldown());
        HideAiming();
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
        if (!respectGameManagerState || GameManager.Instance == null) return true;
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

    public bool IsAiming() => isAiming;
    public bool CanShoot() => canShoot && HasSelectedItem();
    #endregion

    #region Integration with Rope System
    private bool IsRopeActive() => ropeScript != null && ropeScript.IsRopeAttached();
    #endregion

    #region Debug
    void OnDrawGizmos()
    {
        if (isAiming && selectedItem == ItemManager.ItemType.Glue)
        {
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