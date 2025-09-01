using System.Collections;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

// ========================================
// Glue Projectile - กระสุนกาว
// ========================================
public class GlueProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float lifetime = 5f;              // อายุของกระสุนก่อนหายไป
    public float timeStickToTarget = 5f;     // อายุของกระสุนเมื่อโดนเป้าหมาย
    public float groundDestroyDelay = 3f;    // เวลาทำลายเมื่อโดนพื้น
    public float stickForce = 10f;           // แรงยึดติดเมื่อโดนเป้า

    public LayerMask targetLayers = -1;      // Layer ที่สามารถยึดติดได้
    public LayerMask groundLayers = -1;      // Layer ที่สามารถยึดติดได้
    public LayerMask excludedTargetLayers = 0;      // Layer ที่สามารถยึดติดได้

    [Header("Effects")]
    public GameObject impactEffect;          // Effect เมื่อกระทบ
    public GameObject splatEffect;           // Effect กาวกระเซ็น

    [Header("Audio")]
    public AudioClip shootSound;
    public AudioClip impactSound;

    // Private Variables
    private Rigidbody2D rb;
    private AudioSource audioSource;

    private bool hasStuck = false;               // ป้องกัน trigger ซ้ำ
    private bool hasSlowed = false;              // ป้องกัน slow ซ้ำ
    private bool isOnGround = false;             // ตรวจสอบว่าติดพื้นหรือไม่
    private bool hasStartedDestroyCountdown = false;
    private bool hasExtendedDestroyTime = false; // เพิ่ม flag

    private float currentDestroyDelay;           // เวลาปัจจุบันสำหรับ Destroy
    private Coroutine destroyCoroutine;          // เก็บ Coroutine ปัจจุบัน

    private float slowCooldown = 0.2f;           // interval สำหรับ slow
    private float lastSlowTime = -1f;

    #region Unity Callbacks
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();

        // ทำลายตัวเองหลังเวลาที่กำหนด
        Destroy(gameObject, lifetime);
    }

    private void Start()
    {
        PlaySound(shootSound); // เล่นเสียงยิงตอนเริ่ม
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasStuck) return;

        int layerMask = 1 << other.gameObject.layer;

        if ((layerMask & groundLayers) != 0)
        {
            StopOnGround(other);
            hasStuck = true;
        }
        else if ((layerMask & targetLayers) != 0)
        {
            StickToTarget(other, timeStickToTarget);
        }
        else
        {
            StopOnGround(other); // กรณีอื่นถือว่าเหมือนพื้น
            hasStuck = true;

        }

       
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!isOnGround) return;

        int layerMask = 1 << other.gameObject.layer;
        if ((layerMask & targetLayers) == 0) return;

        float timeNow = Time.time;
        if (timeNow - lastSlowTime < slowCooldown) return;
        lastSlowTime = timeNow;

        // ยืดเวลาแค่ครั้งเดียว
        if (!hasExtendedDestroyTime)
        {
            ExtendDestroyTime();
            hasExtendedDestroyTime = true;
        }
        ApplySlow(other);
        AttachJoint(other);
    }
    #endregion

    #region Main Logic
    private void StopOnGround(Collider2D ground)
    {
        StopMovement();
        CreateImpactEffects();
        PlaySound(impactSound);

        transform.SetParent(ground.transform);
        isOnGround = true;

        Debug.Log($"Glue stuck on ground: {ground.name}");

        // เริ่มนับเวลาทำลายแบบปกติ
        currentDestroyDelay = groundDestroyDelay;
        StartDestroyCountdown(currentDestroyDelay);
    }

    private void StickToTarget(Collider2D target, float destroyDelay)
    {
        StopMovement();
        CreateImpactEffects();
        PlaySound(impactSound);

        int targetLayerMask = 1 << target.gameObject.layer;

        int layerMask = 1 << target.gameObject.layer;
        if ((layerMask & excludedTargetLayers) != 0)
        {
            hasStuck = true;
            return;
        }

        transform.SetParent(target.transform);
            Debug.Log($"Glue stuck directly to target: {target.name}");

        StartDestroyCountdown(destroyDelay);
    }

    private IEnumerator SlowDownAndStick(Vector3 stickPoint)
    {
        if (rb == null) yield break;

        rb.bodyType = RigidbodyType2D.Dynamic; // ให้ยังขยับได้

        float t = 0f;
        Vector3 startPos = transform.position;

        while (t < 1f)
        {
            t += Time.deltaTime / 1.5f; // 1.5f = เวลา slowdown (ปรับได้)

            // ค่อย ๆ ลดความเร็วลง
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, t);

            // ค่อย ๆ ขยับเข้าหาจุด stickPoint
            transform.position = Vector3.Lerp(startPos, stickPoint, t);

            yield return null;
        }

        // สุดท้ายล็อกติดแน่น
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        transform.position = stickPoint;
    }

    private void StopMovement()
    {
        if (rb == null) return;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    private void AttachJoint(Collider2D target)
    {
        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb == null) return;

        FixedJoint2D existingFixed = GetComponent<FixedJoint2D>();
        SpringJoint2D existingSpring = GetComponent<SpringJoint2D>();
        if (existingFixed != null || existingSpring != null) return;

        // FixedJoint
        FixedJoint2D jointF = gameObject.AddComponent<FixedJoint2D>();
        jointF.connectedBody = targetRb;
        jointF.breakForce = stickForce;

        // SpringJoint
        SpringJoint2D joint = gameObject.AddComponent<SpringJoint2D>();
        joint.connectedBody = targetRb;

        // ตั้งค่า anchor ให้ตรงจุดชน
        Vector2 hitPoint = target.transform.InverseTransformPoint(transform.position);
        joint.anchor = Vector2.zero;
        joint.connectedAnchor = hitPoint;

        joint.dampingRatio = 0.8f;
        joint.frequency = 1f;
        joint.breakForce = stickForce * 1.2f;
        Debug.Log($"Glue joint attached to {target.name} with force {stickForce}");
    }

    private void ApplySlow(Collider2D target)
    {
        if (hasSlowed) return;

        ISlowable slowable = target.GetComponent<ISlowable>();
        if (slowable != null)
        {
            // ค่อยๆ ลดความเร็วลงเหลือ 0.05 ภายใน 1 วินาที และ slow ค้างไว้ 5 วินาที
            slowable.ApplyGradualSlow(0.05f, 5f, 0.35f);
            hasSlowed = true;
        }
    }

    private void ExtendDestroyTime()
    {
        if (destroyCoroutine != null)
            StopCoroutine(destroyCoroutine);

        currentDestroyDelay += 2f; // เพิ่มเวลายืด (ปรับได้)
        destroyCoroutine = StartCoroutine(DestroyAfterDelay(currentDestroyDelay));
    }
    #endregion

    #region Effects & Audio
    private void CreateImpactEffects()
    {
        Vector3 impactPos = transform.position;

        if (impactEffect != null)
        {
            GameObject impact = Instantiate(impactEffect, impactPos, Quaternion.identity);
            Destroy(impact, 2f);
        }

        if (splatEffect != null)
        {
            GameObject splat = Instantiate(splatEffect, impactPos, Quaternion.identity);
            Destroy(splat, 5f);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
    #endregion

    #region Destroy Logic
    private void StartDestroyCountdown(float delay)
    {
        if (hasStartedDestroyCountdown) return;
        hasStartedDestroyCountdown = true;
        destroyCoroutine = StartCoroutine(DestroyAfterDelay(delay));
    }

    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // ซ่อน Renderer และ Collider ก่อนทำลาย
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) spriteRenderer.enabled = false;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Destroy(gameObject);
    }
    #endregion

    #region Public Methods
    public void SetStickForce(float force) => stickForce = force;
    public void SetLifetime(float time) => lifetime = time;
    #endregion
}
