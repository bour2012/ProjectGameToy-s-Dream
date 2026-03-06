using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ========================================
// Boss Health Bar Component
// ========================================
public class BossHealthBar : MonoBehaviour
{
    [Header("UI References")]
    public Slider healthSlider;
    public Image fillImage;
    public TextMeshProUGUI bossNameText;
    public TextMeshProUGUI healthText;
    public CanvasGroup canvasGroup;

    [Header("Settings")]
    public Gradient healthGradient;
    public float smoothSpeed = 5f;

    private float maxHealth;
    private float targetHealth;
    private float displayedHealth;

    void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    public void SetMaxHealth(float max)
    {
        maxHealth = max;
        targetHealth = max;
        displayedHealth = max;

        if (healthSlider != null)
        {
            healthSlider.maxValue = max;
            healthSlider.value = max;
        }

        UpdateHealthText();
    }

    public void SetHealth(float health)
    {
        targetHealth = Mathf.Clamp(health, 0, maxHealth);
    }

    public void SetBossName(string name)
    {
        if (bossNameText != null)
        {
            bossNameText.text = name;
        }
    }

    void Update()
    {
        // Smooth health bar animation
        if (Mathf.Abs(displayedHealth - targetHealth) > 0.1f)
        {
            displayedHealth = Mathf.Lerp(displayedHealth, targetHealth, Time.deltaTime * smoothSpeed);

            if (healthSlider != null)
            {
                healthSlider.value = displayedHealth;
            }

            // เปลี่ยนสีตามเลือด
            if (fillImage != null && healthGradient != null)
            {
                fillImage.color = healthGradient.Evaluate(displayedHealth / maxHealth);
            }

            UpdateHealthText();
        }
    }

    void UpdateHealthText()
    {
        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(displayedHealth)} / {Mathf.CeilToInt(maxHealth)}";
        }
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