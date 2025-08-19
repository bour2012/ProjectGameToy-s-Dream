using UnityEngine;

public class Lever : MonoBehaviour
{
    public Transform handle;         // ส่วนคันโยกที่จะหมุน
    public float activeRotation = 90f;
    public float inactiveRotation = 45f;
    public bool isActive = false;

    public PlatformController platform;  // อ้างอิง Platform ที่จะถูกควบคุม

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            // ตรวจสอบว่าผู้เล่นอยู่ใกล้พอ (ไว้เช็คด้วย Trigger หรือระยะทาง)
            ToggleLever();
        }
    }

    void ToggleLever()
    {
        isActive = !isActive;

        // หมุนคันโยก
        float targetRot = isActive ? activeRotation : inactiveRotation;
        handle.localRotation = Quaternion.Euler(0, 0, targetRot);

        // เรียกให้ Platform ทำงาน
        if (platform != null)
            platform.Toggle(isActive);
    }
}
