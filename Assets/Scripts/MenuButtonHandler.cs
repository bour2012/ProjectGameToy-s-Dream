using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// ▼▼▼ 1. ต้องเพิ่มบรรทัดนี้เพื่อใช้งาน Timeline ▼▼▼
using UnityEngine.Playables;

public class MenuButtonHandler : MonoBehaviour
{
    [Header("Button Animation (Optional)")]
    [Tooltip("Animator ของปุ่ม Play (ถ้ามี)")]
    public Animator buttonAnimator;
    [Tooltip("ชื่อ Trigger ใน Animator ที่จะให้เล่นตอนกด")]
    public string animationTrigger = "Pressed";

    // ▼▼▼ 2. ส่วนที่เพิ่มใหม่สำหรับ Timeline ▼▼▼
    [Header("Timeline Sequence (Optional)")]
    [Tooltip("ลาก GameObject ที่มี PlayableDirector (Timeline) มาใส่ตรงนี้")]
    public PlayableDirector sequenceTimeline;

    [Tooltip("ถ้าติ๊กถูก จะรอให้ Timeline เล่นจบก่อนค่อยเปลี่ยนซีน / ถ้าไม่ติ๊ก จะใช้เวลาจากช่อง Delay ด้านล่างแทน")]
    public bool waitForTimelineToEnd = true;
    // ▲▲▲

    [Header("General Settings")]
    [Tooltip("เวลารอพื้นฐาน (จะถูกใช้ถ้าไม่มี Timeline หรือไม่ได้ติ๊ก waitForTimelineToEnd)")]
    public float delayBeforeLoad = 0.5f;

    [Header("Connection")]
    [Tooltip("ลากตัว SceneTransition (ที่มี Script เปลี่ยนฉาก) มาใส่ตรงนี้")]
    public SceneTransition sceneTransition;

    // ฟังก์ชันนี้จะเอาไปใส่ในปุ่ม OnClick
    public void OnClickPlay()
    {

        // 1. ลบจุดเซฟ (Spawn Point)
        PlayerPrefs.DeleteKey("LastCheckpoint");
        PlayerPrefs.DeleteKey("CheckpointX");
        PlayerPrefs.DeleteKey("CheckpointY");
        PlayerPrefs.DeleteKey("CheckpointZ");

        // 2. ลบจำนวนไอเทมที่เก็บไว้
        PlayerPrefs.DeleteKey("Saved_Glue");
        PlayerPrefs.DeleteKey("Saved_Thread");

        // 3. ลบประวัติ Dialog และ Checkpoint Bonus
        PlayerPrefs.DeleteKey("PlayedDialogIDs");   // ชื่อ Key จากโค้ด GameManager เก่า
        PlayerPrefs.DeleteKey("TriggeredBonusIDs"); // ชื่อ Key จากโค้ด GameManager เก่า

        // สั่งบันทึกการลบทันที
        PlayerPrefs.Save();

        // ป้องกันการกดซ้ำถ้า Timeline กำลังเล่นอยู่
        if (sequenceTimeline != null && sequenceTimeline.state == PlayState.Playing) return;

        StartCoroutine(PlaySequence());
    }

    IEnumerator PlaySequence()
    {
        // 1. เล่น Animation ของปุ่ม (ถ้ามี) - เล่นทันที
        if (buttonAnimator != null)
        {
            buttonAnimator.SetTrigger(animationTrigger);
        }

        // 2. จัดการ Timeline
        if (sequenceTimeline != null)
        {
            sequenceTimeline.Play(); // เริ่มเล่น Timeline

            if (waitForTimelineToEnd)
            {
                // ถ้ารอให้จบ: ดึงความยาวของ Timeline มาใช้ในการรอ
                // (ต้องแปลง double เป็น float)
                yield return new WaitForSeconds((float)sequenceTimeline.duration);
            }
            else
            {
                // ถ้าไม่รอ Timeline: ใช้เวลา Delay ปกติ
                yield return new WaitForSeconds(delayBeforeLoad);
            }
        }
        else
        {
            // 3. ถ้าไม่มี Timeline เลย: ใช้เวลา Delay ปกติรอ Animation ปุ่ม
            yield return new WaitForSeconds(delayBeforeLoad);
        }

        // 4. สั่งให้ SceneTransition ทำงาน (เริ่ม Fade ดำและเปลี่ยนซีน)
        if (sceneTransition != null)
        {
            sceneTransition.Interact();
        }
        else
        {
            Debug.LogError("ลืมลาก SceneTransition มาใส่ในช่องครับ!");
        }
    }
}