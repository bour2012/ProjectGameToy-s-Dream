using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class SceneTransition : MonoBehaviour, IInteractable
{
    [Header("Transition Settings")]
    public Animator transition;
    public string nextSceneName;
    private bool isTransitioning = false;

    [Header("Mode Settings")]
    [Tooltip("ถ้าติ๊ก, ผู้เล่นต้องกดปุ่มเพื่อเปลี่ยนซีน")]
    public bool requireButtonPress = true;

    public bool modeChengTime = false;
    public float changTime;

    // ▼▼▼ เพิ่มใหม่: ส่วนตั้งค่าเงื่อนไขการล็อค ▼▼▼
    [Header("Lock Condition (Optional)")]
    [Tooltip("ลาก Lever ที่ต้องการให้ Active ก่อนถึงจะผ่านได้มาใส่ (ถ้าปล่อยว่างคือเข้าได้เลย)")]
    public Lever conditionLever;

    [FormerlySerializedAs("dialogVoice")]
    public VoiceProfile currentVoice; // ลากไฟล์ Voice Profile ที่สร้างในข้อ 1 มาใส่ตรงนี้
    [Range(1, 5)] public int soundFreq = 2;
    public bool randomizePitch = true;
    public Vector2 pitchRange = new Vector2(0.9f, 1.1f);
    [Tooltip("ข้อความที่จะแสดงใน Dialog เมื่อประตูยังล็อคอยู่")]
    [TextArea] public string lockedMessage = "The door is locked. Looks like I need to activate a lever somewhere.";
    // ▲▲▲ สิ้นสุดส่วนเพิ่มใหม่ ▲▲▲


    [Header("UI Settings (Optional)")]
    [TextArea] public string interactionPromptText = "[E] to Enter";


    private bool hasUnlocked = false;
    void Awake()
    {
        if (transition != null && transition.gameObject != null)
        {
            transition.gameObject.SetActive(true);
        }
    }

    private void Update()
    {
        if (modeChengTime)
        {
            changTime -= Time.deltaTime;
            if (changTime <= 0)
                SceneManager.LoadScene(nextSceneName);
        }

        if (!hasUnlocked && conditionLever != null && conditionLever.isActive)
        {
            hasUnlocked = true;
          
        }
    }

    #region IInteractable Implementation

    public string GetInteractText()
    {
        // ถ้ามี Lever ล็อคอยู่ อาจจะเปลี่ยนข้อความก็ได้ (Option) แต่ใช้ข้อความเดิมก็ได้
        return requireButtonPress ? interactionPromptText : "";
    }

    public void Interact()
    {
     
        if (requireButtonPress && !isTransitioning)
        {
            bool isLocked = false;
            if (conditionLever != null)
            {
                // ถ้ายังไม่เคยปลดล็อค และ Lever ปัจจุบันก็ยังไม่ Active -> ถือว่าล็อค
                if (!hasUnlocked && !conditionLever.isActive)
                {
                    isLocked = true;
                }
            }

            if (isLocked)
            {
                ShowLockedDialog();
                return;
            }

     
            isTransitioning = true;
            StartCoroutine(LoadScene());
        }
      
    }

    #endregion

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!requireButtonPress && other.CompareTag("Player") && !isTransitioning)
        {
            // สำหรับโหมดเดินชน ถ้าติดล็อค จะไม่ให้ผ่าน และไม่โหลดซีน
            if (conditionLever != null && !conditionLever.isActive)
            {
                // (Optional) อาจจะให้โชว์ Dialog ด้วยก็ได้ถ้าต้องการ
                // ShowLockedDialog(); 
                return;
            }

            isTransitioning = true;
            StartCoroutine(LoadScene());
        }
    }


    private void ShowLockedDialog()
    {
        if (DialogManager.Instance != null)
        {
            // ส่งทั้งข้อความ และ ค่าเสียงที่ตั้งไว้ใน Inspector ไปให้ DialogManager
            DialogManager.Instance.ShowAlert(
                lockedMessage,
                currentVoice,
                soundFreq,
                randomizePitch,
                pitchRange
            );
        }
    }


    IEnumerator LoadScene()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ClearCheckpointSaveData();
            GameManager.Instance.ClearPlayedDialogsHistory();
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