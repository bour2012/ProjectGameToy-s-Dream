using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct ButtonSoundPair
{
    public Button myButton;
    public AudioClip customSound;
}

public class SceneButtonSound : MonoBehaviour
{
    [Header("ตัวเล่นเสียงหลักประจำฉาก")]
    public AudioSource localAudioSource;

    [Header("1. ตั้งค่าปุ่ม UI บนหน้าจอ")]
    public ButtonSoundPair[] uiButtonSounds;

    [Header("2. ตั้งค่าปุ่มเหยียบ (Pressure Plate) ในฉาก")]
    public AudioClip pressurePlateSound; // ลากเสียงเหยียบปุ่มใส่แค่ตรงนี้ที่เดียว!
    [Header("3. ตั้งค่าคันโยก (Lever)")]
    public AudioClip leverToggleSound;
    void Start()
    {
        if (localAudioSource == null) return;

        // ============================================
        // ส่วนที่ 1: จัดการปุ่ม UI (แบบเดิม)
        // ============================================
        foreach (ButtonSoundPair pair in uiButtonSounds)
        {
            if (pair.myButton != null && pair.customSound != null)
            {
                AudioClip clipToPlay = pair.customSound;
                pair.myButton.onClick.AddListener(() =>
                {
                    localAudioSource.PlayOneShot(clipToPlay);
                });
            }
        }

        // ============================================
        // ส่วนที่ 2: จัดการ Pressure Plate อัตโนมัติ (ใหม่)
        // ============================================
        if (pressurePlateSound != null)
        {
            // สแกนหา PressurePlate ทุกตัวที่วางอยู่ในฉากนี้
            PressurePlate[] allPlates = FindObjectsByType<PressurePlate>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (PressurePlate plate in allPlates)
            {
                // บังคับยัด AudioSource และ ไฟล์เสียง จากศูนย์กลางเข้าไปให้มันเลย
                plate.audioSource = localAudioSource;
                plate.activateSound = pressurePlateSound;
            }
            Debug.Log($"เชื่อมต่อเสียงให้แท่นเหยียบทั้งหมด {allPlates.Length} อันเรียบร้อย!");
        }

        if (leverToggleSound != null)
        {
            // สแกนหา Lever ทุกตัวในฉาก
            Lever[] allLevers = FindObjectsByType<Lever>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (Lever lever in allLevers)
            {
                // ส่ง AudioSource และ เสียงสับคันโยก ไปให้ Lever ทุกตัว
                lever.audioSource = localAudioSource;
                lever.toggleSound = leverToggleSound;
            }
            Debug.Log($"เชื่อมต่อเสียงให้คันโยกทั้งหมด {allLevers.Length} อันเรียบร้อย!");
        }
    }
}