using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class PlayerInteractor : MonoBehaviour
{
    [Header("Interaction Settings")]
    public KeyCode interactKey = KeyCode.E;

    [Header("UI References")]
    public GameObject interactPromptUI;
    public TextMeshProUGUI promptText;

    [Header("UI Positioning")]
    public Vector3 promptOffset = new Vector3(0, 1.5f, 0);

    [Header("UI Animation")]
    [Tooltip("ความเร็วในการลอยขึ้น-ลง")]
    public float animationSpeed = 2f;
    [Tooltip("ระยะทางในการลอยขึ้น-ลง (หน่วยเป็น World Unit)")]
    public float animationAmplitude = 0.1f;
    [Tooltip("เวลาที่ใช้ในการขยาย UI (วินาที)")]
    public float appearTime = 0.3f;
    [Tooltip("เวลาที่ใช้ในการหด UI (วินาที)")]
    public float disappearTime = 0.2f;

    private IInteractable currentInteractable;
    private Collider2D currentTargetCollider;
    private Coroutine animationCoroutine;
    private Vector3 originalPromptScale;

    void Awake()
    {
        if (interactPromptUI != null)
        {
            originalPromptScale = interactPromptUI.transform.localScale;
            interactPromptUI.transform.localScale = Vector3.zero; // 👈 ตั้งค่าเริ่มต้นให้เป็น 0
            interactPromptUI.SetActive(false);
        }
    }

    void Update()
    {
        if (currentInteractable != null && Input.GetKeyDown(interactKey))
        {
            currentInteractable.Interact();
            //HidePrompt();
        }

        if (interactPromptUI != null && interactPromptUI.activeSelf && currentTargetCollider != null)
        {
            UpdatePromptPosition();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        IInteractable interactable = other.GetComponent<IInteractable>();
        if (interactable != null)
        {
            currentInteractable = interactable;
            currentTargetCollider = other;
            ShowPrompt(interactable);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var interactable = other.GetComponent<IInteractable>();
        if (interactable != null && interactable == currentInteractable)
        {
            HidePrompt();
        }
    }

    private void ShowPrompt(IInteractable interactable)
    {
        if (interactPromptUI == null || currentTargetCollider == null) return;

        if (promptText != null)
        {
            promptText.text = interactable.GetInteractText();
        }

        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(AnimatePromptCoroutine(true));
    }

    private void HidePrompt()
    {
        currentInteractable = null;
        currentTargetCollider = null;

        if (interactPromptUI != null && interactPromptUI.activeSelf)
        {
            if (animationCoroutine != null) StopCoroutine(animationCoroutine);
            animationCoroutine = StartCoroutine(AnimatePromptCoroutine(false));
        }
        else if (interactPromptUI != null)
        {
            interactPromptUI.SetActive(false);
        }
    }

    private void UpdatePromptPosition()
    {
        if (currentTargetCollider == null)
        {
            HidePrompt();
            return;
        }

        Vector3 topPosition = currentTargetCollider.bounds.center + new Vector3(0, currentTargetCollider.bounds.extents.y, 0);
        float yOffset = Mathf.Sin(Time.time * animationSpeed) * animationAmplitude;
        Vector3 animationOffset = new Vector3(0, yOffset, 0);
        interactPromptUI.transform.position = topPosition + promptOffset + animationOffset;
        interactPromptUI.transform.rotation = Quaternion.identity;
    }

    private IEnumerator AnimatePromptCoroutine(bool appear)
    {
        if (interactPromptUI == null) yield break;

        float duration = appear ? appearTime : disappearTime;
        Vector3 startScale = interactPromptUI.transform.localScale;
        Vector3 endScale = appear ? originalPromptScale : Vector3.zero;

        if (appear)
        {
            UpdatePromptPosition();
            interactPromptUI.SetActive(true);
            interactPromptUI.transform.localScale = Vector3.zero;
            yield return null;
        }

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            float easeProgress = Mathf.Sin(progress * Mathf.PI * 0.5f);

            interactPromptUI.transform.localScale = Vector3.Lerp(startScale, endScale, easeProgress);

            yield return null;
        }

        interactPromptUI.transform.localScale = endScale;

        if (!appear)
        {
            interactPromptUI.SetActive(false);
        }

        animationCoroutine = null;
    }
}