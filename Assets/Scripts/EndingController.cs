using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement; // NEW: ต้องเพิ่มบรรทัดนี้เพื่อใช้คำสั่งเปลี่ยน Scene

public class EndingController : MonoBehaviour
{
    [Header("UI References")]
    public CanvasGroup blackScreenCanvasGroup;
    public CanvasGroup textCanvasGroup;

    [Header("Settings")]
    public float fadeDuration = 2.0f;

    [Header("Scene Transition")] // NEW: ตั้งค่าเกี่ยวกับการเปลี่ยนฉาก
    public string sceneToLoad = "MainMenu"; // NEW: ใส่ชื่อ Scene ที่ต้องการไป (เช่น "MainMenu")
    public float waitBeforeChange = 3.0f;   // NEW: จะให้โชว์หน้า To be continued นานแค่ไหนก่อนตัดฉาก

    private void Start()
    {
        blackScreenCanvasGroup.alpha = 0;
        textCanvasGroup.alpha = 0;
        blackScreenCanvasGroup.blocksRaycasts = false;
    }

    public void StartEndingSequence()
    {
        StartCoroutine(ProcessEnding());
    }

    IEnumerator ProcessEnding()
    {
        // 1. Fade จอดำ
        blackScreenCanvasGroup.blocksRaycasts = true;
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            blackScreenCanvasGroup.alpha = timer / fadeDuration;
            yield return null;
        }
        blackScreenCanvasGroup.alpha = 1;

        yield return new WaitForSeconds(0.5f);

        // 2. Fade ข้อความ
        timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            textCanvasGroup.alpha = timer / fadeDuration;
            yield return null;
        }
        textCanvasGroup.alpha = 1;

        // NEW 3: รอให้คนอ่านข้อความแป๊บนึง แล้วค่อยเปลี่ยน Scene
        Debug.Log("Waiting to load next scene...");
        yield return new WaitForSeconds(waitBeforeChange);

        // NEW 4: โหลด Scene ใหม่
        SceneManager.LoadScene(sceneToLoad);
    }
}