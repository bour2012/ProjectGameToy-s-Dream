using UnityEngine;

public class PlatformController : MonoBehaviour
{
    public Animator platformAnimator;
    public string openAnimationName = "PlatformOpen";
    public string closeAnimationName = "PlatformClose";
    public string idleCloseAnimationName = "PlatformIdleClose";
    public string idleOpenAnimationName = "PlatformIdleOpen";
    private bool isActive = false;

    public Transform platform;
    private Rigidbody2D platformRb2D;
    private RigidbodyConstraints2D defaultConstraints;
    public Vector3 pivotPosition;
    public Vector3 upPosition;
    public Vector3 downPosition;
    public float speed = 2f;
    public float openRotationAngle = 0f;
    public float rotationSpeed = 50f;
    public float startRotation = 0f;
    public bool isRotationMode = false;
    public bool isAnimMode = false;

    [Header("Lock Settings")]
    public bool isLocked = false;
    public string requiredKeyID;

    private float currentRotation = 0f;

    void Start()
    {
        // เพิ่มการดึง Rigidbody2D component
        platformRb2D = platform.GetComponent<Rigidbody2D>();
        if (platformRb2D == null)
        {
            Debug.LogError("Platform ต้องมี Rigidbody2D component!");
            return;
        }
        defaultConstraints = platformRb2D.constraints; // เก็บค่าสำรองไว้
        FreezePlatform();

        if (isRotationMode)
        {
            startRotation = platform.eulerAngles.z;
            currentRotation = startRotation;
        }
        else
        {
            if (Mathf.Approximately(downPosition.x, 0f) &&
                Mathf.Approximately(downPosition.y, 0f) &&
                Mathf.Approximately(downPosition.z, 0f))
            {
                downPosition = platform.position;
            }
        }
    }

    private void OnEnable()
    {
        ItemManager.OnKeyCollected += OnKeyCollectedHandler;
    }

    private void OnDisable()
    {
        ItemManager.OnKeyCollected -= OnKeyCollectedHandler;
    }
    void FreezePlatform()
    {
        // ล็อกแกน X, Y, Z (Platform ห้ามขยับ)
        platformRb2D.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    void UnfreezePlatform()
    {
        // ปลดล็อก X, Y, Rotation Z (Platform ขยับได้)
        platformRb2D.constraints = defaultConstraints;
    }

    bool IsPlatformFrozen()
    {
        // ตรวจสอบว่า Platform ถูกล็อกอยู่หรือไม่
        return (platformRb2D.constraints == RigidbodyConstraints2D.FreezeAll);
    }
    private void OnKeyCollectedHandler(string collectedKeyID)
    {
        if (isLocked && collectedKeyID == requiredKeyID)
        {
            UnlockAndActivate();
        }
    }

    private void UnlockAndActivate()
    {
        isLocked = false;
        Debug.Log($"Platform '{gameObject.name}' was automatically unlocked by key: {requiredKeyID}");
        Toggle(true);
    }

    public void Toggle(bool state)
    {
       


        if (isLocked)
        {
            Debug.Log($"Platform '{gameObject.name}' is locked! Need key: {requiredKeyID}");
            return;
        }


        isActive = state;

        if (isAnimMode && platformAnimator != null)
        {
            if (isActive)
            {
                platformAnimator.Play(openAnimationName);
            }
            else
            {
                platformAnimator.Play(closeAnimationName);
            }
        }

        if (isActive)
        {
            // ปลดล็อก ให้ขยับได้
            UnfreezePlatform();
        }
        else
        {
            // ถ้า toggle ปิด ให้ขยับลงแล้ว freeze เมื่อถึงจุดหมาย
            UnfreezePlatform();
        }
    }

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

    // เปลี่ยนจาก Update() เป็น FixedUpdate() เพื่อใช้กับฟิสิกส์
    void FixedUpdate()
    {
        if (platformRb2D == null) return;

        if (isRotationMode)
        {
            RotatePlatform();
        }
        else if (!isAnimMode)
        {
            MovePlatform();
        }
    }

    private void MovePlatform()
    {
        Vector2 current = platformRb2D.position;
        Vector2 target = isActive ? new Vector2(upPosition.x, upPosition.y) : new Vector2(downPosition.x, downPosition.y);
        Vector2 newPosition = Vector2.MoveTowards(current, target, speed * Time.deltaTime);

        // ถ้าใกล้ถึงเป้าหมาย (ใช้ threshold ระยะ < 0.02f)
        if (Vector2.Distance(newPosition, target) < 0.02f)
        {
            newPosition = target; // snap ตำแหน่งให้ตรง
        }

        platformRb2D.MovePosition(newPosition);

        if (!isActive && Vector3.Distance(newPosition, downPosition) < 0.01f)
        {
            // Freeze เมื่อถึงตำแหน่งเริ่มต้น (downPosition)
            FreezePlatform();
        }
    }

    private void RotatePlatform()
    {
        float targetRotation = isActive ? openRotationAngle : startRotation;
        currentRotation = Mathf.MoveTowards(currentRotation, targetRotation, rotationSpeed * Time.deltaTime);
        // Platform 2D ควรหมุนรอบ Z
    
        //platformRb2D.MoveRotation(pivotPosition, Vector3.forward, currentRotation - platform.eulerAngles.z);
        platform.RotateAround(pivotPosition, Vector3.forward, currentRotation - platform.eulerAngles.z);
    }
}
