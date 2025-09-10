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
    public float groundDestroyDelay = 5f;    // เวลาทำลายเมื่อโดนพื้น
    public float stickForce = 10f;           // แรงยึดติดเมื่อโดนเป้า
    public float timeGlueStick = 10f;           // แรงยึดติดเมื่อโดนเป้า

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
    private bool hasSlowedHard = false;              // ป้องกัน slow ซ้ำ
    private bool isOnGround = false;             // ตรวจสอบว่าติดพื้นหรือไม่
    private bool hasStartedDestroyCountdown = false;
    private bool hasExtendedDestroyTime = false; // เพิ่ม flag

    private float currentDestroyDelay;           // เวลาปัจจุบันสำหรับ Destroy
    private Coroutine destroyCoroutine;          // เก็บ Coroutine ปัจจุบัน

    private float slowCooldown = 0.2f;           // interval สำหรับ slow
    private float lastSlowTime = -1f;
    private float destroyStartTime;
    private bool targetInside = false;   // flag ว่ามี target อยู่ข้างในไหม
    private Collider2D currentTarget;    // เก็บ target ปัจจุบัน
    public float RemainingLifetime => Mathf.Max(0, (destroyStartTime + currentDestroyDelay) - Time.time);

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

    private void Update()
    {
        //Debug.Log($"เหลือเวลาอีก {RemainingLifetime:F2} วินาทีก่อนสลาย");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasStuck) return;

        int layerMask = 1 << other.gameObject.layer;

        if ((layerMask & groundLayers) != 0)
        { StopOnGround(other); hasStuck = true; }
        else if ((layerMask & targetLayers) != 0)
        { StickToTarget(other, timeStickToTarget); }
        else
        {
            StopOnGround(other); // กรณีอื่นถือว่าเหมือนพื้น
            hasStuck = true; }

            //if ((layerMask & groundLayers) != 0)
            //{
            //    StopOnGround(other);
            //    hasStuck = true;
            //}
            //else if ((layerMask & targetLayers) != 0)
            //{

            //    StickToTarget(other, timeStickToTarget);

            //}
            //else if ((layerMask & excludedTargetLayers) != 0)
            //{
            //   return;
            //}
            //else
            //{
            //    StopOnGround(other); // กรณีอื่นถือว่าเหมือนพื้น
            //    hasStuck = true;

            //}


        }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == currentTarget)
        {
            targetInside = false;
            currentTarget = null;

        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {

        if (!isOnGround) return;

        targetInside = true;
        currentTarget = other;

        int layerMask = 1 << other.gameObject.layer;
        if ((layerMask & targetLayers) == 0) return;

        //float timeNow = Time.time;
        //if (timeNow - lastSlowTime < slowCooldown) return;
        //lastSlowTime = timeNow;

        // ยืดเวลาแค่ครั้งเดียว
       // if (!hasExtendedDestroyTime)
       // {
            ExtendDestroyTime(other);
           // hasExtendedDestroyTime = true;
      //  }
   

       // StartCoroutine(ApplySlow(other));
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

        if (hasStuck) return;

        hasStuck = true; // ป้องกัน trigger ซ้ำ


        int layerMask = 1 << target.gameObject.layer;
        if ((layerMask & excludedTargetLayers) != 0) { hasStuck = true; return; }
        transform.SetParent(target.transform); 
        Debug.Log($"Glue stuck directly to target: {target.name}");
        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb == null) return;
        // เริ่ม Coroutine ชะลอก่อนติดหนึบ
        StartCoroutine(SlowThenStick(targetRb, 5));

    }

    private IEnumerator SlowThenStick(Rigidbody2D targetRb, float destroyDelay)
    {
        if (rb == null) yield break;
        rb.bodyType = RigidbodyType2D.Dynamic; // ยังขยับได้
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = transform.position; // คงตำแหน่งเดิมจนกว่าจะติด

        while (elapsed < destroyDelay)
        {
            elapsed += Time.deltaTime;

            // ค่อย ๆ ลดความเร็วลง
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, elapsed / destroyDelay);

            // ถ้าอยากให้กระสุนค่อย ๆ ขยับเข้าหาตำแหน่งเป้าหมายเล็กน้อย
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / destroyDelay);

            yield return null;
        }



        //หลังหมดเวลา->ติดหนึบแน่น
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        transform.position = targetPos;

        // ต่อ Joint หรือทำให้ติดแน่น
        //FixedJoint2D jointF = gameObject.AddComponent<FixedJoint2D>();
        // jointF.connectedBody = targetRb;
        // jointF.breakForce = stickForce;

        // เริ่มนับเวลาทำลาย
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
    //private IEnumerator ApplySlow(Collider2D target)
    //{
    //    if (hasSlowed) yield break; // ป้องกัน slow ซ้ำ

    //    ISlowable slowable = target.GetComponent<ISlowable>();
    //    if (slowable == null) yield break;

    //    float initialSlowTime = 0.5f;   // เวลาสำหรับ slow แบบเร็ว
    //    float totalSlowTime = 1f;     // เวลารวมของการ slow

    //    float elapsed = 0f;

    //    // ขั้นแรก: slow แบบเร็ว
    //    while (elapsed < initialSlowTime)
    //    {
    //        elapsed += Time.deltaTime;
    //        float t = elapsed / initialSlowTime; // 0 → 1
    //        float currentSpeed = Mathf.Lerp(1f, 0.2f, t); // เริ่มจาก 1 → 0.2
    //        slowable.ApplyGradualSlow(currentSpeed, totalSlowTime, 0.02f);
    //        yield return null;
    //    }

    //    // ขั้นสอง: slow แบบช้าลงจนหมดเวลา
    //    elapsed = 0f;
    //    float remainingTime = totalSlowTime - initialSlowTime;
    //    while (elapsed < remainingTime)
    //    {
    //        elapsed += Time.deltaTime;
    //        float t = elapsed / remainingTime; // 0 → 1
    //        float currentSpeed = Mathf.Lerp(0.2f, 0.05f, t); // ค่อยๆ ลดจาก 0.2 → 0.05
    //        slowable.ApplyGradualSlow(currentSpeed, remainingTime, 0.35f);
    //        yield return null;
    //    }

    //    hasSlowed = true; // ป้องกัน slow ซ้ำ
    //}

    private void ApplySlow(Collider2D target)
    {
        if (hasSlowed) return;

        ISlowable slowable = target.GetComponent<ISlowable>();

        if (slowable != null)
        {
            // ค่อยๆ ลดความเร็วลงเหลือ 0.05 ภายใน 1 วินาที และ slow ค้างไว้ 5 วินาที
            slowable.ApplyGradualSlow(0.20f, 3.5f, 0.35f);
            hasSlowed = true;
          Debug.Log("Applied normal slow to target");
        }
    }
    private void ApplySlowHard(Collider2D target)
    {
        if (hasSlowedHard) return;

        ISlowable slowable = target.GetComponent<ISlowable>();

        if (slowable != null)
        {
            // ค่อยๆ ลดความเร็วลงเหลือ 0.05 ภายใน 1 วินาที และ slow ค้างไว้ 5 วินาที
            slowable.ApplyGradualSlow(0.05f, 5f, 0.35f);
            hasSlowedHard = true;
            Debug.Log("Applied hard slow to target");
        }
    }

    private void ExtendDestroyTime(Collider2D target)
    {
        if (destroyCoroutine != null)
            StopCoroutine(destroyCoroutine);

        ////currentDestroyDelay += 3f; // เพิ่มเวลายืด (ปรับได้)

        //if (currentDestroyDelay > 2f)
        //    ApplySlow(target);
        //else
        //    ApplySlowHard(target);

        //destroyCoroutine = StartCoroutine(DestroyAfterDelay(currentDestroyDelay));
        // เริ่ม Coroutine ใหม่
        destroyCoroutine = StartCoroutine(DestroyCountdownRoutine(target));
    }


    private IEnumerator DestroyCountdownRoutine(Collider2D target)
    {
        //float timer = currentDestroyDelay; // เวลาเริ่มต้น
        while (RemainingLifetime > 0f)
        {
            // ทุก ๆ frame จะเช็คว่าเหลือเวลาเท่าไหร่

          // if (targetInside && target != null)
           // {
                if (RemainingLifetime > timeGlueStick)
                    ApplySlow(target);
                else
                    ApplySlowHard(target);
          //  }

            //currentDestroyDelay -= Time.deltaTime; // ลดเวลาไปเรื่อย ๆ
            yield return null; // รอ 1 frame
        }

        // เมื่อหมดเวลา -> ทำลาย object
        Destroy(gameObject);
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

        destroyStartTime = Time.time;      // บันทึกเวลาเริ่มนับ
        currentDestroyDelay = delay;       // เก็บ delay ไว้
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
