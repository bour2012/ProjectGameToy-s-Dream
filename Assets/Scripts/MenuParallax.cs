using UnityEngine;

public class MenuParallax : MonoBehaviour
{
    public float offsetMultiplier = 1f;
    public float smoothTime = .3f;

    private Vector2 startPosition;
    private Vector3 velocity;
    void Start()
    {
        // เปลี่ยนเป็น localPosition เพื่อจำตำแหน่งเทียบกับ Parent
        startPosition = transform.localPosition;
    }

    void Update()
    {
        Vector2 offset = Camera.main.ScreenToViewportPoint(Input.mousePosition);

        // คำนวณ offset โดยให้ (0.5, 0.5) คือจุดกึ่งกลาง (เมาส์อยู่กลางจอ = ไม่ขยับ)
        Vector2 centeredOffset = offset - new Vector2(0.5f, 0.5f);

        // เป้าหมายคือ ตำแหน่ง local เดิม + การขยับตามเมาส์
        Vector3 targetPosition = startPosition + (centeredOffset * offsetMultiplier);

        // ใช้ localPosition ในการขยับ
        transform.localPosition = Vector3.SmoothDamp(transform.localPosition, targetPosition, ref velocity, smoothTime);
    }
    //void Start()
    //{
    //    startPosition = transform.position;
    //}


    //// Update is called once per frame
    //void Update()
    //{
    //    Vector2 offset = Camera.main.ScreenToViewportPoint(Input.mousePosition);
    //    transform.position = Vector3.SmoothDamp(transform.position, startPosition + (offset * offsetMultiplier), ref velocity, smoothTime);
    //}
}
