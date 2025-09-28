using UnityEngine;

public class PlatformMovement : MonoBehaviour
{
    public Transform[] points;
    public float moveSpeed;
    private int pointIndex;
    private bool canMove = false; // ตัวแปรเช็คว่ากด E หรือยัง
    private bool isPlayerNear = false; // ผู้เล่นอยู่ใกล้หรือไม่
    public bool modeLoop = false;
    public bool modeSwing = false;
    public bool modeJumped = false;
    public bool modeDestroyed = false;
    [Header("Destroy Settings")]
    public float timeDelay = 2f; // กำหนดแรงกระเด้ง

    [Header("Swing Settings")]
    public float swingSpeed = 2f;   // ความเร็วในการแกว่ง
    public float swingAngle = 30f;  // องศาสูงสุดที่จะแกว่งซ้าย-ขวา
    public float offset = 0f;       // ใช้เลื่อนจังหวะการแกว่ง (เผื่อมีหลายอันแล้วไม่อยากแกว่งพร้อมกัน)

    [Header("Bounce Settings")]
    public float bounceForce = 15f; // กำหนดแรงกระเด้ง


    private float startRotationZ;

    private Vector3 startPosition; // เก็บตำแหน่งเริ่มต้นของ platform

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
            if ( pointIndex < points.Length)
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
        if (!modeLoop && !modeSwing)
        { 
                if (canMove && pointIndex < points.Length)
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
        }
    }
}
