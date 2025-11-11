using TMPro;
using UnityEngine;

public class DamageNumber : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI damageText;

    [Header("Settings")]
    public float floatSpeed = 2f;
    public float lifetime = 1f;
    public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    private float elapsedTime;
    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        elapsedTime += Time.deltaTime;
        float progress = elapsedTime / lifetime;

        // ลอยขึ้นไป
        transform.position = startPosition + Vector3.up * floatSpeed * elapsedTime;

        // Scale animation
        float scale = scaleCurve.Evaluate(progress);
        transform.localScale = Vector3.one * scale;

        // Fade out
        if (damageText != null)
        {
            Color color = damageText.color;
            color.a = alphaCurve.Evaluate(progress);
            damageText.color = color;
        }
    }

    public void SetDamage(float damage)
    {
        if (damageText != null)
        {
            damageText.text = damage.ToString("F0");
        }
    }
}
