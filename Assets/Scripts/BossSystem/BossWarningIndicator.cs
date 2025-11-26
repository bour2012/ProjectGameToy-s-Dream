using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossWarningIndicator : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI warningText;
    public Image backgroundImage;
    public CanvasGroup canvasGroup;

    [Header("Settings")]
    public string warningMessage = "WARNING: BOSS APPROACHING";
    public float displayDuration = 3f;
    public float pulseSpeed = 2f;
    public Color warningColor = Color.red;

    void Start()
    {
        if (warningText != null)
        {
            warningText.text = warningMessage;
        }
        StartCoroutine(ShowWarning());
    }

    System.Collections.IEnumerator ShowWarning()
    {
        float elapsed = 0f;

        // Fade in
        while (elapsed < 0.5f)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = elapsed / 0.5f;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Pulse
        elapsed = 0f;
        while (elapsed < displayDuration)
        {
            float pulse = (Mathf.Sin(elapsed * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;

            if (warningText != null)
            {
                Color color = warningColor;
                color.a = 0.7f + pulse * 0.3f;
                warningText.color = color;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Fade out
        elapsed = 0f;
        while (elapsed < 0.5f)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f - (elapsed / 0.5f);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}
