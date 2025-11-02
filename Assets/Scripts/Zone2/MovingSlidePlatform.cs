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

    void Awake()
    {
        originalSpeed = normalSpeed;
        currentSpeed = normalSpeed;
    }

    // ไม่มี FixedUpdate แล้ว

    // เมื่อ player เหยียบแท่นนี้ ให้ player ถูกเลื่อน!
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!isActive) return;

        Rigidbody2D hitRb = collision.rigidbody;
        if (hitRb != null)
        {
            // ใช้ AddForce ในทิศ platform
            Vector2 force = moveDirection.normalized * currentSpeed * 1200f * Time.fixedDeltaTime;
            hitRb.AddForce(force, ForceMode2D.Force);
        }
    }

    public void ApplySlow(float slowAmount, float duration)
    {
        if (slowRoutine != null) StopCoroutine(slowRoutine);
        slowRoutine = StartCoroutine(SlowRoutine(slowAmount, duration));
    }
    public void ApplyGradualSlow(float targetSlowAmount, float duration, float lerpTime)
    {
        if (slowRoutine != null) StopCoroutine(slowRoutine);
        slowRoutine = StartCoroutine(GradualSlowRoutine(targetSlowAmount, duration, lerpTime));
    }
    private IEnumerator SlowRoutine(float slowAmount, float duration)
    {
        currentSpeed = originalSpeed * slowAmount;
        yield return new WaitForSeconds(duration);
        currentSpeed = originalSpeed;
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
    }
}
