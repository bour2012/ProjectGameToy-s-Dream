using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformMovement : MonoBehaviour
{
    public Transform[] points;
    public float moveSpeed;
    private int pointIndex;
    private bool canMove = false;
    private bool isPlayerNear = false;

    [Tooltip("แพลตฟอร์มวิ่งวนลูปตามจุดใน points")]
    public bool modeLoop = false;
    [Tooltip("ถ้า true = โหมด loop จะเริ่มขยับหลังจากมีการชนด้วย collider")]
    public bool loopRequiresTrigger = false;
    [Tooltip("แพลตฟอร์มแกว่งซ้าย-ขวา")]
    public bool modeSwing = false;
    [Tooltip("เมื่อผู้เล่นเหยียบแล้วกระเด้งขึ้น")]
    public bool modeJumped = false;

    public AudioClip jumpSound;
    public AudioSource audioSource;

    public bool modeDestroyed = false;
    [Tooltip("แพลตฟอร์มยิงออกไปจากจุด spawn สู่ targetPoint")]
    public bool modeShoot = false;
    [Tooltip("Spawn projectiles repeatedly")]
    public bool shootContinuous = false;
    public float continuousShootInterval = 0.5f;
    [Tooltip("โหมดใช้ Animator คุมแพลตฟอร์ม")]
    public bool modeAnim = false;

    [Header("Destroy Settings")]
    public float timeDelay = 2f;

    [Header("Swing Settings")]
    public float swingSpeed = 2f;
    public float swingAngle = 30f;
    public float offset = 0f;

    [Header("Bounce Settings")]
    public float bounceForce = 15f;

    [Header("Shooting Settings")]
    public Transform targetPoint;
    public float travelTime = 10f;
    public Transform spawnPoint;
    public GameObject platformPrefab;
    private bool hasShot = false;
    [Tooltip("If true: when a spawned projectile is disabled, immediately respawn it at the spawnPoint.")]
    public bool respawnOnDisappear = true;

    // --- Object Pooling System ---
    // เก็บทุกกระสุนที่สร้างขึ้นมาเพื่อวนใช้ใหม่
    private List<GameObject> projectilePool = new List<GameObject>();
    private bool triggeredLoop = false;

    // Optimization: Cache Variables
    private Rigidbody2D myRb;
    private float startRotationZ;
    private Vector3 startPosition;

    [Header("Animation Settings")]
    public Animator anim;

    // Helper: เช็คความถูกต้องได้เร็วขึ้น
    private bool IsShootingSetupValid
    {
        get
        {
            return spawnPoint != null && targetPoint != null &&
                   spawnPoint.gameObject.activeInHierarchy && targetPoint.gameObject.activeInHierarchy;
        }
    }

    public enum ShootStaggerMode { Simultaneous, RandomSpread, SiblingStagger, ManualStagger }
    public ShootStaggerMode shootStaggerMode = ShootStaggerMode.Simultaneous;
    public float staggerInterval = 0.25f;
    public float randomSpread = 1f;
    public int manualOrder = 0;

    private void Start()
    {
        startPosition = transform.position;
        // transform.position = startPosition; // Optional reset
        startRotationZ = transform.eulerAngles.z;

        // Optimization: Cache Component
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        // Cache Rigidbody ถ้าใช้โหมด Anim
        myRb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (isPlayerNear && Input.GetKeyDown(KeyCode.E))
        {
            canMove = true;
            // Debug.Log("Player pressed E near platform."); 
        }

        HandleMovement();
        HandleSwing();

        if (modeShoot)
        {
            if (!IsShootingSetupValid)
            {
                // ถ้าจุด Spawn หายไป ให้เคลียร์กระสุนทิ้งให้หมด
                ClearAndDestroyPool();
                hasShot = false;
            }
            else
            {
                if (!hasShot)
                    StartCoroutine(StartShootingWithStagger());
            }
        }

        // Optimization: ใช้ cached Rigidbody แทน GetComponent ทุกเฟรม
        if (modeAnim && anim != null && myRb != null)
        {
            anim.SetFloat("Speed", myRb.linearVelocity.y);
        }
    }

    // แยกฟังก์ชันออกมาเพื่อให้อ่านง่ายและทำงานเร็ว
    private void HandleMovement()
    {
        bool shouldMove = false;

        if (modeLoop)
        {
            if (!loopRequiresTrigger || isPlayerNear || canMove || triggeredLoop)
                shouldMove = true;
        }
        else if (!modeSwing) // Non-loop & Non-swing
        {
            if ((canMove || triggeredLoop) && pointIndex < points.Length)
                shouldMove = true;
        }

        if (shouldMove && points != null && pointIndex < points.Length)
        {
            transform.position = Vector2.MoveTowards(transform.position, points[pointIndex].position, moveSpeed * Time.deltaTime);

            // Optimization: ใช้ sqrMagnitude แทน Distance (เร็วกว่า)
            if ((transform.position - points[pointIndex].position).sqrMagnitude < 0.0001f)
            {
                pointIndex += 1;
                if (modeLoop && pointIndex >= points.Length)
                {
                    pointIndex = 0;
                }
            }
        }
    }

    private void HandleSwing()
    {
        if (modeSwing)
        {
            float angle = Mathf.Sin((Time.time + offset) * swingSpeed) * swingAngle;
            transform.rotation = Quaternion.Euler(0f, 0f, startRotationZ + angle);
        }
    }

    // --- Object Pooling Logic ---
    // ฟังก์ชันสำหรับขอกระสุนจาก Pool
    private GameObject GetProjectileFromPool()
    {
        // 1. หาตัวที่ปิดอยู่ (Inactive) ใน Pool
        foreach (var p in projectilePool)
        {
            if (p != null && !p.activeInHierarchy)
            {
                p.transform.position = spawnPoint.position;
                p.transform.rotation = spawnPoint.rotation;
                p.SetActive(true);

                Animator pAnim = p.GetComponent<Animator>();
                if (pAnim != null)
                {
                    pAnim.Rebind();
                    //pAnim.SetTrigger("Shoot"); 
                }
                return p;
            }
        }

        // 2. ถ้าไม่มีว่างเลย หรือ Pool ยังว่างเปล่า ให้สร้างใหม่
        if (platformPrefab != null)
        {
            GameObject newObj = Instantiate(platformPrefab, spawnPoint.position, spawnPoint.rotation);
            newObj.SetActive(true); // บังคับเปิด
            Animator pAnim = newObj.GetComponent<Animator>();
            //if (pAnim != null)
            //{
            //    pAnim.SetTrigger("Shoot"); // ปลด comment บรรทัดนี้ ถ้าตัวกระสุนต้องใช้ Trigger "Shoot" ด้วย
            //}
            projectilePool.Add(newObj);
            return newObj;
        }

        return null;
    }

    private void ReturnProjectileToPool(GameObject p)
    {
        if (p != null)
        {
            p.SetActive(false); // ปิดการใช้งานแทนการ Destroy
        }
    }

    private void ClearAndDestroyPool()
    {
        // ใช้เมื่อ Platform หลักโดนทำลาย หรือ Disabled
        foreach (var p in projectilePool)
        {
            if (p != null) Destroy(p);
        }
        projectilePool.Clear();
    }

    private void OnDisable()
    {
        ClearAndDestroyPool();
        // Debug.Log("Platform disabled: Cleared pool.");
    }

    public void AnimationShoot()
    {
        if (anim != null) anim.SetTrigger("Shoot");
    }

    // Coroutine นี้ถูกปรับปรุงให้ใช้ Pooling
    private System.Collections.IEnumerator ShootPlatformRoutine(bool markDone = true)
    {
        if (spawnPoint == null || targetPoint == null)
        {
            hasShot = false;
            yield break;
        }

        if (anim != null)
        {
            anim.SetTrigger("Shoot");
        }

        // Delay ก่อนเริ่มยิงเล็กน้อย (ถ้าต้องการ)
        float shootDelay = 0.5f;
        if (shootDelay > 0f) yield return new WaitForSeconds(shootDelay);

        if (!IsShootingSetupValid)
        {
            hasShot = false;
            yield break;
        }

        // ขอวัตถุจาก Pool
        GameObject currentProjectile = GetProjectileFromPool();
        if (currentProjectile == null) yield break;

        float elapsed = 0f;
        Vector3 startPos = spawnPoint.position;
        Vector3 endPos = targetPoint.position;

        // Loop การเคลื่อนที่
        while (elapsed < travelTime)
        {
            // 1. เช็คความถูกต้องของจุด Spawn/Target
            if (!IsShootingSetupValid)
            {
                ReturnProjectileToPool(currentProjectile);
                ClearAndDestroyPool();
                yield break;
            }

            // 2. เช็คว่ากระสุนหายไปหรือถูกปิดไประหว่างทางหรือไม่
            if (currentProjectile == null || !currentProjectile.activeInHierarchy)
            {
                if (respawnOnDisappear && IsShootingSetupValid)
                {
                    // --- ส่วนที่แก้ไขตามคำขอ ---

                    // คำนวณเวลาที่เหลืออยู่ ว่าจริงๆ แล้วมันควรจะเดินทางอีกกี่วินาทีถึงจะจบ
                    float remainingTime = travelTime - elapsed;

                    // ถ้ามีเวลาเหลือ ให้รอก่อน (เพื่อไม่ให้เสกใหม่รัวๆ)
                    if (remainingTime > 0f)
                    {
                        yield return new WaitForSeconds(remainingTime);
                    }

                    // หลังจากรอจนครบเวลาของรอบนั้นแล้ว ค่อยเริ่ม Spawn ใหม่

                    // ดึงตัวเดิมหรือตัวใหม่จาก Pool
                    if (currentProjectile == null)
                    {
                        currentProjectile = GetProjectileFromPool();
                    }
                    else
                    {
                        currentProjectile.transform.position = spawnPoint.position;
                        currentProjectile.transform.rotation = spawnPoint.rotation;
                        currentProjectile.SetActive(true);

                        // Reset Animation ถ้าจำเป็น
                        Animator pAnim = currentProjectile.GetComponent<Animator>();
                        if (pAnim != null) pAnim.Rebind();
                    }

                    // Reset ค่าต่างๆ เพื่อเริ่ม Loop ใหม่เสมือนยิงนัดใหม่
                    startPos = spawnPoint.position;
                    // อัปเดต endPos เผื่อเป้าหมายขยับ
                    endPos = targetPoint.position;
                    elapsed = 0f;

                    // continue จะกระโดดกลับไปที่ start ของ while loop ทันที
                    continue;
                }
                else
                {
                    // ถ้าไม่ต้องการให้ Respawn ก็จบการทำงานไปเลย
                    break;
                }
            }

            // 3. คำนวณการเคลื่อนที่ปกติ
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / travelTime);

            if (currentProjectile != null)
                currentProjectile.transform.position = Vector3.Lerp(startPos, endPos, t);

            yield return null;
        }

        // เมื่อถึงเป้าหมาย หรือจบ Loop ตามเวลา
        ReturnProjectileToPool(currentProjectile);

        if (markDone)
        {
            yield return new WaitForSeconds(2f);
            hasShot = false;
        }
    }

    private void OnDestroy()
    {
        ClearAndDestroyPool();
    }

    private System.Collections.IEnumerator StartShootingWithStagger()
    {
        hasShot = true;
        float delay = 0f;

        switch (shootStaggerMode)
        {
            case ShootStaggerMode.Simultaneous: delay = 0f; break;
            case ShootStaggerMode.RandomSpread: delay = Random.Range(0f, randomSpread); break;
            case ShootStaggerMode.SiblingStagger: delay = transform.GetSiblingIndex() * staggerInterval; break;
            case ShootStaggerMode.ManualStagger: delay = manualOrder * staggerInterval; break;
        }

        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (shootContinuous)
        {
            StartCoroutine(ContinuousSpawn());
            yield break;
        }

        yield return StartCoroutine(ShootPlatformRoutine(true));
    }

    private System.Collections.IEnumerator ContinuousSpawn()
    {
        while (hasShot)
        {
            if (!IsShootingSetupValid)
            {
                ClearAndDestroyPool();
                yield break;
            }

            StartCoroutine(ShootPlatformRoutine(false));

            // ป้องกัน Interval เป็น 0 ซึ่งจะทำให้เกมค้าง
            float interval = Mathf.Max(0.02f, continuousShootInterval);
            yield return new WaitForSeconds(interval);
        }
    }

    public void ShootToTarget() { }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerNear = true;

            if (modeJumped)
            {
                Rigidbody2D rbPlayer = collision.gameObject.GetComponent<Rigidbody2D>();
                if (rbPlayer != null)
                {
                    rbPlayer.linearVelocity = new Vector2(rbPlayer.linearVelocity.x, 0f);
                    rbPlayer.AddForce(Vector2.up * bounceForce, ForceMode2D.Impulse);

                    if (anim != null) anim.SetTrigger("Jump");

                    if (jumpSound != null && audioSource != null)
                    {
                        audioSource.PlayOneShot(jumpSound);
                    }
                }
            }

            if (loopRequiresTrigger)
            {
                triggeredLoop = true;
                canMove = true;
            }
        }

        // Optimization check
        if (collision.gameObject.layer == LayerMask.NameToLayer("DeadZone"))
        {
            Destroy(gameObject, timeDelay);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerNear = false;
        }
    }
}