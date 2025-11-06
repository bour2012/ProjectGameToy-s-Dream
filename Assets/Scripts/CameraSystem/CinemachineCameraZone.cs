using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(Collider2D))]
public class CinemachineCameraZone : MonoBehaviour
{
    [Header("Zone Camera Settings")]
    [Tooltip("กล้องของ Zone นี้ที่จะ Active เมื่อ Player เข้ามา")]
    [SerializeField] private CinemachineCamera zoneCam;

    [Header("Priority Settings")]
    [Tooltip("Priority เมื่อ Player อยู่ใน Zone (ควรสูงกว่ากล้องหลัก)")]
    [SerializeField] private int activePriority = 15;

    [Tooltip("Priority เมื่อ Player ออกจาก Zone")]
    [SerializeField] private int inactivePriority = 5;

    [Header("Default Camera")]
    [Tooltip("กล้องหลัก (ติดกับ Player)")]
    [SerializeField] private CinemachineCamera defaultCam;

    [Header("Perspective Settings")]
    [Tooltip("มุมมองของกล้องเมื่อเข้า Zone (เฉพาะ Perspective Mode)")]
    [Range(20f, 100f)]
    [SerializeField] private float zoneFieldOfView = 60f;

    [Tooltip("มุมมองเดิมของกล้องเมื่อออกจาก Zone")]
    private float defaultFieldOfView = 60f;

    private void Start()
    {
        if (zoneCam == null)
        {
            Debug.LogError($"❌ Zone Camera ไม่ได้ถูก assign ใน {gameObject.name}!", this);
            return;
        }


        // หา DefaultCam ถ้ายังไม่ได้ใส่
        if (defaultCam == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                defaultCam = player.GetComponentInChildren<CinemachineCamera>();
            }
        }

        if (defaultCam != null)
        {
            defaultFieldOfView = defaultCam.Lens.FieldOfView;
        }

        // ปิด ZoneCam ตอนเริ่มต้น
        zoneCam.Priority.Value = inactivePriority;
        zoneCam.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (zoneCam != null)
        {
            zoneCam.enabled = true;
            zoneCam.Priority.Value = activePriority;

            // ปรับ Field of View สำหรับ Perspective
            zoneCam.Lens.FieldOfView = zoneFieldOfView;

            Debug.Log($"[Camera Zone] ▶ เข้า Zone: {gameObject.name} | Camera: {zoneCam.name} | FOV: {zoneFieldOfView}");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (zoneCam != null)
        {
            zoneCam.enabled = false;
            zoneCam.Priority.Value = inactivePriority;
        }

        if (defaultCam != null)
        {
            // คืนค่า Field of View ของกล้องหลัก
            defaultCam.Lens.FieldOfView = defaultFieldOfView;
        }

        Debug.Log($"[Camera Zone] ◀ ออกจาก Zone: {gameObject.name}");
    }

    // --- Helper Methods ---
    public void ActivateZoneCamera()
    {
        if (zoneCam == null) return;

        zoneCam.enabled = true;
        zoneCam.Priority.Value = activePriority;
        zoneCam.Lens.FieldOfView = zoneFieldOfView;
    }

    public void DeactivateZoneCamera()
    {
        if (zoneCam == null) return;

        zoneCam.enabled = false;
        zoneCam.Priority.Value = inactivePriority;
    }
}
