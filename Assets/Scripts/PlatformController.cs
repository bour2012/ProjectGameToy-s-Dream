using UnityEngine;

public class PlatformController : MonoBehaviour
{

    public Animator platformAnimator; // ตัวควบคุมอนิเมชันของแพลตฟอร์ม
    public string openAnimationName = "PlatformOpen"; // ชื่ออนิเมชันเมื่อเปิด
    public string closeAnimationName = "PlatformClose"; // ชื่ออนิเมชันเมื่อปิด
    public string idleCloseAnimationName = "PlatformIdleClose";
    public string idleOpenAnimationName = "PlatformIdleOpen";
    private bool isActive = false; // สถานะของแพลตฟอร์ม (เปิด/ปิด)


    public Transform platform; // ตัวแพลตฟอร์มที่ต้องการควบคุม
    public Vector3 pivotPosition; // ตำแหน่งของจุดศูนย์กลางสำหรับการหมุน
    public Vector3 upPosition; // ตำแหน่งเมื่อเปิด
    public Vector3 downPosition; // ตำแหน่งเมื่อปิด
    public float speed = 2f; // ความเร็วในการเคลื่อนที่
    public float openRotationAngle = 0f; // องศาที่ต้องการหมุนเมื่อเปิด
    public float rotationSpeed = 50f; // ความเร็วในการหมุน (องศาต่อวินาที)
    public float startRotation = 0f; // องศาปัจจุบันของแพลตฟอร์ม
    public bool isRotationMode = false; // ตัวเลือก: true = หมุน, false = ย้ายตำแหน่ง
    public bool isAnimMode = false; // ตัวเลือก: true = หมุน, false = ย้ายตำแหน่ง

    //private bool isActive = false;
    private float currentRotation = 0f; // องศาปัจจุบันของแพลตฟอร์ม

    void Start()
    {
        if (isRotationMode)
        {
            startRotation = platform.eulerAngles.z;
            currentRotation = startRotation;
        }
        else
        {
            // ถ้าไม่ได้ตั้งค่า downPosition ใน Inspector ให้ใช้ตำแหน่งปัจจุบัน
            if (Mathf.Approximately(downPosition.x, 0f) && Mathf.Approximately(downPosition.y, 0f) && Mathf.Approximately(downPosition.z, 0f))
            {
                downPosition = platform.position;
            }
        }
    }
    public void Toggle(bool state)
    {
        //if (isActive == state) return; // ถ้าสถานะไม่เปลี่ยนแปลง ไม่ต้องทำอะไร

        isActive = state;

        if (isAnimMode)
        {
            if (platformAnimator != null)
            {
                if (isActive)
                {
                    // เล่นอนิเมชันเปิด
                    platformAnimator.Play(openAnimationName);
                }
                else
                {
                    // เล่นอนิเมชันปิด
                    platformAnimator.Play(closeAnimationName);
                }
            }
            else
            {
                Debug.LogWarning("Platform Animator is not assigned!");
            }
        }
    }

    //    }


    public void PlayIdleOpenAnimation()
    {
        if (platformAnimator != null)
        {
            platformAnimator.Play(idleOpenAnimationName);
        }
    }

    public void PlayIdleCloseAnimation()
    {
        if (platformAnimator != null)
        {
            platformAnimator.Play(idleCloseAnimationName);
        }
    }

    //public void Toggle(bool state)
    //{
    //    isActive = state;
    //}

    void Update()
    {
        if (isRotationMode)
        {
            // หมุนแพลตฟอร์ม
            RotatePlatform();
        }
        else if(!isAnimMode)
        {
            // ย้ายตำแหน่งแพลตฟอร์ม
            MovePlatform();
        }
    }

    private void MovePlatform()
    {
        // เคลื่อนที่แพลตฟอร์มไปยังตำแหน่งเป้าหมาย
        Vector3 target = isActive ? upPosition : downPosition;
        platform.position = Vector3.MoveTowards(platform.position, target, speed * Time.deltaTime);
    }

    private void RotatePlatform()
    {
        // คำนวณองศาเป้าหมาย
        float targetRotation = isActive ? openRotationAngle : startRotation;

        // หมุนแพลตฟอร์มทีละน้อยจนถึงองศาเป้าหมาย
        currentRotation = Mathf.MoveTowards(currentRotation, targetRotation, rotationSpeed * Time.deltaTime);

        // หมุนแพลตฟอร์มรอบจุดศูนย์กลางที่กำหนด (pivotPosition)
        platform.RotateAround(pivotPosition, Vector3.forward, currentRotation - platform.eulerAngles.z);
    }
}

