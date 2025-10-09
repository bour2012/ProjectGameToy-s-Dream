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
    public CinemachineCamera mainCamera;
    [SerializeField] private int activePriority = 15;
    private Transform originalFollow;
    private Transform originalLookAt;
    [SerializeField]  private int originalCameraSize; // เก็บค่า Lens OriginalNearClipPlane เดิม
    //[Header("Camera Shake")]
    //public GameObject impulseSource;
    //public float shakeForce = 1f;



    [Header("Text Animation")]
    public bool useTypewriterEffect = false;
    public float typewriterSpeed = 0.05f;

    private DialogData[] currentSequence;
    private int currentIndex = 0;
    private bool isDialogActive = false;
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
            //impulseSource.SetActive(false);
        
        // ปิด dialogBox ตั้งแต่เริ่มต้น
        if (dialogBox != null)
        {
            dialogBox.SetActive(false);
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
        if (!isDialogActive) return;

        // คลิกซ้ายเพื่อข้าม
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            if (isTyping)
            {
                // ข้าม animation พิมพ์ข้อความ
                StopAllCoroutines();
                dialogText.text = currentSequence[currentIndex].dialogText;
                isTyping = false;
            }
            else
            {
                // ไปข้อความถัดไป
                NextDialog();
            }
        }

        // กด ESC ข้ามทั้งหมด
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            EndDialog();
        }
    }

    // ฟังก์ชันใหม่: รับ DialogData array
    public void StartDialogSequence(DialogData[] sequence, bool autoNext = false, float delay = 2f)
    {
        if (sequence == null || sequence.Length == 0)
        {
            Debug.LogWarning("Dialog sequence is empty!");
            return;
        }

        if (isDialogActive) return;

        currentSequence = sequence;
        currentIndex = 0;
        autoAdvance = autoNext;
        autoAdvanceDelay = delay;
        isDialogActive = true;

        // บันทึกค่ากล้องเดิม
        originalFollow = mainCamera.Follow;
        originalLookAt = mainCamera.LookAt;


        // ตั้งค่ากล้องเป็น 50
        mainCamera.Priority.Value = 50;

        ShowCurrentDialog();
    }

    private void ShowCurrentDialog()
    {
        if (currentIndex >= currentSequence.Length)
        {
            EndDialog();
            return;
        }

        DialogData currentData = currentSequence[currentIndex];

        // เปิด dialogBox เมื่อเริ่มแสดงข้อความ
        if (!dialogBox.activeSelf)
        {
            dialogBox.SetActive(true);
        }

        // แสดงข้อความ
        if (useTypewriterEffect)
        {
            StartCoroutine(TypewriterEffect(currentData.dialogText));
        }
        else
        {
            dialogText.text = currentData.dialogText;
        }

        // เล่นเสียง
        if (currentData.dialogVoice != null && audioSource != null)
        {
            audioSource.clip = currentData.dialogVoice;
            audioSource.Play();
        }

        // จัดการกล้อง
        Transform focusTarget = currentData.focusTarget;

        // ถ้าไม่มี focusTarget ให้หา Player
        if (focusTarget == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                focusTarget = player.transform;
            }
        }

        // ตั้งค่ากล้องให้ติดตามเป้าหมาย
        if (focusTarget != null)
        {
            mainCamera.Follow = focusTarget;
            mainCamera.LookAt = focusTarget;
            //// ✅ ให้ impulseSource หันไปทางหรืออยู่ตำแหน่งเดียวกับเป้าหมาย
            //impulseSource.transform.position = focusTarget.position;
            //impulseSource.transform.LookAt(focusTarget);
        }

        //// สั่นกล้อง
        //if (currentData.shakeCamera)
        //{
        //    StartCoroutine(CameraShake(shakeForce));
        //}

        // Auto advance
        if (autoAdvance)
        {
            StartCoroutine(AutoAdvanceCoroutine());
        }
    }

    private void NextDialog()
    {
        currentIndex++;
        ShowCurrentDialog();
    }

    public void EndDialog()
    {
        isDialogActive = false;
        dialogBox.SetActive(false);

        if (audioSource != null)
            audioSource.Stop();

        // คืนค่ากล้อง
        if (mainCamera != null)
        {
            mainCamera.Follow = originalFollow;
            mainCamera.LookAt = originalLookAt;
            mainCamera.Priority.Value = originalCameraSize; // คืนค่า Orthographic Size เดิม
        }

        currentSequence = null;
        currentIndex = 0;
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
    }

    //private IEnumerator CameraShake(float duration)
    //{
    //    impulseSource.SetActive(true);
    //    yield return new WaitForSeconds(duration);
    //    impulseSource.SetActive(false);
    //}

    private IEnumerator AutoAdvanceCoroutine()
    {
        yield return new WaitForSeconds(autoAdvanceDelay);

        if (isDialogActive && !isTyping)
        {
            NextDialog();
        }
    }

    // ฟังก์ชันเก่าเพื่อ backward compatibility (ถ้ามีโค้ดเดิมใช้อยู่)
    [System.Obsolete("Use StartDialogSequence instead")]
    public void StartDialog(string[] dialogLines, AudioClip voice, Transform focusTarget = null, bool shakeCamera = false)
    {
        // แปลงเป็น DialogData array
        DialogData[] sequence = new DialogData[dialogLines.Length];
        for (int i = 0; i < dialogLines.Length; i++)
        {
            sequence[i] = new DialogData
            {
                dialogText = dialogLines[i],
                dialogVoice = (i == 0) ? voice : null, // เล่นเสียงแค่ครั้งแรก
                focusTarget = focusTarget,
                shakeCamera = (i == 0) ? shakeCamera : false // สั่นแค่ครั้งแรก
            };
        }

        StartDialogSequence(sequence);
    }
}