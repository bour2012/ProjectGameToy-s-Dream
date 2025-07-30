using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SewingController : MonoBehaviour
{
    [Header("Rendering Settings")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 2;

    [Header("References")]
    public Camera mainCamera;
    public SewingPath targetPath;
    public LineRenderer playerLineRenderer;
    public DollRepairSystem dollRepairSystem; // เพิ่มการเชื่อมต่อกับ DollRepairSystem

    [Header("Player Line Settings")]
    public Material playerLineMaterial;
    public Color correctColor = Color.green;
    public Color wrongColor = Color.red;
    public float lineWidth = 0.08f;

    [Header("Game Settings")]
    public float minDistanceThreshold = 0.1f; // ระยะห่างขั้นต่ำระหว่างจุด
    public float successThreshold = 80f; // เพิ่มตัวแปรสำหรับตั้งค่าเปอร์เซ็นต์ที่ถือว่าผ่าน

    private List<Vector3> playerPath = new List<Vector3>();
    private bool isSewing = false;
    private bool isStarted = false;
    private Vector3 lastMousePosition;
    private int currentWaypointIndex = 0;

    // Events
    public System.Action<float> OnAccuracyUpdated;
    public System.Action OnSewingCompleted; // Event สำหรับระบบภายใน
    public System.Action OnSewingSystemCompleted; // Event สำหรับแจ้ง DollRepairSystem

    private void Start()
    {
        SetupPlayerLineRenderer();
        if (mainCamera == null)
            mainCamera = Camera.main;

        // หา DollRepairSystem อัตโนมัติถ้าไม่ได้กำหนด
        if (dollRepairSystem == null)
        {
            dollRepairSystem = GetComponentInParent<DollRepairSystem>();
            if (dollRepairSystem == null)
            {
                dollRepairSystem = FindFirstObjectByType<DollRepairSystem>();
            }
        }

        // บังคับให้ SewingPath สร้าง path ใหม่
        if (targetPath != null)
            targetPath.ForceRebuild(); // ? เพิ่มเมธอดนี้ใน SewingPath
    }

    void UpdateLineColor(Vector3 point)
    {
        // ตรวจสอบว่า targetPath ไม่เป็น null
        if (targetPath == null)
        {
            Debug.LogWarning("Target Path is not assigned in SewingController!");
            return;
        }

        if (targetPath.IsPointNearPath(point, out float distance))
        {
            playerLineRenderer.startColor = correctColor;
            playerLineRenderer.endColor = correctColor;
        }
        else
        {
            playerLineRenderer.startColor = wrongColor;
            playerLineRenderer.endColor = wrongColor;
        }
    }

    bool DidPlayerFollowPathExactly()
    {
        if (targetPath == null || targetPath.pathPoints.Count == 0)
            return false;

        List<Vector3> requiredPoints = targetPath.pathPoints
            .Where(p => p != null)
            .Select(p => p.position)
            .ToList();

        int currentIndex = 0;

        for (int i = 0; i < playerPath.Count; i++)
        {
            float dist = Vector3.Distance(playerPath[i], requiredPoints[currentIndex]);

            if (dist <= targetPath.toleranceRadius)
            {
                currentIndex++; // ผ่านจุดนี้แล้ว
                if (currentIndex >= requiredPoints.Count)
                    break; // ครบทุกจุดแล้ว
            }
        }

        // ถ้า currentIndex เท่ากับจำนวนจุดทั้งหมด = วาดผ่านครบทุกจุด
        return currentIndex == requiredPoints.Count;
    }

    void SetupPlayerLineRenderer()
    {
        if (playerLineRenderer == null)
            playerLineRenderer = gameObject.AddComponent<LineRenderer>();

        playerLineRenderer.material = playerLineMaterial;
        playerLineRenderer.startWidth = lineWidth;
        playerLineRenderer.endWidth = lineWidth;
        playerLineRenderer.useWorldSpace = true;

        playerLineRenderer.sortingLayerName = sortingLayerName;
        playerLineRenderer.sortingOrder = sortingOrder;

        playerLineRenderer.positionCount = 0;
    }

    private void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        Vector3 mouseWorldPos = GetMouseWorldPosition();

        if (Input.GetMouseButtonDown(0))
        {
            StartSewing(mouseWorldPos);
        }
        else if (Input.GetMouseButton(0) && isSewing)
        {
            UpdateSewing(mouseWorldPos);
        }
        else if (Input.GetMouseButtonUp(0) && isSewing)
        {
            EndSewing();
        }
    }

    Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = 10f; // ระยะห่างจากกล้อง
        return mainCamera.ScreenToWorldPoint(mousePos);
    }

    void StartSewing(Vector3 startPosition)
    {
        isSewing = true;
        isStarted = true;
        playerPath.Clear();
        currentWaypointIndex = 0;

        AddPointToPath(startPosition);
        lastMousePosition = startPosition;

        // ? Reset จุดถัดไปในเส้นทาง
        if (targetPath != null)
        {
            targetPath.nextWaypointIndex = 0;
            targetPath.UpdateWaypointDotColors(0);
        }

        Debug.Log("Started sewing at: " + startPosition);
    }

    void UpdateSewing(Vector3 currentPosition)
    {
        if (Vector3.Distance(lastMousePosition, currentPosition) >= minDistanceThreshold)
        {
            AddPointToPath(currentPosition);
            lastMousePosition = currentPosition;

            CheckAccuracy();
            UpdateNextWaypointIndex(currentPosition); // ? เพิ่มบรรทัดนี้
        }
    }

    void UpdateNextWaypointIndex(Vector3 currentPos)
    {
        if (targetPath == null || targetPath.pathPoints.Count == 0)
            return;

        if (currentWaypointIndex >= targetPath.pathPoints.Count)
            return;

        Vector3 targetPoint = targetPath.pathPoints[currentWaypointIndex].position;
        float dist = Vector3.Distance(currentPos, targetPoint);

        if (dist <= targetPath.toleranceRadius)
        {
            currentWaypointIndex++;
            targetPath.nextWaypointIndex = currentWaypointIndex;

            // เพิ่มบรรทัดนี้:
            targetPath.UpdateWaypointDotColors(currentWaypointIndex);
        }
    }

    void AddPointToPath(Vector3 point)
    {
        playerPath.Add(point);

        // อัปเดต LineRenderer
        playerLineRenderer.positionCount = playerPath.Count;
        playerLineRenderer.SetPositions(playerPath.ToArray());

        // เปลี่ยนสีตามความแม่นยำ
        UpdateLineColor(point);
    }

    void EndSewing()
    {
        isSewing = false;
        Debug.Log("Sewing ended. Total points: " + playerPath.Count);

        float finalAccuracy = CalculateFinalAccuracy();
        bool followedExactly = DidPlayerFollowPathExactly();

        if (followedExactly || finalAccuracy >= successThreshold)
        {
            Debug.Log("✅ Passed! Final Accuracy: " + finalAccuracy.ToString("F2") + "%");

            // เรียก event ว่าทำสำเร็จ (สำหรับระบบภายใน)
            OnSewingCompleted?.Invoke();

            // แจ้งไปยัง DollRepairSystem ว่าระบบเย็บผ้าเสร็จแล้ว
            NotifyRepairSystemCompleted();
        }
        else
        {
            Debug.Log("❌ Not Passed. Accuracy: " + finalAccuracy.ToString("F2") + "%");
        }

        // อัปเดต Accuracy UI
        OnAccuracyUpdated?.Invoke(finalAccuracy);
    }

    void CheckAccuracy()
    {
        if (playerPath.Count == 0) return;

        float accuracy = CalculateCurrentAccuracy();
        OnAccuracyUpdated?.Invoke(accuracy);
    }

    public float CalculateCurrentAccuracy()
    {
        if (playerPath.Count == 0) return 0f;

        int correctPoints = 0;

        foreach (Vector3 point in playerPath)
        {
            if (targetPath.IsPointNearPath(point, out float distance))
            {
                correctPoints++;
            }
        }

        return ((float)correctPoints / playerPath.Count) * 100f;
    }

    float CalculateFinalAccuracy()
    {
        return CalculateCurrentAccuracy();
    }

    /// <summary>
    /// แจ้งไปยัง DollRepairSystem ว่าระบบเย็บผ้าเสร็จสมบูรณ์แล้ว
    /// </summary>
    void NotifyRepairSystemCompleted()
    {
        // เรียก Event สำหรับระบบอื่นๆ ฟัง
        OnSewingSystemCompleted?.Invoke();

        // แจ้งไปยัง DollRepairSystem โดยตรง
        if (dollRepairSystem != null)
        {
            dollRepairSystem.OnSubSystemCompleted("Sewing");
        }
        else
        {
            Debug.LogWarning("DollRepairSystem not found! Cannot notify sewing completion.");
        }
    }

    // ฟังก์ชันรีเซ็ต
    public void ResetSewing()
    {
        playerPath.Clear();
        playerLineRenderer.positionCount = 0;
        isSewing = false;
        isStarted = false;
        currentWaypointIndex = 0;

        // Reset target path ถ้ามี
        if (targetPath != null)
        {
            targetPath.nextWaypointIndex = 0;
            targetPath.UpdateWaypointDotColors(0);
        }
    }

    /// <summary>
    /// ตรวจสอบว่าระบบเย็บผ้าเสร็จสมบูรณ์แล้วหรือไม่
    /// </summary>
    public bool IsCompleted()
    {
        if (playerPath.Count == 0) return false;

        float accuracy = CalculateCurrentAccuracy();
        bool followedExactly = DidPlayerFollowPathExactly();

        return followedExactly || accuracy >= successThreshold;
    }

    /// <summary>
    /// ได้รับความแม่นยำปัจจุบัน
    /// </summary>
    public float GetCurrentAccuracy()
    {
        return CalculateCurrentAccuracy();
    }

    /// <summary>
    /// บังคับให้ระบบเสร็จสิ้น (สำหรับ Debug หรือ Testing)
    /// </summary>
    [ContextMenu("Force Complete")]
    public void ForceComplete()
    {
        NotifyRepairSystemCompleted();
    }
}