using UnityEngine;

public class PressurePlate : MonoBehaviour
{
    public PlatformController platform;
    public LayerMask triggerLayers; // กำหนด LayerMask สำหรับ Object ที่จะตรวจสอบ
    private int objectsOnPlate = 0;

    void OnTriggerEnter2D(Collider2D other)
    {
        // ตรวจสอบว่า Object ที่เข้ามาอยู่ใน Layer ที่กำหนดหรือไม่
        if (IsInLayerMask(other.gameObject, triggerLayers))
        {
            objectsOnPlate++;
            platform.Toggle(true); // เปิดแพลตฟอร์ม
            Debug.Log("Pressure Plate Activated: Open");
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        // ตรวจสอบว่า Object ที่ออกไปอยู่ใน Layer ที่กำหนดหรือไม่
        if (IsInLayerMask(other.gameObject, triggerLayers))
        {
            objectsOnPlate--;
            if (objectsOnPlate <= 0)
            {
                platform.Toggle(false); // ปิดแพลตฟอร์ม
                Debug.Log("Pressure Plate Deactivated: Close");
            }
        }
    }

    // ฟังก์ชันตรวจสอบว่า GameObject อยู่ใน LayerMask หรือไม่
    private bool IsInLayerMask(GameObject obj, LayerMask layerMask)
    {
        return (layerMask.value & (1 << obj.layer)) > 0;
    }
}
