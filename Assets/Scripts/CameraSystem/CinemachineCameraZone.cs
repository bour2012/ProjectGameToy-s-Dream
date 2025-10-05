using UnityEngine;
using Unity.Cinemachine;

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
    [Tooltip("กล้องหลัก (ปกติติดอยู่กับ Player) - ไม่ใส่ก็ได้ถ้าไม่ต้องการจัดการ Priority")]
    [SerializeField] private CinemachineCamera defaultCam;

    [Header("Optional Settings")]
    [SerializeField] private bool resetDefaultCamPriority = true;
    [SerializeField] private int defaultCamInactivePriority = 10;

    private void Start()
    {
        zoneCam.enabled = false;

        if (zoneCam == null)
        {
            Debug.LogError($"Zone Camera ไม่ได้ถูก assign ใน {gameObject.name}!", this);
            return;
        }

        // ตั้งค่า Priority เริ่มต้นให้ Zone Camera ต่ำ
        zoneCam.Priority.Value = inactivePriority;

        // ถ้าไม่ได้ระบุ defaultCam ให้ลองหาจาก Player
        if (defaultCam == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                defaultCam = player.GetComponentInChildren<CinemachineCamera>();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (zoneCam != null)
        {
            // เปิดใช้กล้องของ Zone
            zoneCam.enabled = true;
            zoneCam.Priority.Value = activePriority;

            Debug.Log($"[Camera Zone] เข้า Zone: {gameObject.name} | Camera: {zoneCam.name}");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (zoneCam != null)
        {
            // ปิดใช้กล้องของ Zone
            zoneCam.enabled = false;
            zoneCam.Priority.Value = inactivePriority;


            Debug.Log($"[Camera Zone] ออกจาก Zone: {gameObject.name}");
        }
    }

    // Helper method สำหรับเปลี่ยนกล้องจาก script อื่น
    public void ActivateZoneCamera()
    {
        if (zoneCam != null)
        {
            zoneCam.enabled = true;
            zoneCam.Priority.Value = activePriority;
        }
    }

    public void DeactivateZoneCamera()
    {
        if (zoneCam != null)
        {
            zoneCam.enabled = false;
            zoneCam.Priority.Value = inactivePriority;
        }
    }
}