using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossGlueMeter : MonoBehaviour
{
    [Header("UI References")]
    public Slider glueSlider;
    public Image fillImage;
    public TextMeshProUGUI glueText;
    public Image[] segmentImages; // สำหรับแสดงเป็น segment
    public CanvasGroup canvasGroup;

    [Header("Settings")]
    public Color emptyColor = Color.gray;
    public Color fillingColor = Color.yellow;
    public Color fullColor = Color.orange;
    public bool useSegments = false; // ใช้แบบ segment หรือ slider

    private int maxGlue;
    private int currentGlue;

    void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    public void SetMaxGlue(int max)
    {
        maxGlue = max;
        currentGlue = 0;

        if (useSegments && segmentImages != null)
        {
            // Setup segments
            for (int i = 0; i < segmentImages.Length; i++)
            {
                if (segmentImages[i] != null)
                {
                    segmentImages[i].gameObject.SetActive(i < max);
                    segmentImages[i].color = emptyColor;
                }
            }
        }
        else if (glueSlider != null)
        {
            glueSlider.maxValue = max;
            glueSlider.value = 0;
        }

        UpdateGlueText();
    }

    public void SetCurrentGlue(int amount)
    {
        currentGlue = Mathf.Clamp(amount, 0, maxGlue);

        if (useSegments && segmentImages != null)
        {
            // อัพเดท segments
            for (int i = 0; i < segmentImages.Length && i < maxGlue; i++)
            {
                if (segmentImages[i] != null)
                {
                    if (i < currentGlue)
                    {
                        segmentImages[i].color = (currentGlue >= maxGlue) ? fullColor : fillingColor;

                        // Pulse effect เมื่อเต็ม
                        if (currentGlue >= maxGlue)
                        {
                            StartCoroutine(PulseSegment(segmentImages[i]));
                        }
                    }
                    else
                    {
                        segmentImages[i].color = emptyColor;
                    }
                }
            }
        }
        else if (glueSlider != null)
        {
            glueSlider.value = currentGlue;

            if (fillImage != null)
            {
                float progress = (float)currentGlue / maxGlue;
                fillImage.color = Color.Lerp(fillingColor, fullColor, progress);
            }
        }

        UpdateGlueText();
    }

    void UpdateGlueText()
    {
        if (glueText != null)
        {
            glueText.text = $"Glue: {currentGlue}/{maxGlue}";
        }
    }

    System.Collections.IEnumerator PulseSegment(Image segment)
    {
        Vector3 originalScale = segment.transform.localScale;
        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float scale = 1f + Mathf.Sin(elapsed / duration * Mathf.PI) * 0.3f;
            segment.transform.localScale = originalScale * scale;
            elapsed += Time.deltaTime;
            yield return null;
        }

        segment.transform.localScale = originalScale;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }

    public void Hide()
    {
        if (canvasGroup != null)
        {
            StartCoroutine(FadeOut());
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    System.Collections.IEnumerator FadeOut()
    {
        float duration = 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            canvasGroup.alpha = 1f - (elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        gameObject.SetActive(false);
    }
}
