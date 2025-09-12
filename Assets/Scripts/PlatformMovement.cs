using UnityEngine;

public class PlatformMovement : MonoBehaviour
{
    public Transform[] points;
    public float moveSpeed;
    private int pointIndex;
    private bool canMove = false; // ตัวแปรเช็คว่ากด E หรือยัง

    private void Start()
    {
        transform.position = points[pointIndex].position;
    }

    private void Update()
    {
        // ตรวจสอบการกดปุ่ม E
        if (Input.GetKeyDown(KeyCode.E))
        {
            canMove = true;
        }

        // ถ้า canMove = true ถึงจะเริ่มเคลื่อนที่
        if (canMove && pointIndex < points.Length)
        {
            transform.position = Vector2.MoveTowards(transform.position, points[pointIndex].position, moveSpeed * Time.deltaTime);

            if (Vector2.Distance(transform.position, points[pointIndex].position) < 0.01f)
            {
                pointIndex += 1;
            }
        }
    }
}
