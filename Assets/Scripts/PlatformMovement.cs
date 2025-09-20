using UnityEngine;

public class PlatformMovement : MonoBehaviour
{
    public Transform[] points;
    public float moveSpeed;
    private int pointIndex;
    private bool canMove = false; // ตัวแปรเช็คว่ากด E หรือยัง
    private bool isPlayerNear = false; // ผู้เล่นอยู่ใกล้หรือไม่
    public bool modeLoop = false;

    private Vector3 startPosition; // เก็บตำแหน่งเริ่มต้นของ platform

    private void Start()
    {
        startPosition = transform.position;
        transform.position = startPosition;
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
        else
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
