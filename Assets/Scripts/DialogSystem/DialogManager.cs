using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;

public class DialogManager : MonoBehaviour
{
    public static DialogManager Instance;

    [Header("UI")]
    public GameObject dialogBox;
    public TextMeshProUGUI dialogText;

    [Header("Audio")]
    public AudioSource audioSource;

    [Header("Camera")]
    [Tooltip("ลาก VCam พิเศษสำหรับ Dialog (VCam_DialogFocus) มาใส่ที่นี่")]
    public CinemachineCamera dialogCamera;
    [Tooltip("Priority ที่จะใช้เมื่อกล้อง Dialog ทำงาน")]
    [SerializeField] private int dialogCameraPriority = 100;

    private DialogTrigger currentOriginator;

    [Header("Text Animation")]
    public bool useTypewriterEffect = false;
    public float typewriterSpeed = 0.05f;
    private Coroutine typewriterCoroutine;

    private DialogData[] currentSequence;
    private int currentIndex = 0;
    private bool isDialogActive = false;
    private bool isShowingHint = false;
    private bool isTyping = false;
    private bool autoAdvance = false;
    private float autoAdvanceDelay = 2f;

    void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        FindDialogComponents();
        if (dialogBox != null)
        {
            dialogBox.SetActive(false);
        }
        // ทำให้แน่ใจว่ากล้อง Dialog ไม่ทำงานตอนเริ่มเกม
        if (dialogCamera != null)
        {
            dialogCamera.Priority = 0;
        }
    }

    private void FindDialogComponents()
    {
        if (dialogBox == null || dialogText == null)
        {
            GameObject canvas = GameObject.Find("MainCanvas");
            if (canvas != null)
            {
                dialogBox = canvas.transform.Find("DialogBox").gameObject;
                dialogText = dialogBox.transform.Find("DialogText").GetComponent<TextMeshProUGUI>();
            }
            else
            {
                Debug.LogError("MainCanvas not found in the scene!");
            }
        }
    }

    private void Update()
    {
        if (isShowingHint) return;
        if (!isDialogActive) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (isTyping)
            {
                if (typewriterCoroutine != null)
                {
                    StopCoroutine(typewriterCoroutine);
                    typewriterCoroutine = null;
                }
                if (currentSequence != null && currentIndex < currentSequence.Length)
                {
                    dialogText.text = currentSequence[currentIndex].dialogText;
                }
                isTyping = false;
            }
            else
            {
                NextDialog();
            }
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            EndDialog();
        }
    }

    public void StartDialogSequence(DialogData[] sequence, DialogTrigger originator, bool autoNext = false, float delay = 2f)
    {
        if (sequence == null || sequence.Length == 0 || isDialogActive) return;

        currentOriginator = originator;
        currentSequence = sequence;
        currentIndex = 0;
        autoAdvance = autoNext;
        autoAdvanceDelay = delay;
        isDialogActive = true;

        ShowCurrentDialog();
    }

    private void ShowCurrentDialog()
    {
        if (currentSequence == null || currentIndex >= currentSequence.Length)
        {
            EndDialog();
            return;
        }

        DialogData currentData = currentSequence[currentIndex];
        if (currentData == null)
        {
            Debug.LogError($"Found a NULL DialogData at index {currentIndex}. Skipping.");
            NextDialog();
            return;
        }

        if (currentData.isHint)
        {
            StartCoroutine(ShowHintCoroutine(currentData));
        }
        else
        {
            ShowNormalDialog(currentData);
        }
    }

    private void ShowNormalDialog(DialogData currentData)
    {
        // --- ส่วนจัดการกล้อง ---
        if (dialogCamera != null)
        {
            if (currentData.focusTarget != null)
            {
                // ถ้ามี Target: ให้กล้อง Dialog ทำงาน
                dialogCamera.Follow = currentData.focusTarget;
                dialogCamera.LookAt = currentData.focusTarget;
                dialogCamera.Priority = dialogCameraPriority;
            }
            else
            {
                // ถ้าไม่มี Target: ปล่อยให้กล้อง Gameplay ปกติทำงาน
                dialogCamera.Priority = 0;
            }
        }

        // --- ส่วนจัดการ UI และ Text ---
        if (!dialogBox.activeSelf)
        {
            dialogBox.SetActive(true);
        }

        if (useTypewriterEffect)
        {
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
            }
            typewriterCoroutine = StartCoroutine(TypewriterEffect(currentData.dialogText));
        }
        else
        {
            dialogText.text = currentData.dialogText;
        }

        // --- ส่วนจัดการเสียงและอื่นๆ ---
        if (currentData.dialogVoice != null && audioSource != null)
        {
            audioSource.clip = currentData.dialogVoice;
            audioSource.Play();
        }

        if (autoAdvance)
        {
            StartCoroutine(AutoAdvanceCoroutine());
        }
    }

    private IEnumerator ShowHintCoroutine(DialogData hintData)
    {
        isShowingHint = true;

        if (hintData.hintUIElement == null)
        {
            Debug.LogError("Hint UI Element is not assigned in DialogData!");
            isShowingHint = false;
            NextDialog();
            yield break;
        }

        if (dialogBox.activeSelf) { dialogBox.SetActive(false); }

        TextMeshProUGUI hintText = hintData.hintUIElement.GetComponent<TextMeshProUGUI>();
        if (hintText != null) { hintText.text = hintData.dialogText; }

        hintData.hintUIElement.SetActive(true);

        if (hintData.displayDuration > 0)
        {
            yield return new WaitForSeconds(hintData.displayDuration);
            if (hintData.hintUIElement != null) { hintData.hintUIElement.SetActive(false); }
        }

        isShowingHint = false;
        NextDialog();
    }

    private void NextDialog()
    {
        currentIndex++;
        ShowCurrentDialog();
    }

    public void EndDialog()
    {
        if (!isDialogActive) return;

        isDialogActive = false;
        if (dialogBox != null) dialogBox.SetActive(false);
        if (audioSource != null) audioSource.Stop();

        // เมื่อจบ Dialog ต้องลด Priority ของกล้อง Dialog ลงเสมอ
        if (dialogCamera != null)
        {
            dialogCamera.Priority = 0;
        }

        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        if (currentOriginator != null)
        {
            currentOriginator.InvokeCompletionEvent();
        }

        currentSequence = null;
        currentIndex = 0;
        currentOriginator = null;
    }

    private IEnumerator TypewriterEffect(string text)
    {
        isTyping = true;
        dialogText.text = "";

        foreach (char c in text)
        {
            dialogText.text += c;
            yield return new WaitForSeconds(typewriterSpeed);
        }

        isTyping = false;
        typewriterCoroutine = null;
    }

    private IEnumerator AutoAdvanceCoroutine()
    {
        yield return new WaitForSeconds(autoAdvanceDelay);
        if (isDialogActive && !isTyping)
        {
            NextDialog();
        }
    }
}