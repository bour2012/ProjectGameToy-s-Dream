using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
public class SceneTransition : MonoBehaviour, IInteractable
{
    [Header("Transition Settings")]
    public Animator transition;
    public string nextSceneName;
    private bool isTransitioning = false;

    [Header("Mode Settings")]
    [Tooltip("ถ้าติ๊ก, ผู้เล่นต้องกดปุ่มเพื่อเปลี่ยนซีน. ถ้าไม่ติ๊ก, ผู้เล่นจะเปลี่ยนซีนทันทีที่เดินเข้ามา")]
    public bool requireButtonPress = true;

    [Header("UI Settings (Optional)")]
    [Tooltip("ข้อความที่จะแสดงบน UI Prompt (จะทำงานเมื่อ requireButtonPress เป็น true)")]
    [TextArea] public string interactionPromptText = "[E] to Enter";

    // เราไม่ต้องการตัวแปร UI และการตรวจจับผู้เล่นในนี้อีกต่อไป

    void Awake()
    {
        // ทำให้แน่ใจว่า Animator พร้อมใช้งาน
        if (transition != null && transition.gameObject != null)
        {
            transition.gameObject.SetActive(true);
        }
    }

    #region IInteractable Implementation

    /// <summary>
    /// PlayerInteractor จะเรียกฟังก์ชันนี้เพื่อขอข้อความไปแสดงบน UI
    /// </summary>
    public string GetInteractText()
    {
        // จะแสดงข้อความก็ต่อเมื่อตั้งค่าให้ต้องกดปุ่มเท่านั้น
        // ถ้า requireButtonPress เป็น false, PlayerInteractor จะไม่แสดง UI
        return requireButtonPress ? interactionPromptText : "";
    }

    /// <summary>
    /// PlayerInteractor จะเรียกฟังก์ชันนี้เมื่อผู้เล่นกดปุ่ม E
    /// </summary>
    public void Interact()
    {
        // จะทำงานก็ต่อเมื่อตั้งค่าให้ต้องกดปุ่ม และยังไม่ได้กำลังเปลี่ยนซีน
        if (requireButtonPress && !isTransitioning)
        {
            isTransitioning = true;
            StartCoroutine(LoadScene());
        }
    }

    #endregion

    /// <summary>
    /// ฟังก์ชันนี้สำหรับโหมด "เดินผ่านแล้วเปลี่ยนซีน" โดยอัตโนมัติ
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // ถ้า "ไม่ต้อง" กดปุ่ม, เป็น Player, และยังไม่ได้กำลังเปลี่ยนซีน -> เริ่มเปลี่ยนซีนทันที
        if (!requireButtonPress && other.CompareTag("Player") && !isTransitioning)
        {
            isTransitioning = true;
            StartCoroutine(LoadScene());
        }
    }

    // Coroutine หลักสำหรับจัดการ Animation และการโหลดซีน (เหมือนเดิม)
    IEnumerator LoadScene()
    {

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ClearCheckpointSaveData();
        }

        transition.SetTrigger("End");
        yield return new WaitForSeconds(1.5f);
        SceneManager.LoadScene(nextSceneName);
    }

    public void QuitGame()
    {
        StartCoroutine(ExitGame());
    }

    IEnumerator ExitGame()
    {

        transition.SetTrigger("End");
        yield return new WaitForSeconds(1.5f);
        Application.Quit();

    }

   
  
}
