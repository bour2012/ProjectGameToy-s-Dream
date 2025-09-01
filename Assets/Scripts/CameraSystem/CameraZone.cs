using UnityEngine;

public class CameraZone : MonoBehaviour
{
    public float zoneCameraSize = 8f; // ขนาดกล้องเมื่อเข้ามาใน Zone นี้
    private float defaultSize;        // เก็บค่าเดิมไว้
    public Vector2 zoneOffset = Vector2.zero; // เพิ่มตัวแปรนี้ใน Inspector
    private Vector2 defaultOffset; // เก็บค่า offset เดิม

    private void Start()
    {
        defaultSize = Camera.main.orthographicSize;
        CameraLerpToTransform camFollow = Camera.main.GetComponent<CameraLerpToTransform>();
        if (camFollow != null)
        {
            defaultOffset = camFollow.offset;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            CameraLerpToTransform camFollow = Camera.main.GetComponent<CameraLerpToTransform>();
            if (camFollow != null)
            {
                camFollow.SetTargetSize(zoneCameraSize);
                camFollow.SetZoneOffset(zoneOffset); // ปรับ offset ตอนเข้า zone
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                CameraLerpToTransform camFollow = cam.GetComponent<CameraLerpToTransform>();
                if (camFollow != null)
                {
                    camFollow.SetTargetSize(defaultSize);
                    camFollow.SetZoneOffset(defaultOffset); // คืนค่า offset เดิม
                }
                else
                {
                    Debug.LogWarning("CameraLerpToTransform script not found on Main Camera!");
                }
            }
            else
            {
                Debug.LogWarning("Main Camera not found! Make sure the camera has the 'MainCamera' tag.");
            }
        }
    }
}
