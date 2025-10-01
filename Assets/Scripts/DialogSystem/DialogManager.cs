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
    private Transform originalFollow;
    private Transform originalLookAt;

    private Queue<string> lines;
    private bool isDialogActive = false;
    private string currentLine = ""; // เก็บข้อความปัจจุบัน


    void Awake()
    {
        Instance = this;
    }
    //{
    //    if (Instance == null)
    //    {
    //        Instance = this;
    //        DontDestroyOnLoad(gameObject); // ป้องกันการทำลายเมื่อโหลดฉากใหม่

    //        // ค้นหา DialogBox และ DialogText ใหม่
    //        FindDialogComponents();
    //    }
    //    else if (Instance != this)
    //    {
    //        Destroy(gameObject); // ทำลาย Instance ที่ซ้ำซ้อน
    //    }
    //}
    private void Start()
    {
        FindDialogComponents();
    }


    private void FindDialogComponents()
    {
        if (dialogBox == null || dialogText == null)
        {
            GameObject canvas = GameObject.Find("MainCanvas"); // ค้นหา Canvas ที่เก็บ DialogBox
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

    public void StartDialog(string[] dialogLines, AudioClip voice, Transform focusTarget = null, bool shakeCamera = false)
    {
        if (isDialogActive) return;

        isDialogActive = true;
        dialogBox.SetActive(true);
        lines = new Queue<string>(dialogLines);

        // เล่นเสียง
        if (voice != null)
        {
            audioSource.clip = voice;
            audioSource.Play();
        }

        // บันทึกค่า Follow และ LookAt เดิมของกล้อง
        originalFollow = mainCamera.Follow;
        originalLookAt = mainCamera.LookAt;

        // ค้นหา Player ใหม่ทุกครั้ง
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
        }

        // Camera Shake
        if (shakeCamera) StartCoroutine(CameraShake(0.5f, 0.2f));

        ShowNextLine();
    }

    public void ShowNextLine()
    {
        if (lines.Count == 0)
        {
            EndDialog();
            return;
        }

        string line = lines.Dequeue();
        dialogText.text = line;
    }

    public void EndDialog()
    {
        dialogBox.SetActive(false);
        audioSource.Stop();

        // คืนค่ากล้อง
        mainCamera.Follow = originalFollow;
        mainCamera.LookAt = originalLookAt;

        isDialogActive = false;
        lines.Clear(); // ล้างบทสนทนา
        currentLine = ""; // รีเซ็ตข้อความปัจจุบัน
    }
    private void Update()
    {
        if (!isDialogActive) return;

        if (Input.GetKeyDown(KeyCode.Space)) // กดข้ามบรรทัด
            ShowNextLine();

        if (Input.GetKeyDown(KeyCode.Escape)) // กดข้ามทั้งหมด
            EndDialog();
    }

    private IEnumerator CameraShake(float duration, float magnitude)
    {
        Vector3 originalPos = mainCamera.transform.localPosition;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            mainCamera.transform.localPosition = new Vector3(x, y, originalPos.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.localPosition = originalPos;
    }
}
