using UnityEngine;

public class PressurePlate : MonoBehaviour
{
    public PlatformController platform;
    public LayerMask triggerLayers; // กำหนด LayerMask สำหรับ Object ที่จะตรวจสอบ
    private int objectsOnPlate = 0;

    [Header("Audio Settings")]
    public AudioSource audioSource;   // ตัวเล่นเสียง (ลากมาใส่ หรือปล่อยว่างเพื่อให้โค้ดหาเอง)
    public AudioClip activateSound;   // ไฟล์เสียงตอนเหยียบ

    //private void Start()
    //{
    //    // ถ้าลืมลาก AudioSource มาใส่ โค้ดจะพยายามหาจากในตัวมันเองให้
    //    if (audioSource == null)
    //    {
    //        audioSource = GetComponent<AudioSource>();
    //    }
    //}

    void OnTriggerEnter2D(Collider2D other)
    {
        // ตรวจสอบว่า Object ที่เข้ามาอยู่ใน Layer ที่กำหนดหรือไม่
        if (IsInLayerMask(other.gameObject, triggerLayers))
        {
            objectsOnPlate++;
            platform.Toggle(true); // เปิดแพลตฟอร์ม
            Debug.Log("Pressure Plate Activated: Open");

            // --- เล่นเสียงตรงนี้ ---
            PlaySound(activateSound);
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

                // ถ้าอยากให้มีเสียงตอน "ปล่อย" ด้วย ให้เพิ่ม PlaySound(deactivateSound) ตรงนี้ครับ
            }
        }
    }

    // ฟังก์ชันช่วยเล่นเสียง (กัน Error กรณีลืมใส่เสียง)
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            // ใช้ PlayOneShot เพื่อให้เสียงเล่นทับกันได้ (กรณีเหยียบรัวๆ)
            audioSource.PlayOneShot(clip);
        }
    }

    // ฟังก์ชันตรวจสอบว่า GameObject อยู่ใน LayerMask หรือไม่
    private bool IsInLayerMask(GameObject obj, LayerMask layerMask)
    {
        return (layerMask.value & (1 << obj.layer)) > 0;
    }
}