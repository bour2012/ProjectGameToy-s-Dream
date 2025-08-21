using UnityEngine;

public class Lever : MonoBehaviour
{
    [Header("Lever Settings")]
    public Transform handle;         // ส่วนคันโยกที่จะหมุน
    public float activeRotation = 90f;
    public float inactiveRotation = 45f;
    public bool isActive = false;
    public PlatformController platform;  // อ้างอิง Platform ที่จะถูกควบคุม

    [Header("Player Detection")]
    public float interactionRange = 2f;  // ระยะที่สามารถใช้งานได้
    public LayerMask playerLayer = 1024; // Layer ของ Player (default: 10)
    public bool showDebugRange = true;   // แสดง Debug Range

    [Header("UI Feedback (Optional)")]
    public GameObject interactionUI;     // UI แสดงเมื่ออยู่ใกล้ (เช่น "Press E")

    private Transform playerTransform;
    private bool playerInRange = false;

    void Start()
    {
        // หา Player GameObject
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogWarning("ไม่พบ Player GameObject! กรุณาตั้ง Tag 'Player' ให้กับผู้เล่น");
        }

        // ซ่อน UI ตอนเริ่มต้น
        if (interactionUI != null)
        {
            interactionUI.SetActive(false);
        }
    }

    void Update()
    {
        CheckPlayerDistance();

        if (Input.GetKeyDown(KeyCode.E) && playerInRange)
        {
            ToggleLever();
        }
    }

    void CheckPlayerDistance()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        bool wasInRange = playerInRange;

        playerInRange = distanceToPlayer <= interactionRange;

        // แสดง/ซ่อน UI เมื่อเข้า/ออกจากระยะ
        if (playerInRange != wasInRange)
        {
            if (interactionUI != null)
            {
                interactionUI.SetActive(playerInRange);
            }

            // Debug สำหรับการเข้า/ออกจากระยะ
            if (playerInRange)
            {
                Debug.Log($"ผู้เล่นเข้าใกล้ Lever - กด E เพื่อใช้งาน");
            }
            else
            {
                Debug.Log($"ผู้เล่นออกจากระยะ Lever");
            }
        }
    }

    void ToggleLever()
    {
        isActive = !isActive;

        // หมุนคันโยกแบบ Smooth (ถ้าต้องการ)
        float targetRot = isActive ? activeRotation : inactiveRotation;
        handle.localRotation = Quaternion.Euler(0, 0, targetRot);

        // เรียกให้ Platform ทำงาน
        if (platform != null)
        {
            platform.Toggle(isActive);
        }

        Debug.Log($"Lever {(isActive ? "เปิด" : "ปิด")}");
    }

    // วิธีทางเลือก: ใช้ Trigger แทนการตรวจระยะ
    void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            playerInRange = true;
            if (interactionUI != null)
            {
                interactionUI.SetActive(true);
            }
            Debug.Log("ผู้เล่นเข้าใกล้ Lever - กด E เพื่อใช้งาน");
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            playerInRange = false;
            if (interactionUI != null)
            {
                interactionUI.SetActive(false);
            }
            Debug.Log("ผู้เล่นออกจากระยะ Lever");
        }
    }

    // แสดง Debug Range ใน Scene View
    void OnDrawGizmosSelected()
    {
        if (showDebugRange)
        {
            Gizmos.color = playerInRange ? Color.green : Color.yellow;

            // วาดวงกลมด้วยการใช้เส้น
            DrawWireCircle(transform.position, interactionRange);

            // แสดงเส้นไปยังผู้เล่น (ถ้าพบ)
            if (playerTransform != null)
            {
                Gizmos.color = playerInRange ? Color.green : Color.red;
                Gizmos.DrawLine(transform.position, playerTransform.position);
            }
        }
    }

    // ฟังก์ชันวาดวงกลมเอง
    void DrawWireCircle(Vector3 center, float radius)
    {
        int segments = 32;
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }

    // ฟังก์ชันสำหรับเรียกจากภายนอก
    public bool CanInteract()
    {
        return playerInRange;
    }

    public void ForceToggle()
    {
        ToggleLever();
    }
}