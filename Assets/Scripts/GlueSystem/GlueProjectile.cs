using System.Collections;
using UnityEngine;
using UnityEngine.U2D.Animation;
using System.Collections.Generic;
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
    public float timeGlueStick = 10f;           // แรงยึดติดเมื่อโดนเป้า

    public LayerMask targetLayers = -1;      // Layer ที่สามารถยึดติดได้
    public LayerMask groundLayers = -1;      // Layer ที่สามารถยึดติดได้
    public LayerMask excludedTargetLayers = 0;      // Layer ที่สามารถยึดติดได้

    [Header("Effects")]
    public GameObject impactEffect;          // Effect เมื่อกระทบ
    public GameObject splatEffect;           // Effect กาวกระเซ็น
    [Header("Burning / Boss Fire")]
    public Sprite burningSprite;             // optional sprite to show when glue is on fire
    public GameObject burningEffectPrefab;   // optional VFX when glue becomes burning
    public float burningDamage = 15f;        // damage applied to Boss when burning glue hits
    [Tooltip("Turn speed (deg/sec) used to smoothly rotate the glue to face its velocity when burning")]
    public float burningRotationTurnSpeed = 720f;

    [Header("Audio")]
    public AudioClip shootSound;
    public AudioClip impactSound;

    [Header("Sprite Options")]
    [Tooltip("Sprite used while the glue is flying/being shot")]
    public Sprite flyingSprite;
    [Tooltip("Sprite used once the glue has hit and stuck to a target")]
    public Sprite stuckSprite;
    [Tooltip("Degrees offset applied when orienting the flying sprite to its velocity")]
    public float flyingRotationOffset = 0f;
    [Tooltip("Degrees offset applied when orienting the stuck sprite to face the target (flat side)")]
    public float stuckRotationOffset = 0f;
    [Tooltip("How long the SpriteSkin remains simulated (jiggle) before freezing the bones into the final pose")]
    public float stickySkinDuration = 1.0f;

    // Private Variables
    private Rigidbody2D rb;
    private AudioSource audioSource;
    private SpriteRenderer spriteRenderer;
    [Header("2D Skin / Bones")]
    public SpriteSkin spriteSkin;
    [Tooltip("Optional: root transform that contains bone rigidbodies. If empty, will search children.")]
    public Transform bonesRoot;
    [Tooltip("The root/core bone that should remain kinematic (anchored) at all times")]
    public Rigidbody2D rootBone;
    [Tooltip("Optional manual list of bone Rigidbody2D components. If empty, children under bonesRoot will be used.")]
    public Rigidbody2D[] boneRigidbodies;


    // keep original body types/constraints so we can restore them
    private RigidbodyType2D mainInitialBodyType;
    private Dictionary<Rigidbody2D, RigidbodyType2D> boneInitialBodyTypes = new Dictionary<Rigidbody2D, RigidbodyType2D>();
    private Dictionary<Rigidbody2D, RigidbodyConstraints2D> boneInitialConstraints = new Dictionary<Rigidbody2D, RigidbodyConstraints2D>();

    private bool hasStuck = false;               // ป้องกัน trigger ซ้ำ
    private bool hasSlowed = false;              // ป้องกัน slow ซ้ำ
    private bool hasSlowedHard = false;              // ป้องกัน slow ซ้ำ
    private bool isBurning = false;               // glue is on fire (can damage boss)
 /*   private bool isOnGround = false;*/             // ตรวจสอบว่าติดพื้นหรือไม่
    private bool hasStartedDestroyCountdown = false;
    //private bool hasExtendedDestroyTime = false; // เพิ่ม flag

    private float currentDestroyDelay;           // เวลาปัจจุบันสำหรับ Destroy
    private Coroutine destroyCoroutine;          // เก็บ Coroutine ปัจจุบัน

    //private float slowCooldown = 0.2f;           // interval สำหรับ slow
    //private float lastSlowTime = -1f;
    private float destroyStartTime;

    private bool targetInside = false;   // flag ว่ามี target อยู่ข้างในไหม
    private Collider2D currentTarget;    // เก็บ target ปัจจุบัน
    public float RemainingLifetime => Mathf.Max(0, (destroyStartTime + currentDestroyDelay) - Time.time);

    #region Unity Callbacks
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // record initial main body type
        if (rb != null)
            mainInitialBodyType = rb.bodyType;

        // collect bone rigidbodies if not provided
        CollectBoneRigidbodies();

        // store initial bone settings
        foreach (var boneRb in boneRigidbodies)
        {
            if (boneRb == null) continue;
            boneInitialBodyTypes[boneRb] = boneRb.bodyType;
            boneInitialConstraints[boneRb] = boneRb.constraints;
        }

        // Default to flying mode on spawn
        SetFlyingMode();

        // ทำลายตัวเองหลังเวลาที่กำหนด
        //Destroy(gameObject, lifetime);
    }

    private void Start()
    {
        PlaySound(shootSound); // เล่นเสียงยิงตอนเริ่ม
        // Set initial sprite for flying if provided
        if (spriteRenderer != null && flyingSprite != null)
            spriteRenderer.sprite = flyingSprite;
    }

    private void Update()
    {

    }
    private void LateUpdate()
    {
        // When burning, smoothly rotate toward velocity
        if (isBurning)
        {
            float desiredAngle = transform.rotation.eulerAngles.z;
            // prefer Rigidbody2D velocity if available
            if (rb != null && rb.linearVelocity.sqrMagnitude > 0.0001f)
            {
                Vector2 v = rb.linearVelocity;
                desiredAngle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            }
            else
            {
                // fallback: use current forward direction
                Vector3 fwd = transform.right;
                desiredAngle = Mathf.Atan2(fwd.y, fwd.x) * Mathf.Rad2Deg;
            }

            float current = transform.rotation.eulerAngles.z;
            float maxDelta = burningRotationTurnSpeed * Time.deltaTime;
            float newAngle = Mathf.MoveTowardsAngle(current, desiredAngle, maxDelta);
            transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
            return;
        }

        // When not burning, while flying face velocity instantly (so the flat side can be oriented correctly)
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.0001f)
        {
            Vector2 v = rb.linearVelocity;
            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg + flyingRotationOffset;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
    private void FixedUpdate()
    {
        if (currentTarget != null)
        {
            bool stillInside = IsTargetStillInside(currentTarget);
            if (!stillInside)
            {
                targetInside = false;
                currentTarget = null;
                Debug.Log("Target exited glue (detected in FixedUpdate).");
            }
        }
    }

    private bool IsTargetStillInside(Collider2D target)
    {
        // ตรวจสอบว่ามี Collider ของ target อยู่ในพื้นที่หรือไม่
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, 0.5f, targetLayers);
        foreach (var col in colliders)
        {
            if (col == target)
            {
                return true; // target ยังอยู่ในพื้นที่
            }
        }
        return false; // target ออกจากพื้นที่แล้ว
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasStuck) return;

        // Boss fire barrier: glue passing through becomes "burning" (ไฟ)
        if (other != null && other.CompareTag("BossFireBarrier"))
        {
            if (!isBurning)
            {
                BecomeBurning();
            }
            // don't treat barrier as ground/target; let projectile continue
            return;
        }

        int layerMask = 1 << other.gameObject.layer;



        if ((layerMask & groundLayers) != 0)
        {
            StopOnGround(other);
            Debug.Log($"Enter Ground: {other.name} at {other.transform.position}");
            hasStuck = true;
        }
        else if ((layerMask & excludedTargetLayers) != 0)
        {
            return;
        }
        else if ((layerMask & targetLayers) != 0)
        {
            // If glue is burning and we hit a Boss, apply damage immediately
            // use InParent in case collider is on child of boss GameObject
            var boss = other.GetComponentInParent<BossController>();
            if (isBurning && boss != null)
            {
                // apply damage that bypasses falling-only restriction
                boss.ReceiveEnvironmentalDamage(burningDamage);
                CreateImpactEffects();
                PlaySound(impactSound);
                Destroy(gameObject);
                return;
            }

            StickToTarget(other, timeStickToTarget);
            targetInside = true;
            currentTarget = other;
            Debug.Log($"Enter: {other.name} at {other.transform.position}");
            //ApplySlow(other);
            if (RemainingLifetime > timeGlueStick)
            {
                ApplySlow(other);
            }
        }

        else
        {
            //StopOnGround(other); // กรณีอื่นถือว่าเหมือนพื้น
            //hasStuck = true;

        }


    }

    private void OnTriggerExit2D(Collider2D other)
    {
       int layerMask = 1 << other.gameObject.layer;

    if ((layerMask & targetLayers) != 0 && other == currentTarget)
    {
        if (!IsTargetStillInside(other))
        {
            targetInside = false; // ตั้งค่า targetInside เป็น false
            currentTarget = null;
            Debug.Log($"Target {other.name} exited glue.");
        }
        else
        {
            Debug.Log($"False exit detected for {other.name}, still colliding.");
        }
    }

        //int layerMask = 1 << other.gameObject.layer;

        // ถ้า collider ไม่ใช่ targetLayer ให้ targetInside = true (ยังถือว่าติดกาว)
        //if ((layerMask & targetLayers) != 0)
        //{
        //    targetInside = true;
        //    currentTarget = other;
        //    //ExtendDestroyTime(other);
        //    Debug.Log($"[Frame {Time.frameCount}] Target still inside glue: {other.name}");
        //}
        //else
        //{
        //    // collider เป็น targetLayer ที่เราต้องการ → ออกนอกกาว
        //    targetInside = false;
        //    currentTarget = null;
        //    Debug.Log($"[Frame {Time.frameCount}] Target EXIT glue: {other.name}");
        //}
    }
 

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);

        if (currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(currentTarget.bounds.center, currentTarget.bounds.size);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
        }
    }
    private void OnTriggerStay2D(Collider2D other)
    {
        //if (!isOnGround) return;


        //    Debug.Log($"Stay: {other.name} at {other.transform.position}");
        //    targetInside = true;
        //    currentTarget = other;
        //    ExtendDestroyTime(other);
        //    AttachJoint(other);

        int layerMask = 1 << other.gameObject.layer;

        if ((layerMask & targetLayers) != 0)
        {
            Debug.Log($"Stay: {other.name} at {other.transform.position}");
            targetInside = true;
            currentTarget = other;
            ExtendDestroyTime(other);
            AttachJoint(other);
        }

        //if (!isOnGround) return;

        //targetInside = true;
        //currentTarget = other;
        //ExtendDestroyTime(other);

        ////float timeNow = Time.time;
        ////if (timeNow - lastSlowTime < slowCooldown) return;
        ////lastSlowTime = timeNow;

        //// ยืดเวลาแค่ครั้งเดียว

        ////ApplySlow(other);
        //AttachJoint(other);
    }
    #endregion

    #region Main Logic
    private void StopOnGround(Collider2D ground)
    {
        // determine contact point and choose nearest bone as anchor, then switch to sticky
        Vector2 contactPoint = ground.ClosestPoint(transform.position);

        // orient to surface normal so the glue faces the actual contact surface
        Vector2 surfaceNormal = ((Vector2)transform.position - contactPoint).normalized;
        if (surfaceNormal.sqrMagnitude <= 0.000001f && rb != null)
        {
            // fallback: face opposite velocity when contact point exactly equals position
            if (rb.linearVelocity.sqrMagnitude > 0.000001f)
                surfaceNormal = -rb.linearVelocity.normalized;
            else
                surfaceNormal = Vector2.up;
        }

        OrientTowards(surfaceNormal, stuckRotationOffset);

        Rigidbody2D anchor = FindNearestBone(contactPoint);
        SetStickyMode(anchor);
        CreateImpactEffects();
        PlaySound(impactSound);

        transform.SetParent(ground.transform);
        //isOnGround = true;

        // swap to stuck sprite if provided (ground stick should also show stuck sprite)
        if (spriteRenderer != null && stuckSprite != null)
            spriteRenderer.sprite = stuckSprite;

        // allow sprite skin to simulate/jiggle for a short time, then freeze bones into final pose
        if (spriteSkin != null)
            StartCoroutine(FinishStickyAfter(stickySkinDuration));

        Debug.Log($"Glue stuck on ground: {ground.name}");

        // เริ่มนับเวลาทำลายแบบปกติ
        currentDestroyDelay = groundDestroyDelay;
        StartDestroyCountdown(currentDestroyDelay);
    }

    private void StickToTarget(Collider2D target, float destroyDelay)
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (target.gameObject.layer != enemyLayer)
        { 
              // choose nearest bone to contact point as anchor and switch to sticky
              Vector2 contactPoint = target.ClosestPoint(transform.position);
              Rigidbody2D anchor = FindNearestBone(contactPoint);
              SetStickyMode(anchor);
        }
      
        //CreateImpactEffects();
        PlaySound(impactSound);

        int targetLayerMask = 1 << target.gameObject.layer;
        int layerMask = 1 << target.gameObject.layer;
        if ((layerMask & excludedTargetLayers) != 0)
        {
            hasStuck = true;
            return;
        }

        transform.SetParent(target.transform);

        // swap to stuck sprite if provided
        if (spriteRenderer != null && stuckSprite != null)
            spriteRenderer.sprite = stuckSprite;

        // determine contact point on the target and orient using the surface normal
        Vector2 closestPoint = target.ClosestPoint(transform.position);
        Vector2 surfaceNormal = ((Vector2)transform.position - closestPoint).normalized;
        if (surfaceNormal.sqrMagnitude <= 0.000001f)
        {
            // fallback: use opposite velocity if available, otherwise default up
            if (rb != null && rb.linearVelocity.sqrMagnitude > 0.000001f)
                surfaceNormal = -rb.linearVelocity.normalized;
            else
                surfaceNormal = Vector2.up;
        }
        OrientTowards(surfaceNormal, stuckRotationOffset);

        // allow sprite skin to simulate/jiggle for a short time, then freeze bones into final pose
        if (spriteSkin != null)
            StartCoroutine(FinishStickyAfter(stickySkinDuration));

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

        // สุดท้ายล็อกติดแน่น และสลับเป็น Sticky/Jelly mode
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        transform.position = stickPoint;
        Rigidbody2D anchor = FindNearestBone(stickPoint);
        SetStickyMode(anchor);
    }

    private void StopMovement()
    {
        if (rb == null) return;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    // Collect bone rigidbodies from bonesRoot or children if not manually assigned
    private void CollectBoneRigidbodies()
    {
        if (boneRigidbodies != null && boneRigidbodies.Length > 0) return;

        List<Rigidbody2D> found = new List<Rigidbody2D>();
        Transform searchRoot = bonesRoot != null ? bonesRoot : transform;
        var rbs = searchRoot.GetComponentsInChildren<Rigidbody2D>(true);
        foreach (var b in rbs)
        {
            // exclude the main projectile Rigidbody if accidentally included
            if (b == rb) continue;
            found.Add(b);
        }

        // ensure rootBone (if explicitly assigned) is included in the bones list
        if (rootBone != null && !found.Contains(rootBone))
            found.Add(rootBone);

        boneRigidbodies = found.ToArray();
    }

    // Find the bone Rigidbody2D that is nearest to the given world point.
    // If none found, return rootBone if assigned, otherwise null.
    private Rigidbody2D FindNearestBone(Vector2 worldPoint)
    {
        if (boneRigidbodies == null || boneRigidbodies.Length == 0)
        {
            return rootBone != null ? rootBone : null;
        }

        Rigidbody2D best = null;
        float bestSqr = float.MaxValue;
        foreach (var b in boneRigidbodies)
        {
            if (b == null) continue;
            var pos = b.transform.position;
            float sqr = (pos.x - worldPoint.x) * (pos.x - worldPoint.x) + (pos.y - worldPoint.y) * (pos.y - worldPoint.y);
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = b;
            }
        }

        if (best == null && rootBone != null)
            return rootBone;

        return best;
    }

    // -- Mode Switching API -------------------------------------------------
    // Flying Mode: main Rigidbody Dynamic, SpriteSkin disabled, bones Kinematic (locked)
    public void SetFlyingMode()
    {
        // Sprite skin off (no deformation)
        if (spriteSkin != null)
            spriteSkin.enabled = false;

        // main body dynamic so projectile flies according to physics
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
        }

        // bones locked as Kinematic so visuals don't flop
        foreach (var boneRb in boneRigidbodies)
        {
            if (boneRb == null) continue;
            if (boneRb == rootBone)
            {
                // keep root bone kinematic and simulated so it acts as a fixed anchor
                boneRb.bodyType = RigidbodyType2D.Kinematic;
                boneRb.constraints = RigidbodyConstraints2D.FreezeAll;
                boneRb.simulated = true;
            }
            else
            {
                boneRb.bodyType = RigidbodyType2D.Kinematic;
                boneRb.constraints = RigidbodyConstraints2D.FreezeAll;
                boneRb.simulated = false; // optionally disable simulation to fully lock
            }
        }
    }

    // Sticky/Jelly Mode: SpriteSkin enabled, main Rigidbody Kinematic, bones Dynamic
    // If anchorBone != null, that bone will be set as the kinematic anchor; others become dynamic.
    public void SetStickyMode(Rigidbody2D anchorBone)
    {
        // enable sprite skin for deformation
        if (spriteSkin != null)
            spriteSkin.enabled = true;

        // stop main body and make kinematic so it sticks
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
        }

        // bones: anchorBone -> Kinematic (anchored), others -> Dynamic
        foreach (var boneRb in boneRigidbodies)
        {
            if (boneRb == null) continue;

            if (anchorBone != null && boneRb == anchorBone)
            {
                // make the chosen bone the anchored pin but don't fully FreezeAll yet
                // Leave rotation frozen so the anchor stays oriented but allow positional parenting/jiggle
                boneRb.simulated = true;
                boneRb.constraints = RigidbodyConstraints2D.FreezeRotation;
                boneRb.bodyType = RigidbodyType2D.Kinematic;
            }
            else if (rootBone != null && anchorBone == null && boneRb == rootBone)
            {
                // if no anchor specified but a rootBone exists, keep it anchored but not fully frozen
                boneRb.simulated = true;
                boneRb.constraints = RigidbodyConstraints2D.FreezeRotation;
                boneRb.bodyType = RigidbodyType2D.Kinematic;
            }
            else
            {
                // outer bones become dynamic to jiggle
                boneRb.simulated = true;
                boneRb.constraints = RigidbodyConstraints2D.None;
                boneRb.bodyType = RigidbodyType2D.Dynamic;
            }
        }
    }

    // Backwards-compatible parameterless version: try to use rootBone if assigned
    public void SetStickyMode()
    {
        SetStickyMode(rootBone);
    }

    // Wait for `duration` seconds while SpriteSkin simulates, then freeze bones into the current pose
    private IEnumerator FinishStickyAfter(float duration)
    {
        if (duration <= 0f)
        {
            FreezeBonesToPose();
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            // if the projectile or spriteSkin is destroyed, exit early
            if (this == null || spriteSkin == null)
                yield break;

            t += Time.deltaTime;
            yield return null;
        }

        // Freeze bones to lock the final deformed pose
        FreezeBonesToPose();
    }

    // Make all bones kinematic and freeze constraints so the visual locks in the final pose
    private void FreezeBonesToPose()
    {
        if (boneRigidbodies == null) return;

        foreach (var b in boneRigidbodies)
        {
            if (b == null) continue;
            b.simulated = true;
            b.bodyType = RigidbodyType2D.Kinematic;
            b.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

    // Orient the projectile so its forward (right) faces `dir` with an optional offset in degrees
    private void OrientTowards(Vector2 dir, float offsetDegrees)
    {
        if (dir.sqrMagnitude <= 0.00001f) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + offsetDegrees;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void AttachJoint(Collider2D target)
    {
        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb == null) return;

        // ป้องกันการสร้าง joint ซ้ำ
        if (GetComponent<SpringJoint2D>() != null) return;

        // สร้าง SpringJoint2D
        SpringJoint2D joint = gameObject.AddComponent<SpringJoint2D>();
        joint.connectedBody = targetRb;

        // anchor ของตัวกาว (วางที่ศูนย์กลางของมัน)
        joint.anchor = Vector2.zero;

        // anchor ของ target (คำนวณตำแหน่งจุดชน)
        Vector2 hitPoint = target.transform.InverseTransformPoint(transform.position);
        joint.connectedAnchor = hitPoint;

        // ปรับค่าการยึด/ความหนืด
        joint.dampingRatio = 1f;     // หนืด (0 = ไม่มีหนืด, 1 = หนืดสุด)
        joint.frequency = 5f;        // ความถี่การสั่น (ค่าต่ำ = นุ่ม, ค่าสูง = แข็ง)
        joint.breakForce = stickForce; // กำหนดแรงที่ joint จะขาด

        Debug.Log($"Glue joint attached to {target.name} with force {stickForce}");
        //Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        //if (targetRb == null) return;

        //FixedJoint2D existingFixed = GetComponent<FixedJoint2D>();
        //SpringJoint2D existingSpring = GetComponent<SpringJoint2D>();
        //if (existingFixed != null || existingSpring != null) return;

        //// FixedJoint
        //FixedJoint2D jointF = gameObject.AddComponent<FixedJoint2D>();
        //jointF.connectedBody = targetRb;
        //jointF.breakForce = stickForce;

        //// SpringJoint
        //SpringJoint2D joint = gameObject.AddComponent<SpringJoint2D>();
        //joint.connectedBody = targetRb;

        //// ตั้งค่า anchor ให้ตรงจุดชน
        //Vector2 hitPoint = target.transform.InverseTransformPoint(transform.position);
        //joint.anchor = Vector2.zero;
        //joint.connectedAnchor = hitPoint;

        //joint.dampingRatio = 0.8f;
        //joint.frequency = 1f;
        //joint.breakForce = stickForce * 1.2f;
        //Debug.Log($"Glue joint attached to {target.name} with force {stickForce}");
    }

    private void BecomeBurning()
    {
        isBurning = true;

        // change sprite if provided
        if (burningSprite != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = burningSprite;
            spriteRenderer.enabled = true;
        }

        // immediately align to current velocity so the visual starts facing travel direction
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.0001f)
        {
            Vector2 v = rb.linearVelocity;
            float desiredAngle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, desiredAngle);
        }

        // play burning effect if any
        if (burningEffectPrefab != null)
        {
            var fx = Instantiate(burningEffectPrefab, transform.position, Quaternion.identity, transform);
            var ps = fx.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                float maxLifetime = (ps.main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants) ? ps.main.startLifetime.constantMax : ps.main.startLifetime.constant;
                Destroy(fx, ps.main.duration + maxLifetime + 0.25f);
            }
            else
            {
                Destroy(fx, 4f);
            }
        }

        Debug.Log("Glue became burning after passing BossFireBarrier");
    }

    private void ApplySlow(Collider2D target)
    {
        if (hasSlowed) return;

        // Guard against destroyed Unity objects: the Collider2D or its GameObject
        // may be destroyed while the coroutine is running which causes
        // MissingReferenceException when accessing components. Check for null
        // using Unity's overloaded null operator and bail out safely.
        if (target == null) return;

        ISlowable slowable = null;
        try
        {
            slowable = target.GetComponent<ISlowable>();
        }
        catch (MissingReferenceException)
        {
            // Target was destroyed mid-frame; ignore and stop attempting to slow it.
            return;
        }

        if (slowable != null)
        {
            // Gradually slow the target and mark as slowed
            slowable.ApplyGradualSlow(0.25f, 3.5f, 0.35f);
            hasSlowed = true;
            Debug.Log("Applied normal slow to target");
        }
    }


    private void ApplySlowHard(Collider2D target)
    {
        if (hasSlowedHard) return;

        if (target == null) return;

        ISlowable slowable = null;
        try
        {
            slowable = target.GetComponent<ISlowable>();
        }
        catch (MissingReferenceException)
        {
            return;
        }

        if (slowable != null)
        {
            // Gradually apply a stronger slow
            slowable.ApplyGradualSlow(0.05f, 5f, 0.35f);
            hasSlowedHard = true;

            Debug.Log("Applied hard slow to target");
        }
    }


    private void ExtendDestroyTime(Collider2D target)
    {
        //if (destroyCoroutine != null)
        //    StopCoroutine(destroyCoroutine);
            destroyCoroutine = StartCoroutine(DestroyCountdownRoutine(target));

        //currentDestroyDelay += 2f; // เพิ่มเวลายืด (ปรับได้)
        //destroyCoroutine = StartCoroutine(DestroyAfterDelay(currentDestroyDelay));
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

    private IEnumerator DestroyCountdownRoutine(Collider2D target)
    {
        while (RemainingLifetime > 0f)
        {
            // เช็คเงื่อนไขเรียลไทม์ทุกเฟรม
            // Protect against the target being destroyed while this coroutine runs.
            if (target == null || currentTarget == null)
            {
                // Clear flags and exit coroutine early when target no longer exists
                targetInside = false;
                currentTarget = null;
                yield break;
            }

            if (targetInside && currentTarget != null)
            {
                if (RemainingLifetime > timeGlueStick)
                {
                    // ถ้ายังมีเวลาเยอะ ใช้สโลว์ธรรมดา
                    ApplySlow(target);
                }
                else if (RemainingLifetime <= timeGlueStick)
                {
                    // ถ้าเวลาเหลือน้อยและ target ยังอยู่ข้างใน ใช้สโลว์หนัก
                    ApplySlowHard(target);
                }
            }
            // ถ้า targetInside == false จะไม่เรียก ApplySlowHard อีก

            yield return null; // รอ 1 frame
        }

    }
   
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

        //// ซ่อน Renderer และ Collider ก่อนทำลาย

        //SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        //if (spriteRenderer != null) spriteRenderer.enabled = false;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        if (hasSlowedHard)
            Destroy(gameObject, 2.5f);
        else
            Destroy(gameObject);
    }
    #endregion

    #region Public Methods
    public void SetStickForce(float force) => stickForce = force;
    public void SetLifetime(float time) => lifetime = time;
    #endregion
}
