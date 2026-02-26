using UnityEngine;

public class BGMTrigger : MonoBehaviour
{
    [Header("ใส่เพลงที่ต้องการเล่นตรงนี้")]
    public AudioClip musicToPlay;

    // ฟังก์ชันนี้จะถูกเรียกจาก Event ใน Inspector
    public void TriggerMusic()
    {
        // ใช้คำสั่งที่คุณต้องการ เพื่อหา GameManager
        GameManager gm = FindFirstObjectByType<GameManager>();

        if (gm != null && musicToPlay != null)
        {
            gm.PlayBGM(musicToPlay);
        }
        else
        {
            Debug.LogWarning("หา GameManager ไม่เจอ หรือยังไม่ได้ใส่เพลง!");
        }
    }
}
