using UnityEngine;

public class CameraZone : MonoBehaviour
{
    public float zoneCameraSize = 8f; // ขนาดกล้องเมื่อเข้ามาใน Zone นี้
    private float defaultSize;        // เก็บค่าเดิมไว้

    private void Start()
    {
        // ค่าเริ่มต้นของกล้อง
        defaultSize = Camera.main.orthographicSize;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            CameraLerpToTransform camFollow = Camera.main.GetComponent<CameraLerpToTransform>();
            if (camFollow != null)
            {
                camFollow.SetTargetSize(zoneCameraSize);
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
