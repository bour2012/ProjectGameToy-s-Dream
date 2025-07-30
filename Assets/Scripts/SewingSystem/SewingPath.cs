using System.Collections.Generic;
using UnityEngine;

public class SewingPath : MonoBehaviour
{
    [Header("Path Settings")]
    public List<Transform> pathPoints = new List<Transform>();
    LineRenderer pathRenderer;
    public Material pathMaterial;

    [Header("Visual Settings")]
    public Color pathColor = Color.white;
    public float pathWidth = 0.1f;
    public float toleranceRadius = 0.5f;

    [Header("Debug")]
    public bool showDebugCircles = true;

    [HideInInspector]
    public int nextWaypointIndex = 0;

    public GameObject waypointPrefab;
    private List<GameObject> waypointDots = new List<GameObject>();

    public Transform targetCenter;

    // ����������� path positions
    private List<Vector3> cachedPathPositions = new List<Vector3>();

    public int sortingOrder = 10;

    private void Start()
    {
        SetupPathRenderer();
        GeneratePathPoints();

    }

    private void OnEnable()
    {
       // SpawnWaypointAtCenter();
        if (pathPoints == null || pathPoints.Count == 0) return;

        // 1. �ӹǳ�ش�ٹ���ҧ�ͧ PathPoint ���
        Vector3 originalCenter = Vector3.zero;
        foreach (Transform point in pathPoints)
        {
            originalCenter += point.position;
        }
        originalCenter /= pathPoints.Count;

        // 2. �ӹǳ offset ��ѧ���˹觷����ҵ�ͧ��� (targetCenter)
        if (targetCenter == null)
        {
            Debug.LogWarning("targetCenter �ѧ������絤��� Inspector");
            return;
        }

        Vector3 offset = targetCenter.position - originalCenter;

        // 3. ��Ѻ PathPoint ���Шش ���������¹��� Z
        foreach (Transform point in pathPoints)
        {
            Vector3 newPos = point.position + offset;
            newPos.z = point.position.z; // ��͡ Z ���
            point.position = newPos;
        }
        if (waypointPrefab != null)
            SpawnWaypointDots();

        GeneratePathPoints();
        // 4. ���ҧ path ����
        // ForceRebuild();

    }

    #region === Public Methods ===

    public void ForceRebuild()
    {
        SetupPathRenderer();
        GeneratePathPoints();
    }

    public bool IsPointNearPath(Vector3 point, out float distance)
    {
        distance = float.MaxValue;

        if (cachedPathPositions.Count == 0)
        {
            Debug.LogWarning("No path positions available for checking!");
            return false;
        }

        foreach (Vector3 pathPoint in cachedPathPositions)
        {
            float dist = Vector3.Distance(point, pathPoint);
            if (dist < distance)
                distance = dist;
        }

        return distance <= toleranceRadius;
    }

    #endregion

    #region === Renderer Setup ===

    void SetupPathRenderer()
    {
        if (pathRenderer == null)
            pathRenderer = GetComponent<LineRenderer>() ?? gameObject.AddComponent<LineRenderer>();

       // pathRenderer.material = pathMaterial;
        pathRenderer.startColor = pathColor;
        pathRenderer.endColor = pathColor;
        pathRenderer.startWidth = pathWidth;
        pathRenderer.endWidth = pathWidth;
        pathRenderer.useWorldSpace = true;
        pathRenderer.sortingOrder = sortingOrder;

       
    }

    #endregion

    #region === Path Generation ===

    void GeneratePathPoints()
    {
        Debug.Log("pathPoints.Count = " + pathPoints.Count);

        if (pathPoints.Count < 2)
        {
            Debug.LogWarning("SewingPath needs at least 2 waypoints!");
            return;
        }

        List<Vector3> smoothPath = new List<Vector3>();

        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            if (pathPoints[i] == null || pathPoints[i + 1] == null)
            {
                Debug.LogWarning("Some waypoints are null in SewingPath!");
                continue;
            }

            Vector3 startPoint = pathPoints[i].position;
            Vector3 endPoint = pathPoints[i + 1].position;

            int segments = 10;
            for (int j = 0; j <= segments; j++)
            {
                float t = (float)j / segments;
                Vector3 point = Vector3.Lerp(startPoint, endPoint, t);
                smoothPath.Add(point);
            }
        }

        if (smoothPath.Count > 0)
        {
            pathRenderer.positionCount = smoothPath.Count;
            pathRenderer.SetPositions(smoothPath.ToArray());

            cachedPathPositions = smoothPath;
        }
    }

    #endregion

    #region === Waypoint Dots ===

    public void SpawnWaypointDots()
    {

        ClearOldDots();

        for (int i = 0; i < pathPoints.Count; i++)
        {
            if (pathPoints[i] == null) continue;

            Debug.Log($"Instantiating waypoint dot at {pathPoints[i].position}");

            GameObject dot = Instantiate(waypointPrefab, pathPoints[i].position, Quaternion.identity, this.transform);

            Renderer dotRenderer = dot.GetComponent<Renderer>();
            if (dotRenderer != null)
            {
                Debug.Log("Renderer found on waypoint dot");
                dotRenderer.sortingOrder = sortingOrder;
            }
            else
            {
                Debug.LogWarning("No Renderer found on waypointPrefab!");
            }

            waypointDots.Add(dot);
        }

        UpdateWaypointDotColors(0);
    }


    public void UpdateWaypointDotColors(int currentIndex)
    {
        for (int i = 0; i < waypointDots.Count; i++)
        {
            if (waypointDots[i] == null) continue;

            Color color;

            if (i == 0)
                color = Color.green;      // �ش�����
            else if (i == pathPoints.Count - 1)
                color = Color.red;        // �ش����ش
            else if (i == currentIndex)
                color = Color.cyan;       // �ش�Ѵ�
            else
                color = Color.white;      // �ش���

            Renderer dotRenderer = waypointDots[i].GetComponent<Renderer>();
            if (dotRenderer != null)
            {
                // �� sharedMaterial ���� material ����
                dotRenderer.material.color = color;

               
                var sprite = waypointDots[i].GetComponent<SpriteRenderer>();
                if (sprite != null)
                {
                    sprite.sortingLayerName = "Default"; // ���� "UI", �������ҧ����
                    sprite.sortingOrder = 6;           // ����ҡ����������
                }
            }
        }
    }


    void ClearOldDots()
    {
        foreach (var dot in waypointDots)
        {
            if (dot != null) Destroy(dot);
        }

        waypointDots.Clear();
    }

    #endregion

    #region === Gizmos (Editor Only) ===

    private void OnDrawGizmos()
    {
        if (pathPoints == null || pathPoints.Count == 0) return;

        for (int i = 0; i < pathPoints.Count; i++)
        {
            if (pathPoints[i] == null) continue;

            Vector3 pos = pathPoints[i].position;

            if (i == 0)
                Gizmos.color = Color.green;
            else if (i == pathPoints.Count - 1)
                Gizmos.color = Color.red;
            else if (i == nextWaypointIndex)
                Gizmos.color = Color.cyan;
            else
                Gizmos.color = new Color(1f, 1f, 1f, 0.2f);

            Gizmos.DrawSphere(pos, 0.15f);
        }
    }

    #endregion
}