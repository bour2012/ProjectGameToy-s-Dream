using UnityEngine;

public class RepairToolIcon : MonoBehaviour
{
    [Header("Icon Settings")]
    public float floatAmplitude = 0.1f;
    public float floatSpeed = 2f;
    public float rotationSpeed = 50f;

    private Vector3 startPosition;
    private float timeOffset;

    void Start()
    {
        startPosition = transform.localPosition;
        timeOffset = Random.Range(0f, 2f * Mathf.PI);
    }

    void Update()
    {
        // เอฟเฟกต์ลอยขึ้นลง
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed + timeOffset) * floatAmplitude;
        transform.localPosition = new Vector3(startPosition.x, newY, startPosition.z);

        // เอฟเฟกต์หมุน
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
    }

    public void OnToolSelected()
    {
        // เอฟเฟกต์เมื่อเลือกเครื่องมือ
        StartCoroutine(SelectionPulse());
    }

    System.Collections.IEnumerator SelectionPulse()
    {
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * 1.3f;

        float time = 0;
        while (time < 0.1f)
        {
            time += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, targetScale, time / 0.1f);
            yield return null;
        }

        time = 0;
        while (time < 0.1f)
        {
            time += Time.deltaTime;
            transform.localScale = Vector3.Lerp(targetScale, originalScale, time / 0.1f);
            yield return null;
        }
    }
}
