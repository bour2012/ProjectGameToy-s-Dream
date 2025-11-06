using UnityEngine;

public class CameraLerpToTransform : MonoBehaviour
{
    public Transform target;
    public float followSpeed = 5f;
    public float cameraDepth = -10f;

    [Header("Offset Settings")]
    public Vector2 offset = new Vector2(0, 1);
    public float smoothTime = 0.3f;

    [Header("Zoom Settings")]
    public float zoomSmoothTime = 0.5f; // ความนุ่มนวลในการซูม
    private float targetSize;           // ขนาดกล้องที่ต้องการ
    private Camera cam;

    private Vector3 velocity = Vector3.zero;
    private float zoomVelocity; // ตัวช่วย SmoothDamp สำหรับ float

    void Start()
    {
        cam = GetComponent<Camera>();
        targetSize = cam.orthographicSize; // ตั้งค่าเริ่มต้นตามกล้อง
    }

    void LateUpdate()
    {
        if (!target) return;

        // ---- Follow ----
        Vector3 targetPosition = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            cameraDepth
        );

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref velocity,
            smoothTime,
            followSpeed
        );

        // ---- Zoom ----
        cam.orthographicSize = Mathf.SmoothDamp(
            cam.orthographicSize,
            targetSize,
            ref zoomVelocity,
            zoomSmoothTime
        );
    }

    public void SetZoneOffset(Vector2 newOffset)
    {
        offset = newOffset;
    }

    // เรียกจาก Zone เวลา Player เข้า
    public void SetTargetSize(float newSize)
    {
        targetSize = newSize;
    }
}