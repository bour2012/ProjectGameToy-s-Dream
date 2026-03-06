using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{

    // ฟังก์ชันนี้เอาไว้ไปผูกกับปุ่ม New Game ในหน้า UI
    public void StartNewGame()
    {
     
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ClearAllSaveData();
        }
        else
        {
            // (กันเหนียว) เผื่อเปิดเกมมาหน้าเมนูแล้ว GameManager ยังไม่โหลด
            float savedTime = PlayerPrefs.GetFloat("GameClearTime", 0f);
            PlayerPrefs.DeleteAll();
            if (savedTime > 0f) PlayerPrefs.SetFloat("GameClearTime", savedTime);
            PlayerPrefs.Save();
            Debug.Log("ล้างเซฟจากหน้าเมนูโดยตรง (GameManager ยังไม่ตื่น)");
        }

 
    }
}