using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // **ต้องเพิ่มบรรทัดนี้ เพื่อให้โค้ดรู้จักคอมโพเนนต์ Button**

public class MainMenuController : MonoBehaviour
{
    [Header("UI References")]
    // เปลี่ยนจาก GameObject เป็น Button
    public Button continueButton;

    [Header("Scene Settings")]
    public string gameSceneName = "GameScene";

    void Start()
    {
        // ตรวจสอบว่ามีเซฟเกมค้างอยู่ไหม (HasSavedGame = 1 แปลว่ามี)
        if (PlayerPrefs.GetInt("HasSavedGame", 0) == 1)
        {
            // เปิดให้กดปุ่ม Continue ได้ (ปุ่มสีปกติ)
            continueButton.interactable = true;
        }
        else
        {
            // ปิดไม่ให้กดปุ่ม Continue ได้ (ปุ่มจะกลายเป็นสีเทา/จางอัตโนมัติ)
            continueButton.interactable = false;
        }
    }

    public void StartNewGame()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ClearAllSaveData();
        }
        else
        {
            float savedTime = PlayerPrefs.GetFloat("GameClearTime", 0f);
            PlayerPrefs.DeleteAll();
            if (savedTime > 0f) PlayerPrefs.SetFloat("GameClearTime", savedTime);
        }

        PlayerPrefs.SetInt("IsContinuing", 0);
        PlayerPrefs.SetInt("HasSavedGame", 1);
        PlayerPrefs.Save();

        SceneManager.LoadScene(gameSceneName);
    }

    public void ContinueGame()
    {
        PlayerPrefs.SetInt("IsContinuing", 1);
        PlayerPrefs.Save();

        string sceneToLoad = PlayerPrefs.GetString("SavedScene", gameSceneName);
        SceneManager.LoadScene(sceneToLoad);
    }
}