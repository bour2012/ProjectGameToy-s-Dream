using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
public class SceneTransition : MonoBehaviour
{
    [Header("Transition Settings")]
    public Animator transition;
    public string nextSceneName;
    private bool isTransitioning = false; // กันไม่ให้ซ้ำหลายรอบ

    [Header("Mode Settings")]
    public bool requireButtonPress = false;  // ถ้า true ต้องกดปุ่มถึงจะย้าย scene
    public KeyCode interactKey = KeyCode.E;  // ปุ่มที่ใช้กด (เช่น E)

    [Header("UI Settings")]
    public GameObject interactUIPanel;    // UI panel ที่บอกให้กดปุ่ม
    public TextMeshProUGUI interactText;  // ข้อความใน UI (ใช้ TMP)
    [Header("UI Animation")]
    [Tooltip("ความเร็วในการลอยขึ้น-ลง")]
    public float animationSpeed = 2f;
    [Tooltip("ระยะทางในการลอยขึ้น-ลง")]
    public float animationAmplitude = 10f;

    private bool isPlayerInRange = false;
    private Coroutine uiAnimationCoroutine;
    private Vector2 initialUIPosition;



    // Update is called once per frame
    private void Awake()
    {

        if (transition != null && transition.gameObject != null)
        {
            transition.gameObject.SetActive(true);
        }
        if (interactUIPanel != null)
        {
            // บันทึกตำแหน่งเริ่มต้นของ UI
            initialUIPosition = interactUIPanel.GetComponent<RectTransform>().anchoredPosition;
            interactUIPanel.SetActive(false);
        }
    }

    private void Update()
    {
        // ย้าย Logic การกดปุ่มมาไว้ที่นี่
        if (isPlayerInRange && requireButtonPress && !isTransitioning)
        {
            if (Input.GetKeyDown(interactKey))
            {
                isTransitioning = true;
                if (interactUIPanel != null) interactUIPanel.SetActive(false); // ซ่อน UI ทันทีที่กด
                StartCoroutine(LoadScene());
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true; // ตั้งค่าว่าผู้เล่นอยู่ในระยะ

            if (requireButtonPress)
            {
                // แสดง UI และเริ่ม Animation
                if (interactUIPanel != null)
                {
                    interactUIPanel.SetActive(true);
                    uiAnimationCoroutine = StartCoroutine(AnimateUIPanel());
                }
            }
            else if (!isTransitioning)
            {
                // โหมดเดินผ่านแล้วเปลี่ยนซีน (ไม่ต้องกดปุ่ม)
                isTransitioning = true;
                StartCoroutine(LoadScene());
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false; // ตั้งค่าว่าผู้เล่นออกจากระยะ

            // ซ่อน UI และหยุด Animation
            if (requireButtonPress && interactUIPanel != null)
            {
                if (uiAnimationCoroutine != null)
                {
                    StopCoroutine(uiAnimationCoroutine);
                }
                interactUIPanel.SetActive(false);
                // รีเซ็ตตำแหน่ง UI กลับไปที่เดิม
                interactUIPanel.GetComponent<RectTransform>().anchoredPosition = initialUIPosition;
            }
        }
    }

    IEnumerator LoadScene()
    {
        transition.SetTrigger("End");
        yield return new WaitForSeconds(1.5f);
        SceneManager.LoadScene(nextSceneName);
    }

    private IEnumerator AnimateUIPanel()
    {
        RectTransform uiRect = interactUIPanel.GetComponent<RectTransform>();

        while (true)
        {
            // ใช้ฟังก์ชัน Sin เพื่อสร้างการเคลื่อนที่แบบคลื่น (ขึ้น-ลง)
            float yOffset = Mathf.Sin(Time.time * animationSpeed) * animationAmplitude;
            uiRect.anchoredPosition = new Vector2(initialUIPosition.x, initialUIPosition.y + yOffset);

            yield return null; // รอเฟรมถัดไป
        }
    }
}
