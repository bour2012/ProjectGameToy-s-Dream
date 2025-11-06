using UnityEngine;

public class DeathDialogManager : MonoBehaviour
{
    private DialogTrigger currentDeathDialog = null;

    // ถูกเรียกโดยโซนอันตรายเพื่อ "ฝาก" บทพูดไว้
    public void SetDeathDialog(DialogTrigger dialog)
    {
        currentDeathDialog = dialog;
    }

    // ถูกเรียกเมื่อออกจากโซนเพื่อ "ล้าง" บทพูดที่ฝากไว้
    public void ClearDeathDialog()
    {
        currentDeathDialog = null;
    }

    // ถูกเรียกโดย PlayerDeathSystem เพื่อดึงบทพูดไปส่งให้ GameManager
    public DialogTrigger GetCurrentDeathDialog()
    {
        return currentDeathDialog;
    }
}