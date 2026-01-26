using System.Collections;
using TMPro;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class DialogManager : MonoBehaviour
{
    public static DialogManager Instance;

    [Header("UI Components")]
    public GameObject dialogBox;
    public TextMeshProUGUI dialogText;
    public CanvasGroup fadePanelCanvasGroup;

    [Header("Audio")]
    public AudioSource audioSource;

    [Header("Camera")]
    public CinemachineCamera dialogCamera;
    [SerializeField] private int dialogCameraPriority = 100;
    [SerializeField] private float cameraMovementThreshold = 0.01f;

    private CinemachinePositionComposer positionComposer;

    [Header("Text Animation")]
    public bool useTypewriterEffect = false;
    public float typewriterSpeed = 0.05f;

    private float defaultLensSize;
    private Coroutine zoomCoroutine;
    private float currentTargetLensSize;
    private float initialLensSize;
    private Coroutine typewriterCoroutine;
    private DialogTrigger currentOriginator;
    private DialogData[] currentSequence;
    private int currentIndex = 0;
    private bool isDialogActive = false;
    private bool isTyping = false;
    private bool isTransitioning = false;
    private bool isShowingHint = false;
    private bool autoAdvance = false;
    private float autoAdvanceDelay = 2f;
    private bool isWaitingForCamera = false;
    private Coroutine autoAdvanceCoroutine;
    void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (dialogCamera != null)
        {
            positionComposer = dialogCamera.GetComponent<CinemachinePositionComposer>();
            dialogCamera.Priority = 0;

            if (dialogCamera.Lens.Orthographic)
                defaultLensSize = dialogCamera.Lens.OrthographicSize;
            else
                defaultLensSize = dialogCamera.Lens.FieldOfView;

            initialLensSize = defaultLensSize;
            var lens = dialogCamera.Lens;
            currentTargetLensSize = lens.Orthographic ? lens.OrthographicSize : lens.FieldOfView;
        }

        if (dialogBox != null) dialogBox.SetActive(false);

        if (fadePanelCanvasGroup != null)
        {
            fadePanelCanvasGroup.alpha = 0;
            fadePanelCanvasGroup.blocksRaycasts = false;
        }
    }

    private void Update()
    {
        if (isTransitioning || isWaitingForCamera || isShowingHint) return;

        if (!isDialogActive) return;

        if (Input.GetMouseButtonDown(0))
        {
            StopAutoAdvance();
            if (isTyping)
            {
                StopTypewriter();
                // ถ้ากดข้าม ให้โชว์ข้อความทั้งหมดทันที (รวมถึง Tag สีด้วย)
                if (currentSequence != null && currentIndex < currentSequence.Length)
                    dialogText.text = currentSequence[currentIndex].dialogText;
            }
            else
            {
                NextDialog();
            }
        }
    }
    private void StopAutoAdvance()
    {
        if (autoAdvanceCoroutine != null)
        {
            StopCoroutine(autoAdvanceCoroutine);
            autoAdvanceCoroutine = null;
        }
    }
    public void StartDialogSequence(DialogData[] sequence, DialogTrigger originator, bool freezePlayer, bool autoNext = false, float delay = 2f)
    {
        if (sequence == null || sequence.Length == 0 || isDialogActive) return;

        currentOriginator = originator;
        currentSequence = sequence;
        currentIndex = 0;
        autoAdvance = autoNext;
        autoAdvanceDelay = delay;
        isDialogActive = true;

        if (dialogCamera != null)
        {
            var lens = dialogCamera.Lens;
            currentTargetLensSize = lens.Orthographic ? lens.OrthographicSize : lens.FieldOfView;
        }

        if (freezePlayer && GameManager.Instance != null)
        {
            GameManager.Instance.StartDialogState();
        }

        StartCoroutine(ProcessCurrentDialogStep());
    }

    private IEnumerator ProcessCurrentDialogStep()
    {
        if (currentSequence == null || currentIndex >= currentSequence.Length)
        {
            EndDialog();
            yield break;
        }

        DialogData data = currentSequence[currentIndex];

        if (data.changeZoom)
        {
            currentTargetLensSize = data.targetLensSize;
        }

        if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);
        zoomCoroutine = StartCoroutine(ZoomLensRoutine(currentTargetLensSize, data.zoomDuration));

        if (data.useFadeCut && fadePanelCanvasGroup != null)
        {
            isTransitioning = true;
            if (dialogBox != null) dialogBox.SetActive(false);

            yield return StartCoroutine(FadeRoutine(1f, data.fadeDuration));

            UpdateCameraTarget(data);

            float waitTime = data.fadeHoldDuration > 0f ? data.fadeHoldDuration : 0.5f;
            yield return new WaitForSecondsRealtime(waitTime);

            yield return StartCoroutine(FadeRoutine(0f, data.fadeDuration));
            yield return new WaitForSecondsRealtime(0.5f);

            isTransitioning = false;
        }
        else
        {
            UpdateCameraTarget(data);

            if (data.focusTarget != null && data.waitForCamera && dialogCamera != null)
            {
                if (dialogBox != null) dialogBox.SetActive(false);
                isWaitingForCamera = true;
                yield return StartCoroutine(WaitForCameraToStabilize());
                isWaitingForCamera = false;
            }
        }

        ShowDialogUI(data);
    }

    private IEnumerator WaitForCameraToStabilize()
    {
        float timeout = 5.0f;
        float timer = 0f;
        yield return null;

        Vector3 lastPos = dialogCamera.transform.position;

        while (timer < timeout)
        {
            timer += Time.deltaTime;
            float distanceMoved = Vector3.Distance(dialogCamera.transform.position, lastPos);

            if (distanceMoved < cameraMovementThreshold)
            {
                yield return null;
                if (Vector3.Distance(dialogCamera.transform.position, lastPos) < cameraMovementThreshold)
                {
                    break;
                }
            }

            lastPos = dialogCamera.transform.position;
            yield return null;
        }
    }

    private IEnumerator ZoomLensRoutine(float targetValue, float duration)
    {
        if (dialogCamera == null) yield break;
        var lensSettings = dialogCamera.Lens;
        float startValue = lensSettings.Orthographic ? lensSettings.OrthographicSize : lensSettings.FieldOfView;
        float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            t = Mathf.SmoothStep(0f, 1f, t);
            float currentValue = Mathf.Lerp(startValue, targetValue, t);

            if (lensSettings.Orthographic) lensSettings.OrthographicSize = currentValue;
            else lensSettings.FieldOfView = currentValue;

            dialogCamera.Lens = lensSettings;
            yield return null;
        }
        if (lensSettings.Orthographic) lensSettings.OrthographicSize = targetValue;
        else lensSettings.FieldOfView = targetValue;
        dialogCamera.Lens = lensSettings;
    }

    private void UpdateCameraTarget(DialogData data)
    {
        if (dialogCamera == null) return;

        if (data.focusTarget != null)
        {
            bool isNewTarget = dialogCamera.Follow != data.focusTarget;
            dialogCamera.Follow = data.focusTarget;

            if (positionComposer != null)
            {
                positionComposer.Damping.x = data.cameraSmoothness;
                positionComposer.Damping.y = data.cameraSmoothness;
                positionComposer.Damping.z = data.cameraSmoothness;
            }
            dialogCamera.Priority = dialogCameraPriority;
        }
        else
        {
            dialogCamera.Priority = 0;
        }
    }

    private void ShowDialogUI(DialogData data)
    {
        bool shouldHideUI = string.IsNullOrWhiteSpace(data.dialogText) || data.useFadeCut;
        if (shouldHideUI)
        {
            if (dialogBox != null) dialogBox.SetActive(false);
            data.onLineStart?.Invoke();
            StopAutoAdvance();
            autoAdvanceCoroutine = StartCoroutine(AutoAdvanceCoroutine(0.1f));
            return;
        }

        if (data.isHint)
        {
            if (dialogBox != null) dialogBox.SetActive(false);
            if (data.hintUIElement != null)
            {
                StartCoroutine(ShowHintRoutine(data));
            }
            else
            {
                if (dialogBox != null && !dialogBox.activeSelf) dialogBox.SetActive(true);
                data.onLineStart?.Invoke();
                dialogText.text = data.dialogText;

                StopAutoAdvance();
                autoAdvanceCoroutine = StartCoroutine(AutoAdvanceCoroutine(Mathf.Max(0.1f, data.displayDuration)));
            }
            return;
        }

        if (dialogBox != null && !dialogBox.activeSelf) dialogBox.SetActive(true);
        data.onLineStart?.Invoke();

        if (useTypewriterEffect)
        {
            StopTypewriter();
            typewriterCoroutine = StartCoroutine(TypewriterEffect(data));
        }
        else
        {
            StopAutoAdvance();
            autoAdvanceCoroutine = StartCoroutine(AutoAdvanceCoroutine());
        }
    }

    private IEnumerator ShowHintRoutine(DialogData data)
    {
        var hint = data.hintUIElement;
        if (hint == null)
        {
            NextDialog();
            yield break;
        }
        isShowingHint = true;
        var hintText = hint.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (hintText != null)
        {
            hintText.text = data.dialogText;
        }

        var cg = hint.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        hint.SetActive(true);
        data.onLineStart?.Invoke();

        float dur = Mathf.Max(0.01f, data.displayDuration);
        float t = 0f;
        while (t < dur && isDialogActive)
        {
            t += Time.deltaTime;
            yield return null;
        }

        hint.SetActive(false);
        isShowingHint = false;
        NextDialog();
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        float startAlpha = fadePanelCanvasGroup.alpha;
        float time = 0;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            fadePanelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            yield return null;
        }
        fadePanelCanvasGroup.alpha = targetAlpha;
    }

    private void NextDialog()
    {
        StopAutoAdvance();
        if (isTransitioning || isWaitingForCamera) return;
        currentIndex++;
        StartCoroutine(ProcessCurrentDialogStep());
    }

    public void EndDialog()
    {
        StopAutoAdvance();
        if (fadePanelCanvasGroup != null) fadePanelCanvasGroup.alpha = 0;
        isTransitioning = false;
        isWaitingForCamera = false;
        isDialogActive = false;
        if (dialogBox != null) dialogBox.SetActive(false);
        if (dialogCamera != null) dialogCamera.Priority = 0;
        if (GameManager.Instance != null) GameManager.Instance.EndDialogState();
        currentOriginator?.InvokeCompletionEvent();
    }

    private void StopTypewriter()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        isTyping = false;
    }

    // --------------------------------------------------------------------------------
    // ส่วนที่แก้ไขเพื่อแก้ปัญหา Tag สีโผล่
    // --------------------------------------------------------------------------------
    private IEnumerator TypewriterEffect(DialogData data)
    {
        isTyping = true;
        dialogText.text = ""; // ล้างข้อความเก่า

        if (string.IsNullOrEmpty(data.dialogText))
        {
            isTyping = false;
            if (autoAdvance) StartCoroutine(AutoAdvanceCoroutine());
            yield break;
        }

        // แปลงข้อความทั้งหมดเป็น String ปกติเพื่อเช็ค Tag ได้ง่าย
        string fullText = data.dialogText;

        for (int i = 0; i < fullText.Length; i++)
        {
            // 1. ตรวจสอบว่าตัวอักษรนี้เป็นจุดเริ่มต้นของ Tag หรือไม่ (<)
            if (fullText[i] == '<')
            {
                // มองหาจุดจบของ Tag (>)
                int closeIndex = fullText.IndexOf('>', i);

                // ถ้าเจอจุดจบ แสดงว่าเป็น Tag จริงๆ
                if (closeIndex != -1)
                {
                    // ดึงข้อความทั้งก้อน Tag ออกมา (เช่น <color=red>)
                    string tag = fullText.Substring(i, closeIndex - i + 1);

                    // เติม Tag ลงไปใน Text ทันที (เพื่อให้ Unity รู้ว่าต้องเปลี่ยนสี)
                    dialogText.text += tag;

                    // กระโดดข้าม Index ไปที่ตัวสุดท้ายของ Tag เลย (ไม่ต้องรอพิมพ์ทีละตัว)
                    i = closeIndex;

                    // ข้ามการเล่นเสียงและการรอเวลาใน Loop นี้
                    continue;
                }
            }

            // 2. ถ้าไม่ใช่ Tag ก็พิมพ์ตัวอักษรปกติ
            char c = fullText[i];
            dialogText.text += c;

            // เล่นเสียงเฉพาะตอนพิมพ์ตัวอักษรปกติ (ไม่เล่นตอนใส่ Tag)
            if (data.currentVoice != null)
            {
                PlayLetterSound(c, data);
            }
            else if (data.typingSound != null && c != ' ')
            {
                int freq = Mathf.Max(1, data.playSoundFrequency);
                // เช็คว่าตัวนี้ควรเล่นเสียงไหม
                if (dialogText.text.Length % freq == 0)
                {
                    PlayTypingSound(data);
                }
            }

            yield return new WaitForSeconds(typewriterSpeed);
        }

        isTyping = false;
        if (autoAdvance)
        {
            StopAutoAdvance(); // กันพลาด
            autoAdvanceCoroutine = StartCoroutine(AutoAdvanceCoroutine()); // เก็บตัวแปร
        }
    }
    // --------------------------------------------------------------------------------

    private void PlayTypingSound(DialogData data)
    {
        if (audioSource == null || data == null || data.typingSound == null) return;

        float originalPitch = audioSource.pitch;
        if (data.randomizePitch)
        {
            audioSource.pitch = Random.Range(data.pitchRange.x, data.pitchRange.y);
        }
        else
        {
            audioSource.pitch = 1f;
        }
        audioSource.PlayOneShot(data.typingSound);
        audioSource.pitch = originalPitch;
    }

    private void PlayLetterSound(char c, DialogData data)
    {
        AudioSource src = audioSource != null ? audioSource : GetComponent<AudioSource>();
        if (src == null || data == null) return;

        if (data.currentVoice != null && data.currentVoice.alphabetSounds != null && data.currentVoice.alphabetSounds.Length > 0)
        {
            int index = char.ToUpper(c) - 65;
            if (index < 0 || index >= 26) index = 26;

            if (index < data.currentVoice.alphabetSounds.Length)
            {
                AudioClip clip = data.currentVoice.alphabetSounds[index];
                if (clip != null)
                {
                    src.clip = clip;
                    src.pitch = 1f;
                    src.Play();
                }
            }
        }
        else if (data.typingSound != null)
        {
            if (data.randomizePitch)
                src.pitch = Random.Range(data.pitchRange.x, data.pitchRange.y);
            else
                src.pitch = 1f;

            src.PlayOneShot(data.typingSound);
        }
    }
    public void ShowAlert(string message, VoiceProfile sound = null, int freq = 2, bool rndPitch = true, Vector2? pRange = null)
    {

        DialogData tempData = new DialogData();

        tempData.dialogText = message;

        // กำหนดค่า Default เพื่อไม่ให้เกิดบั๊ก
        tempData.useFadeCut = false;
        tempData.changeZoom = false;
        tempData.isHint = false;
        tempData.waitForCamera = false;

        if (sound != null)
        {
            tempData.currentVoice = sound;
            tempData.playSoundFrequency = freq;
            tempData.randomizePitch = rndPitch;
            // ถ้ามีการส่ง pRange มาให้ใช้ค่าที่ส่งมา ถ้าไม่มีให้ใช้ค่ามาตรฐาน (0.9 - 1.1)
            tempData.pitchRange = pRange ?? new Vector2(0.9f, 1.1f);
        }
        // เริ่มระบบโดยส่งข้อมูลจำลองเข้าไป
        StartDialogSequence(new DialogData[] { tempData }, null, true);

        //DialogData tempData = new DialogData();


        //tempData.dialogText = message;

        //tempData.useFadeCut = false;
        //tempData.changeZoom = false;
        //tempData.isHint = false;

        //StartDialogSequence(new DialogData[] { tempData }, null, true);
    }


    private IEnumerator AutoAdvanceCoroutine(float delayTime = -1f)
    {
        float waitTime = (delayTime >= 0f) ? delayTime : autoAdvanceDelay;
        yield return new WaitForSeconds(waitTime);
        if (isDialogActive && !isTyping) NextDialog();
    }
}