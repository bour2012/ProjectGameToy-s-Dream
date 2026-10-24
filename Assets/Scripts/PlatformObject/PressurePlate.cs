using UnityEngine;
using System.Collections.Generic;

public class PressurePlate : MonoBehaviour
{
    public PlatformController platform;
    public LayerMask triggerLayers; // กำหนด LayerMask สำหรับ Object ที่จะตรวจสอบ

    [Header("Audio Settings")]
    public AudioSource audioSource;   // ตัวเล่นเสียง (ลากมาใส่ หรือปล่อยว่างเพื่อให้โค้ดหาเอง)
    public AudioClip activateSound;   // ไฟล์เสียงตอนเหยียบ
    public AudioClip deactivateSound; // ไฟล์เสียงตอนปล่อย

    private readonly HashSet<Collider2D> collidersOnPlate = new HashSet<Collider2D>();

    void OnTriggerEnter2D(Collider2D other)
    {
        // ตรวจสอบว่า Object ที่เข้ามาอยู่ใน Layer ที่กำหนดหรือไม่
        if (IsInLayerMask(other.gameObject, triggerLayers))
        {
            bool wasEmpty = collidersOnPlate.Count == 0;
            collidersOnPlate.Add(other);

            if (wasEmpty)
            {
                if (platform != null) platform.Toggle(true); // เปิดแพลตฟอร์ม
                Debug.Log("Pressure Plate Activated: Open");

                PlaySound(activateSound);
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        // ตรวจสอบว่า Object ที่ออกไปอยู่ใน Layer ที่กำหนดหรือไม่
        if (IsInLayerMask(other.gameObject, triggerLayers))
        {
            collidersOnPlate.Remove(other);
            CleanupInvalidColliders();

            if (collidersOnPlate.Count == 0)
            {
                if (platform != null) platform.Toggle(false); // ปิดแพลตฟอร์ม
                Debug.Log("Pressure Plate Deactivated: Close");

                PlaySound(deactivateSound);
            }
        }
    }

    private void FixedUpdate()
    {
        // ป้องกันบั๊ก: วัตถุที่เหยียบอยู่ถูกทำลาย (Destroy) หรือ Disable กะทันหันขณะอยู่บนแป้น
        if (collidersOnPlate.Count > 0)
        {
            int beforeCount = collidersOnPlate.Count;
            CleanupInvalidColliders();

            if (beforeCount > 0 && collidersOnPlate.Count == 0)
            {
                if (platform != null) platform.Toggle(false);
                Debug.Log("Pressure Plate Auto-Reset (Object destroyed/removed): Close");
                PlaySound(deactivateSound);
            }
        }
    }

    private void CleanupInvalidColliders()
    {
        collidersOnPlate.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy || !c.enabled);
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