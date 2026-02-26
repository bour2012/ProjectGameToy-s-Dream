using UnityEngine;

public class BGMTrigger : MonoBehaviour
{
    [Header("ใส่เพลงที่ต้องการเล่นตรงนี้")]
    [Tooltip("ถ้าไม่ใส่เพลง (ปล่อยเป็น None) จะเป็นการสั่งปิดเพลงของด่านที่แล้วแทน")]
    public AudioClip musicToPlay;

    // ฟังก์ชันนี้จะถูกเรียกจาก Event ใน Inspector
    public void TriggerMusic()
    {
        // อ้างอิงไปหา GameManager ผ่าน Singleton
        GameManager gm = GameManager.Instance;

        // กันเหนียวกรณี GameManager หาผ่าน Instance ไม่เจอ
        if (gm == null)
        {
            gm = FindFirstObjectByType<GameManager>();
        }

        if (gm != null)
        {
            if (musicToPlay != null)
            {
                // ถ้ามีไฟล์เพลงใส่มา ให้สั่งเล่นเพลงตามปกติ
                gm.PlayBGM(musicToPlay);
            }
            else
            {
                // ถ้า "ไม่ใส่เพลง" (ช่องว่างเปล่า) ให้วิ่งไปสั่งปิด Audio Source ของ GameManager โดยตรง
                if (gm.bgmAudioSource != null)
                {
                    gm.bgmAudioSource.Stop();
                    gm.bgmAudioSource.clip = null; // เคลียร์ชื่อเพลงเก่าออกด้วย
                    Debug.Log("BGMTrigger: สั่งหยุดเพลงเรียบร้อยแล้ว");
                }
            }
        }
        else
        {
            Debug.LogWarning("BGMTrigger: หา GameManager ไม่เจอ!");
        }
    }
}
