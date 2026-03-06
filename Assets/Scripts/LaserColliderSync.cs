using UnityEngine;

[RequireComponent(typeof(LineRenderer), typeof(EdgeCollider2D))]
public class LaserColliderSync : MonoBehaviour
{
    private LineRenderer line;
    private EdgeCollider2D edge;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        edge = GetComponent<EdgeCollider2D>();
    }

    void Update()
    {
        // ป้องกัน Error กรณีเส้นยังไม่ถูกวาด
        if (line.positionCount == 0) return;

        Vector2[] points = new Vector2[line.positionCount];

        for (int i = 0; i < line.positionCount; i++)
        {
            Vector3 linePos = line.GetPosition(i);

            // เช็คว่า LineRenderer ใช้ World Space หรือไม่
            // เพื่อแปลงตำแหน่งให้ Collider (ซึ่งใช้ Local Space เสมอ) เข้าใจตรงกัน
            if (line.useWorldSpace)
            {
                points[i] = transform.InverseTransformPoint(linePos);
            }
            else
            {
                points[i] = linePos;
            }
        }

        // อัปเดตทรงของ Collider ให้เท่ากับเส้นกราฟิก
        edge.points = points;
    }
}