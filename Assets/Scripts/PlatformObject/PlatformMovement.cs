using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformMovement : MonoBehaviour
{
    public Transform[] points;
    public float moveSpeed;
    private int pointIndex;
    private bool canMove = false; // ตัวแปรเช็คว่ากด E หรือยัง
    private bool isPlayerNear = false; // ผู้เล่นอยู่ใกล้หรือไม่
    [Tooltip("แพลตฟอร์มวิ่งวนลูปตามจุดใน points")]
    public bool modeLoop = false;
    [Tooltip("ถ้า true = โหมด loop จะเริ่มขยับหลังจากมีการชนด้วย collider (เช่น ผู้เล่นยืนบนแพลตฟอร์ม), ถ้า false = เริ่มขยับทันที")]
    public bool loopRequiresTrigger = false;
    [Tooltip("แพลตฟอร์มแกว่งซ้าย-ขวา")]
    public bool modeSwing = false;
    [Tooltip("เมื่อผู้เล่นเหยียบแล้วกระเด้งขึ้น (ใช้กับ bounce)")]
    public bool modeJumped = false;
    [Tooltip("เมื่อชนกับตัว DeadZone แล้ว Platform ถูกทำลาย")]
    public bool modeDestroyed = false;
    [Tooltip("แพลตฟอร์มยิงออกไปจากจุด spawn สู่ targetPoint")]
    public bool modeShoot = false;
    [Tooltip("โหมดใช้ Animator คุมแพลตฟอร์ม (ใช้ร่วมกับ Animator)")]
    public bool modeAnim = false;
    [Header("Destroy Settings")]
    public float timeDelay = 2f; // กำหนดแรงกระเด้ง

    [Header("Swing Settings")]
    public float swingSpeed = 2f;   // ความเร็วในการแกว่ง
    public float swingAngle = 30f;  // องศาสูงสุดที่จะแกว่งซ้าย-ขวา
    public float offset = 0f;       // ใช้เลื่อนจังหวะการแกว่ง (เผื่อมีหลายอันแล้วไม่อยากแกว่งพร้อมกัน)

    [Header("Bounce Settings")]
    public float bounceForce = 15f; // กำหนดแรงกระเด้ง

    [Header("Shooting Settings")]
    public Transform targetPoint;      // จุดที่อยากให้ Platform ยิงไปหา
    public float travelTime = 10f;     // แรงยิง
    public Transform spawnPoint;        // จุดเริ่มต้นที่จะ spawn ใหม่
    public GameObject platformPrefab;   // Prefab ของ Platform ตัวเอง
    private bool hasShot = false;      // เช็คว่ากด E ยิงไปแล้วหรือยัง
    // once triggered by collider, start moving and don't stop checking
    private bool triggeredLoop = false;

    public enum ShootStaggerMode { Simultaneous, RandomSpread, SiblingStagger, ManualStagger }
    [Tooltip("How to stagger initial shooting when multiple platforms trigger together")]
    public ShootStaggerMode shootStaggerMode = ShootStaggerMode.Simultaneous;
    [Tooltip("Interval used by SiblingStagger/ManualStagger (seconds between items)")]
    public float staggerInterval = 0.25f;
    [Tooltip("Max random delay (seconds) when using RandomSpread mode")]
    public float randomSpread = 1f;
    [Tooltip("Manual order index when using ManualStagger mode (0 = first)")]
    public int manualOrder = 0;

    private float startRotationZ;

    private Vector3 startPosition; // เก็บตำแหน่งเริ่มต้นของ platform
    [Header("Animation Settings")]
    public Animator anim;   

    private void Start()
    {
        startPosition = transform.position;
        transform.position = startPosition;
        startRotationZ = transform.eulerAngles.z;

    }

    private void Update()
    {
        // เงื่อนไขกด E ได้เฉพาะตอนผู้เล่นอยู่ในระยะ
        if (isPlayerNear && Input.GetKeyDown(KeyCode.E))
        {
            canMove = true;
            Debug.Log("Player pressed E near platform.");
        }

        if (modeLoop)
        {
            // If loopRequiresTrigger is true, require player presence or an explicit canMove flag to start moving
            // once triggeredLoop is true the platform will continue to move regardless of later exits
            if (!loopRequiresTrigger || isPlayerNear || canMove || triggeredLoop)
            {
                if (pointIndex < points.Length)
                {
                    transform.position = Vector2.MoveTowards(transform.position, points[pointIndex].position, moveSpeed * Time.deltaTime);

                    if (Vector2.Distance(transform.position, points[pointIndex].position) < 0.01f)
                    {
                        pointIndex += 1;
                    }

                    if (pointIndex == points.Length)
                    {
                        pointIndex = 0;
                    }
                }
            }
        }
        if (!modeLoop && !modeSwing)
        { 
                // For non-loop mode, move only when canMove OR when triggeredLoop (started by collider)
                if ((canMove || triggeredLoop) && pointIndex < points.Length)
                {

                    transform.position = Vector2.MoveTowards(transform.position, points[pointIndex].position, moveSpeed * Time.deltaTime);

                    if (Vector2.Distance(transform.position, points[pointIndex].position) < 0.01f)
                    {
                        pointIndex += 1;
                    }

                }
            
        }
        if (modeSwing)
        {
            // คำนวณมุมแกว่ง: Sin จะให้ค่า -1 ถึง 1
            float angle = Mathf.Sin((Time.time + offset) * swingSpeed) * swingAngle;

            // ใช้ Quaternion หมุนแกน Z
            transform.rotation = Quaternion.Euler(0f, 0f, startRotationZ + angle);
        }
        if(modeShoot)
        {
            if(!hasShot)
                StartCoroutine(StartShootingWithStagger());
        }
        if (modeAnim)
        {
            Rigidbody2D rb = GetComponent<Rigidbody2D>(); // ต้องมีบรรทัดนี้ก่อนใช้ rb
            anim.SetFloat("Speed", rb.linearVelocity.y); // ทำอะไรสักอย่างกับ Animator
        }
        // ถ้า canMove = true ถึงจะเริ่มเคลื่อนที่

    }
    //private void OnTriggerStay2D(Collider2D collision)
    //{
    //    if (collision.CompareTag("Player"))
    //    {

    //            canMove = true;
    //            Debug.Log("Pressed E while inside trigger.");

    //    }
    //}

    private IEnumerator ShootPlatformRoutine()
    {
        // สร้าง platform ใหม่
        GameObject newPlatform = Instantiate(platformPrefab, spawnPoint.position, spawnPoint.rotation);
        float elapsed = 0f;

        Vector3 startPos = spawnPoint.position;
        Vector3 endPos = targetPoint.position;

        while (elapsed < travelTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / travelTime);
            newPlatform.transform.position = Vector3.Lerp(startPos, endPos, t); // เคลื่อนจาก start → target
            yield return null;
        }

        // ถึงเป้าหมายแล้ว → ทำลาย
        Destroy(newPlatform);

        // รออีก 2 วินาทีแล้วรีเซ็ตยิงใหม่ได้
        yield return new WaitForSeconds(2f);
        hasShot = false;
    }

    // Wrapper: compute initial delay according to selected stagger mode, then start ShootPlatformRoutine
    private System.Collections.IEnumerator StartShootingWithStagger()
    {
        // mark scheduled so we don't schedule again
        hasShot = true;

        float delay = 0f;
        switch (shootStaggerMode)
        {
            case ShootStaggerMode.Simultaneous:
                delay = 0f;
                break;
            case ShootStaggerMode.RandomSpread:
                delay = Random.Range(0f, randomSpread);
                break;
            case ShootStaggerMode.SiblingStagger:
                // use sibling index to stagger items under same parent
                int idx = transform.GetSiblingIndex();
                delay = idx * staggerInterval;
                break;
            case ShootStaggerMode.ManualStagger:
                delay = manualOrder * staggerInterval;
                break;
        }

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        // Actually perform the shooting
        yield return StartCoroutine(ShootPlatformRoutine());
    }
    public void ShootToTarget()
    {
      
        
    }


    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerNear = true; // ผู้เล่นเข้ามาใกล้
            Debug.Log("Player is near platform.");


            // ถ้า Player มาชน
            if (modeJumped)
            {
                Rigidbody2D rb = collision.gameObject.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    // ล้างแรง Y เดิม เพื่อให้กระเด้งแน่นอน
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

                    // เพิ่มแรงกระเด้งขึ้นข้างบน
                    rb.AddForce(Vector2.up * bounceForce, ForceMode2D.Impulse);

                    Debug.Log("JUMPPP");
                }
            }
            // If loopRequiresTrigger is set, mark triggeredLoop so movement continues even after exit
            if (loopRequiresTrigger)
            {
                triggeredLoop = true;
                canMove = true;
            }
        }
        if (collision.gameObject.layer == LayerMask.NameToLayer("DeadZone"))
        {
            Destroy(gameObject,timeDelay);
            Debug.Log("Platform destroyed by DeadZone.");
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerNear = false; // ผู้เล่นออกไป
            Debug.Log("Player left platform area.");

            // do not clear triggeredLoop — once triggered we keep moving. Only clear isPlayerNear.
        }
    }



}
