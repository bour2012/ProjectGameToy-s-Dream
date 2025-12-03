using UnityEngine;
using System.Collections;

public class MovingSlidePlatform : MonoBehaviour, ISlowable
{
    public float normalSpeed = 2f;
    private float currentSpeed;
    private float originalSpeed;
    private Coroutine slowRoutine;
    private int glueHitCount = 0;
    public Vector2 moveDirection = Vector2.right;
    public bool isActive = true;
    [Header("Slow Behaviour")]
    [Tooltip("If true, when slowed the platform will stop applying movement (speed -> 0) instead of just reducing speed.")]
    public bool stopOnSlow = true;

    // ����Ѻ debug GUI
    private bool isSlowed = false;
    private float slowEndTime = 0f;
    private Coroutine mainSlowCoroutine;

    void Awake()
    {
        originalSpeed = normalSpeed;
        currentSpeed = normalSpeed;
        col = GetComponent<Collider2D>();
    }

    private Collider2D col;

    // Called when glue first attaches (first hit). Stops movement while glued.
    public void OnGlueAttached()
    {
        // stop applying movement immediately
        currentSpeed = 0f;
        isSlowed = true;
        // set a long slowEndTime for the OnGUI indicator if desired
       
    }

    // Called when the last glue instance is cleared. Restore movement.
    public void OnGlueDetached()
    {
        currentSpeed = originalSpeed;
        isSlowed = false;
        // When glue detaches, immediately apply a push to any overlapped rigidbodies
        // so the platform resumes affecting objects even if no new collision events occur.
        ApplyPushToOverlapping();
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

    // Treat ApplySlow/ApplyGradualSlow as glue attachments: count hits and use duration
    // to clear each attachment. This prevents stacking multiple slow effects.
    public void ApplySlow(float slowAmount, float duration)
    {
        duration = duration + 2;

        if (duration <= 0f)
            return;

        glueHitCount++;
        if (glueHitCount == 1)
        {
            OnGlueAttached();
        }

            // Compute new end time. If there's already remaining slow time, add duration to extend it,
            // otherwise start from now + duration.
            if (slowEndTime > Time.time)
            {
                slowEndTime += duration; // extend existing end time
            }
            else
            {
                slowEndTime = Time.time + duration; // start fresh
            }
            Debug.Log($"Slow active until: {slowEndTime} (Remaining: {slowEndTime - Time.time})");

        if (mainSlowCoroutine == null)
        {
            OnGlueAttached(); // เริ่มสโลว์
            mainSlowCoroutine = StartCoroutine(WaitUntilSlowEnd());
        }

    }

    public void ApplyGradualSlow(float targetSlowAmount, float duration, float lerpTime)
    {
        // treat the same as ApplySlow: count the hit and schedule removal
        ApplySlow(targetSlowAmount, duration);
    }

    private System.Collections.IEnumerator WaitUntilSlowEnd()
    {
        // ลูปเช็คเรื่อยๆ ตราบใดที่เวลายังไม่ถึงกำหนด
        while (Time.time < slowEndTime)
        {
            // รอจนกว่าจะถึงเวลาที่กำหนด
            // (การใช้ yield return ในลูปแบบนี้ ถ้าค่า slowEndTime เปลี่ยน มันจะวนกลับมาเช็คใหม่และรอเพิ่มเอง)
            float waitTime = slowEndTime - Time.time;
            if (waitTime > 0)
            {
                yield return new WaitForSeconds(waitTime);
            }
            else
            {
                yield return null; // กันเหนียว
            }
        }

        // เมื่อถึงเวลา (Time.time >= slowEndTime)
        OnGlueDetached();       // ยกเลิกสโลว์
        mainSlowCoroutine = null; // เคลียร์ตัวแปร เพื่อให้รอบหน้าสร้างใหม่ได้
        glueHitCount = 0;       // รีเซ็ตตัวนับ (ถ้ายังใช้อยู่)
    }
    private System.Collections.IEnumerator ClearGlueAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        glueHitCount = Mathf.Max(0, glueHitCount - 1);
        if (glueHitCount == 0)
        {
            OnGlueDetached();
        }
    }

    // Find overlapping colliders on the platform and apply one-off push so objects resume sliding
    private void ApplyPushToOverlapping()
    {
        if (col == null) return;

        Bounds b = col.bounds;
        // use OverlapBox to find colliders inside the platform bounds
        Collider2D[] hits = Physics2D.OverlapBoxAll(b.center, b.size, transform.eulerAngles.z);
        foreach (var c in hits)
        {
            if (c == null) continue;
            Rigidbody2D rb = c.attachedRigidbody;
            if (rb == null) continue;
            // apply same force formula used in OnCollisionStay2D
            Vector2 force = moveDirection.normalized * currentSpeed * 1200f * Time.fixedDeltaTime;
            rb.AddForce(force, ForceMode2D.Force);
        }
    }
    //private IEnumerator SlowRoutine(float slowAmount, float duration)
    //{
    //    if (stopOnSlow)
    //    {
    //        // immediately stop sliding
    //        currentSpeed = 0f;
    //        yield return new WaitForSeconds(duration);
    //        currentSpeed = originalSpeed;
    //    }
    //    else
    //    {
    //        currentSpeed = originalSpeed * slowAmount;
    //        yield return new WaitForSeconds(duration);
    //        currentSpeed = originalSpeed;
    //    }
    //    isSlowed = false;
    //}
    //private IEnumerator GradualSlowRoutine(float targetSlowAmount, float duration, float lerpTime)
    //{
    //    float startSpeed = currentSpeed;
    //    float targetSpeed = stopOnSlow ? 0f : originalSpeed * targetSlowAmount;
    //    float elapsed = 0f;
    //    while (elapsed < lerpTime)
    //    {
    //        currentSpeed = Mathf.Lerp(startSpeed, targetSpeed, elapsed / lerpTime);
    //        elapsed += Time.deltaTime;
    //        yield return null;
    //    }
    //    currentSpeed = targetSpeed;
    //    yield return new WaitForSeconds(duration);
    //    currentSpeed = originalSpeed;
    //    isSlowed = false;
    //}

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
