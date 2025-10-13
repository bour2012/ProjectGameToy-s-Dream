using UnityEngine;
using UnityEngine.Events; // <-- สำคัญมาก!

public class PushableBox : MonoBehaviour
{
    [Header("Events")]
    [Tooltip("Event ที่จะทำงานเมื่อผู้เล่นเริ่มผลักกล่องนี้ครั้งแรก")]
    public UnityEvent onPlayerStartPush;

    private bool hasBeenPushed = false;

    // ฟังก์ชันนี้จะถูกเรียกโดย Player
    public void NotifyPlayerIsPushing()
    {
        // ถ้ายังไม่เคยถูกผลักมาก่อน
        if (!hasBeenPushed)
        {
            // "กดปุ่ม" Event
            onPlayerStartPush.Invoke();

            // ตั้งค่าว่าเคยถูกผลักไปแล้ว (เพื่อไม่ให้ทำงานซ้ำ)
            hasBeenPushed = true;
        }
    }
}