using UnityEngine;
using System.Collections;

public class MovingSlidePlatform : MonoBehaviour, ISlowable
{
    public float normalSpeed = 2f;
    private float currentSpeed;
    private float originalSpeed;
    private Coroutine slowRoutine;
    public Vector2 moveDirection = Vector2.right;
    public bool isActive = true;

    // สำหรับ debug GUI
    private bool isSlowed = false;
    private float slowEndTime = 0f;

    void Awake()
    {
        originalSpeed = normalSpeed;
        currentSpeed = normalSpeed;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!isActive) return;
        Rigidbody2D hitRb = collision.rigidbody;
        if (hitRb != null)
        {
            Vector2 force = moveDirection.normalized * currentSpeed * 1200f * Time.fixedDeltaTime;
            hitRb.AddForce(force, ForceMode2D.Force);
        }
    }

    public void ApplySlow(float slowAmount, float duration)
    {
        if (slowRoutine != null) StopCoroutine(slowRoutine);
        slowRoutine = StartCoroutine(SlowRoutine(slowAmount, duration));
        isSlowed = true;
        slowEndTime = Time.time + duration;
    }
    public void ApplyGradualSlow(float targetSlowAmount, float duration, float lerpTime)
    {
        if (slowRoutine != null) StopCoroutine(slowRoutine);
        slowRoutine = StartCoroutine(GradualSlowRoutine(targetSlowAmount, duration, lerpTime));
        isSlowed = true;
        slowEndTime = Time.time + duration;
    }
    private IEnumerator SlowRoutine(float slowAmount, float duration)
    {
        currentSpeed = originalSpeed * slowAmount;
        yield return new WaitForSeconds(duration);
        currentSpeed = originalSpeed;
        isSlowed = false;
    }
    private IEnumerator GradualSlowRoutine(float targetSlowAmount, float duration, float lerpTime)
    {
        float startSpeed = currentSpeed;
        float targetSpeed = originalSpeed * targetSlowAmount;
        float elapsed = 0f;
        while (elapsed < lerpTime)
        {
            currentSpeed = Mathf.Lerp(startSpeed, targetSpeed, elapsed / lerpTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
        currentSpeed = targetSpeed;
        yield return new WaitForSeconds(duration);
        currentSpeed = originalSpeed;
        isSlowed = false;
    }

    void OnGUI()
    {
        if (!isSlowed) return;
        if (Camera.main == null) return;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
        float remain = Mathf.Max(0, slowEndTime - Time.time);

        GUI.color = Color.cyan;
        GUI.Label(new Rect(screenPos.x - 50, Screen.height - screenPos.y, 120, 25),
                  $"SLOWED! ({remain:F1}s)");
        GUI.color = Color.white;
    }
}
