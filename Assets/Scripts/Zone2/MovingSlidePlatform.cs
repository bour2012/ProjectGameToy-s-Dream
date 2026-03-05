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

    // --- [อัปเดตใหม่] รองรับ Animator หลายตัว ---
    [Header("Animation Settings")]
    [Tooltip("ความเร็วของ Animation ตอนที่โดนสโลว์ (0 = หยุดนิ่ง, 0.5 = ช้าลงครึ่งนึง)")]
    public float slowAnimationSpeed = 0f;

    private Animator[] animators; // ใช้ Array เพื่อเก็บ Animator ทุกตัว
    private float[] originalAnimSpeeds; // ใช้ Array เก็บค่าความเร็วเริ่มต้นของ Animator แต่ละตัว
    // ----------------------------------------

    private bool isSlowed = false;
    private float slowEndTime = 0f;
    private Coroutine mainSlowCoroutine;
    private Collider2D col;

    void Awake()
    {
        originalSpeed = normalSpeed;
        currentSpeed = normalSpeed;
        col = GetComponent<Collider2D>();

        // --- [อัปเดตใหม่] กวาดหา Animator ทั้งหมดในตัวลูกอัตโนมัติ ---
        animators = GetComponentsInChildren<Animator>();
        originalAnimSpeeds = new float[animators.Length];

        // จำค่าความเร็วเริ่มต้นของ Animator แต่ละตัวเอาไว้
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
            {
                originalAnimSpeeds[i] = animators[i].speed;
            }
        }
    }

    public void OnGlueAttached()
    {
        currentSpeed = 0f;
        isSlowed = true;

        // --- [อัปเดตใหม่] สั่งลดความเร็ว Animator ทุกตัวพร้อมกัน ---
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
            {
                animators[i].speed = slowAnimationSpeed;
            }
        }
    }

    public void OnGlueDetached()
    {
        currentSpeed = originalSpeed;
        isSlowed = false;

        // --- [อัปเดตใหม่] คืนความเร็วเดิมให้ Animator ทุกตัวพร้อมกัน ---
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
            {
                animators[i].speed = originalAnimSpeeds[i];
            }
        }

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

    public void ApplySlow(float slowAmount, float duration)
    {
        duration = duration + 2;

        if (duration <= 0f) return;

        glueHitCount++;
        if (glueHitCount == 1)
        {
            OnGlueAttached();
        }

        if (slowEndTime > Time.time)
        {
            slowEndTime += duration;
        }
        else
        {
            slowEndTime = Time.time + duration;
        }

        if (mainSlowCoroutine == null)
        {
            OnGlueAttached();
            mainSlowCoroutine = StartCoroutine(WaitUntilSlowEnd());
        }
    }

    public void ApplyGradualSlow(float targetSlowAmount, float duration, float lerpTime)
    {
        ApplySlow(targetSlowAmount, duration);
    }

    private System.Collections.IEnumerator WaitUntilSlowEnd()
    {
        while (Time.time < slowEndTime)
        {
            float waitTime = slowEndTime - Time.time;
            if (waitTime > 0)
            {
                yield return new WaitForSeconds(waitTime);
            }
            else
            {
                yield return null;
            }
        }

        OnGlueDetached();
        mainSlowCoroutine = null;
        glueHitCount = 0;
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

    private void ApplyPushToOverlapping()
    {
        if (col == null) return;

        Bounds b = col.bounds;
        Collider2D[] hits = Physics2D.OverlapBoxAll(b.center, b.size, transform.eulerAngles.z);
        foreach (var c in hits)
        {
            if (c == null) continue;
            Rigidbody2D rb = c.attachedRigidbody;
            if (rb == null) continue;

            Vector2 force = moveDirection.normalized * currentSpeed * 1200f * Time.fixedDeltaTime;
            rb.AddForce(force, ForceMode2D.Force);
        }
    }
}