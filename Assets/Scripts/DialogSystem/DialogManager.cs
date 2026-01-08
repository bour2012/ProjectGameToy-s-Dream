using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public class DialogManager : MonoBehaviour
{
    public static DialogManager Instance;

    // ... (ตัวแปร Header อื่นๆ เหมือนเดิม) ...
    [Header("UI Components")]
    public GameObject dialogBox;
    public TextMeshProUGUI dialogText;
    public CanvasGroup fadePanelCanvasGroup;

    [Header("Audio")]
    public AudioSource audioSource;

    [Header("Camera")]
    public CinemachineCamera dialogCamera;
    [SerializeField] private int dialogCameraPriority = 100;

    // [เพิ่มใหม่] ค่าความคลาดเคลื่อนที่ยอมรับได้ว่า "กล้องถึงแล้ว" (หน่วย Unity Unit ต่อเฟรม)
    [SerializeField] private float cameraMovementThreshold = 0.01f;

    private CinemachinePositionComposer positionComposer;

    [Header("Text Animation")]
    public bool useTypewriterEffect = false;
    public float typewriterSpeed = 0.05f;

    private float defaultLensSize;
    private Coroutine zoomCoroutine;
    // เก็บค่า Zoom ล่าสุดที่ควรจะเป็น (ไม่รีเซ็ตเมื่อประโยคถัดไปไม่มี changeZoom)
    private float currentTargetLensSize;
    // ค่าเริ่มต้นเผื่ออยาก Reset ตอนจบ Dialog ทั้งหมด
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

    // [เพิ่มใหม่] ตัวแปรเช็คว่ากำลังรอกล้องอยู่ไหม
    private bool isWaitingForCamera = false;

    void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // ... (Start เดิม) ...
        if (dialogCamera != null)
        {
            positionComposer = dialogCamera.GetComponent<CinemachinePositionComposer>();
            dialogCamera.Priority = 0;

            if (dialogCamera.Lens.Orthographic)
                defaultLensSize = dialogCamera.Lens.OrthographicSize;
            else
                defaultLensSize = dialogCamera.Lens.FieldOfView;

            // เก็บค่าเริ่มต้นและตั้งค่า target ให้เริ่มจากค่าปัจจุบันของเลนส์
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
        // [แก้ไข] เพิ่มเงื่อนไข isWaitingForCamera ห้ามกดข้ามถ้ารอกล้องอยู่
        if (isTransitioning || isWaitingForCamera) return;
        if (isTransitioning || isWaitingForCamera || isShowingHint) return;

        if (!isDialogActive) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (isTyping)
            {
                StopTypewriter();
                if (currentSequence != null && currentIndex < currentSequence.Length)
                    dialogText.text = currentSequence[currentIndex].dialogText;
            }
            else
            {
                NextDialog();
            }
        }
    }

    // ... (StartDialogSequence เดิม) ...
    public void StartDialogSequence(DialogData[] sequence, DialogTrigger originator, bool freezePlayer, bool autoNext = false, float delay = 2f)
    {
        if (sequence == null || sequence.Length == 0 || isDialogActive) return;

        currentOriginator = originator;
        currentSequence = sequence;
        currentIndex = 0;
        autoAdvance = autoNext;
        autoAdvanceDelay = delay;
        isDialogActive = true;

        // ตั้ง current target ให้เริ่มจากค่ากล้องตอนเริ่ม dialog
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

        // ถ้า Data บอกให้เปลี่ยนค่า Zoom -> อัปเดต target ค่า Zoom ล่าสุด
        if (data.changeZoom)
        {
            currentTargetLensSize = data.targetLensSize;
        }

        // เรียก Coroutine เพื่อเลื่อนไปยัง currentTargetLensSize (ถ้ามี)
        if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);
        zoomCoroutine = StartCoroutine(ZoomLensRoutine(currentTargetLensSize, data.zoomDuration));

        if (data.useFadeCut && fadePanelCanvasGroup != null)
        {
            isTransitioning = true;
            if (dialogBox != null) dialogBox.SetActive(false);

            yield return StartCoroutine(FadeRoutine(1f, data.fadeDuration));

            UpdateCameraTarget(data);

            // ถ้า Fade ดำ เราไม่จำเป็นต้องรอกล้องวิ่ง เพราะคนมองไม่เห็นอยู่แล้ว
            // แต่ถ้าระบบ Cinemachine ตัด Hard Cut ตอน Fade ก็ไม่มีปัญหา

            float waitTime = data.fadeHoldDuration > 0f ? data.fadeHoldDuration : 0.5f;
            yield return new WaitForSecondsRealtime(waitTime);

            yield return StartCoroutine(FadeRoutine(0f, data.fadeDuration));

            // รอเพิ่มหลังจอใส
            yield return new WaitForSecondsRealtime(0.5f);

            isTransitioning = false;
        }
        else
        {
            // ถ้าไม่ Fade ให้ย้ายกล้อง และเช็คว่าจะรอไหม
            UpdateCameraTarget(data);

            //// [เพิ่มใหม่] ส่วนเช็คการรอกล้อง
            if (data.focusTarget != null && data.waitForCamera && dialogCamera != null)
            {
                // ซ่อนกล่องข้อความก่อน
                if (dialogBox != null) dialogBox.SetActive(false);

                // ล็อกอินพุต
                isWaitingForCamera = true;

                // รอจนกว่ากล้องจะนิ่ง
                yield return StartCoroutine(WaitForCameraToStabilize());

                // ปลดล็อก
                isWaitingForCamera = false;
            }
        }

        ShowDialogUI(data);
    }

    // [เพิ่มใหม่] ฟังก์ชันรอกล้องหยุดขยับ
    private IEnumerator WaitForCameraToStabilize()
    {
        float timeout = 5.0f; // กันเหนียวเผื่อกล้องติดบัค ไม่ยอมหยุด จะได้ไม่ค้างตลอดกาล
        float timer = 0f;

        // รอเฟรมแรกให้ Cinemachine เริ่มคำนวณก่อน
        yield return null;

        Vector3 lastPos = dialogCamera.transform.position;

        // เงื่อนไข: ถ้ากล้องขยับน้อยกว่า Threshold ติดต่อกัน หรือ หมดเวลา Timeout
        while (timer < timeout)
        {
            timer += Time.deltaTime;

            float distanceMoved = Vector3.Distance(dialogCamera.transform.position, lastPos);

            // ถ้าขยับน้อยมาก (ถือว่าถึงแล้ว/หยุดแล้ว)
            if (distanceMoved < cameraMovementThreshold)
            {
                // เช็คซ้ำอีกนิดเผื่อเป็นแค่จังหวะสะดุด (Optional)
                yield return null;
                if (Vector3.Distance(dialogCamera.transform.position, lastPos) < cameraMovementThreshold)
                {
                  
                    break; // ออกจาก Loop รอ
                }
            }

            lastPos = dialogCamera.transform.position;
            yield return null;
        }

    }

    // ... (ZoomLensRoutine เดิม) ...
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

    // ... (UpdateCameraTarget เดิม) ...
    private void UpdateCameraTarget(DialogData data)
    {
        if (dialogCamera == null) return;

        if (data.focusTarget != null)
        {
            // เช็คว่า Target เปลี่ยนไหม หรือเป็นตัวเดิม (เพื่อ Reset Damping ถ้าจำเป็น)
            bool isNewTarget = dialogCamera.Follow != data.focusTarget;

            dialogCamera.Follow = data.focusTarget;
            // ถ้าเป็น 2D ปกติไม่ต้อง LookAt แต่ถ้า 3D อาจจะต้องใช้
            // dialogCamera.LookAt = data.focusTarget; 

            if (positionComposer != null)
            {
                // ถ้าเปลี่ยนเป้าหมาย และต้องการให้กล้องเริ่มวิ่งใหม่
                if (isNewTarget)
                {
                    // บางกรณีอาจต้องสั่ง Reset การคำนวณของ Cinemachine
                    // dialogCamera.PreviousStateIsValid = false; 
                }

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

    // ... (ShowDialogUI, FadeRoutine, NextDialog, EndDialog, Typewriter เหมือนเดิม) ...
    // ... Copy ฟังก์ชันที่เหลือจากโค้ดเก่ามาวางต่อได้เลยครับ ไม่มีการเปลี่ยนแปลง ...

    private void ShowDialogUI(DialogData data)
    {
        // เหมือนเดิม 100%
        bool shouldHideUI = string.IsNullOrWhiteSpace(data.dialogText) || data.useFadeCut;
        if (shouldHideUI)
        {
            if (dialogBox != null) dialogBox.SetActive(false);
            data.onLineStart?.Invoke();
            StartCoroutine(AutoAdvanceCoroutine(0.1f));
            return;
        }

        if (data.isHint)
        {
            if (dialogBox != null) dialogBox.SetActive(false); // ซ่อน dialog ปกติ
            if (data.hintUIElement != null)
            {
                StartCoroutine(ShowHintRoutine(data));
            }
            else
            {
                // fallback: แสดงข้อความสั้นๆ ใน dialogBox ถ้าไม่มี hintUIElement
                if (dialogBox != null && !dialogBox.activeSelf) dialogBox.SetActive(true);
                data.onLineStart?.Invoke();
                dialogText.text = data.dialogText;
                StartCoroutine(AutoAdvanceCoroutine(Mathf.Max(0.1f, data.displayDuration)));
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
            dialogText.text = data.dialogText;
            if (autoAdvance) StartCoroutine(AutoAdvanceCoroutine());
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
        // หา TextMeshPro ใน hint (รวม inactive ลูกด้วย) แล้วเซ็ตข้อความ
        var hintText = hint.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (hintText != null)
        {
            hintText.text = data.dialogText;
        }

        // ถ้ามี CanvasGroup ให้แน่ใจว่าแสดงผลได้
        var cg = hint.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        hint.SetActive(true);

        data.onLineStart?.Invoke();

        // Note: per-letter voice playback handled by TypewriterEffect via VoiceProfile.

        float dur = Mathf.Max(0.01f, data.displayDuration);
        float t = 0f;
        while (t < dur && isDialogActive)
        {
            t += Time.deltaTime;
            yield return null;
        }

        // ปิด hint แล้วไปประโยคถัดไป
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
        // [แก้ไข] เพิ่มเงื่อนไข isWaitingForCamera
        if (isTransitioning || isWaitingForCamera) return;

        currentIndex++;
        StartCoroutine(ProcessCurrentDialogStep());
    }

    // ... ฟังก์ชันที่เหลือ (EndDialog, StopTypewriter, TypewriterEffect, AutoAdvanceCoroutine) เหมือนเดิม
    public void EndDialog()
    {
        if (fadePanelCanvasGroup != null) fadePanelCanvasGroup.alpha = 0;
        isTransitioning = false;
        isWaitingForCamera = false; // [เพิ่ม] รีเซ็ตค่า

        isDialogActive = false;
        if (dialogBox != null) dialogBox.SetActive(false);

        if (dialogCamera != null)
        {
            // ปรับ Priority ลง แต่ไม่รีเซ็ตค่าเลนส์ — เก็บค่า Zoom ล่าสุดไว้
            dialogCamera.Priority = 0;
        }
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

    private IEnumerator TypewriterEffect(DialogData data)
    {
        isTyping = true;
        dialogText.text = "";

        if (string.IsNullOrEmpty(data.dialogText))
        {
            isTyping = false;
            if (autoAdvance) StartCoroutine(AutoAdvanceCoroutine());
            yield break;
        }

        char[] characters = data.dialogText.ToCharArray();
        for (int i = 0; i < characters.Length; i++)
        {
            char c = characters[i];
            dialogText.text += c;

            // If a VoiceProfile is assigned, play the mapped letter sound (cuts previous clip)
            if (data.currentVoice != null)
            {
                PlayLetterSound(c, data);
            }
            else if (data.typingSound != null && c != ' ')
            {
                int freq = Mathf.Max(1, data.playSoundFrequency);
                if (i % freq == 0)
                {
                    PlayTypingSound(data);
                }
            }

            yield return new WaitForSeconds(typewriterSpeed);
        }

        isTyping = false;
        if (autoAdvance) StartCoroutine(AutoAdvanceCoroutine());
    }

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

    // Play a letter-mapped sound from the VoiceProfile (cuts previous clip immediately)
    private void PlayLetterSound(char c, DialogData data)
    {
        AudioSource src = audioSource != null ? audioSource : GetComponent<AudioSource>();
        if (src == null || data == null) return;

        // ---------------------------------------------------------
        // Case 1: VoiceProfile present -> play A-Z mapped sound (cuts previous clip)
        // ---------------------------------------------------------
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
        // ---------------------------------------------------------
        // Case 2: No VoiceProfile -> fallback to typingSound (one-shot, respects pitch randomization)
        // ---------------------------------------------------------
        else if (data.typingSound != null)
        {
            if (data.randomizePitch)
                src.pitch = Random.Range(data.pitchRange.x, data.pitchRange.y);
            else
                src.pitch = 1f;

            src.PlayOneShot(data.typingSound);
        }
    }

    private IEnumerator AutoAdvanceCoroutine(float delayTime = -1f)
    {
        float waitTime = (delayTime >= 0f) ? delayTime : autoAdvanceDelay;
        yield return new WaitForSeconds(waitTime);
        if (isDialogActive && !isTyping) NextDialog();
    }
}