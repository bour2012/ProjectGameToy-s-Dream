using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LaserEmitter : MonoBehaviour
{
    [Header("Laser Settings")]
    public LayerMask collisionLayers;
    public LayerMask damageLayers;
    public float laserMaxLength = 100f;
    public float laserWidth = 0.1f;

    [Header("Damage Settings")]
    public PlayerDeathSystem.DeathType deathType = PlayerDeathSystem.DeathType.InstantDeath;

    // ==========================================
    // ส่วนตั้งค่า VFX (ตามคลิป Tutorial)
    // ==========================================
    [Header("VFX Settings")]
    public GameObject startVFX; // ลาก GameObject "StartVFX" มาใส่
    public GameObject endVFX;   // ลาก GameObject "EndVFX" มาใส่

    private LineRenderer lineRenderer;
    private List<ParticleSystem> particles = new List<ParticleSystem>(); // เก็บ Particle ทุกตัว
    private bool isLaserActive = true; // สถานะเลเซอร์ (เปิด/ปิด)

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 2;

        // ค้นหา Particle ทั้งหมดในลูกๆ ของ StartVFX และ EndVFX มาเก็บไว้ (เหมือนในคลิป)
        FillLists();
    }

    // ฟังก์ชันรวบรวม Particle System ทั้งหมดเหมือนในคลิป
    void FillLists()
    {
        if (startVFX != null)
        {
            // GetComponentsInChildren จะดึง Particle ของลูกๆ ทั้งหมด (เช่น Particles, Beam)
            particles.AddRange(startVFX.GetComponentsInChildren<ParticleSystem>());
        }

        if (endVFX != null)
        {
            particles.AddRange(endVFX.GetComponentsInChildren<ParticleSystem>());
        }
    }

    // ฟังก์ชันสำหรับเปิดเลเซอร์
    public void EnableLaser()
    {
        lineRenderer.enabled = true;
        isLaserActive = true;

        // สั่งให้ Particle ทุกตัวเล่น
        foreach (var ps in particles)
        {
            ps.Play();
        }
    }

    // ฟังก์ชันสำหรับปิดเลเซอร์
    public void DisableLaser()
    {
        lineRenderer.enabled = false;
        isLaserActive = false;

        // สั่งให้ Particle ทุกตัวหยุด
        foreach (var ps in particles)
        {
            ps.Stop();
        }
    }

    void Update()
    {
        // ถ้าเลเซอร์ปิดอยู่ ไม่ต้องทำอะไรเลย
        if (!isLaserActive) return;

        Vector2 startPos = transform.position;
        Vector2 direction = transform.up;
        Vector2 visualEndPoint;

        RaycastHit2D visualHit = Physics2D.Raycast(startPos, direction, laserMaxLength, collisionLayers);

        // 1. ให้ StartVFX อยู่ที่จุดเริ่มต้นปืนเสมอ
        if (startVFX != null)
        {
            startVFX.transform.position = startPos;
        }

        if (visualHit.collider != null)
        {
            visualEndPoint = visualHit.point;

            // 2. ให้ EndVFX เลื่อนไปอยู่ที่จุดชนกำแพง
            if (endVFX != null)
            {
                endVFX.transform.position = visualEndPoint;
            }
        }
        else
        {
            visualEndPoint = startPos + (direction * laserMaxLength);

            // ถ้าไม่ชนกำแพง ก็ให้จุดปลายไปอยู่สุดระยะปืน
            if (endVFX != null)
            {
                endVFX.transform.position = visualEndPoint;
            }
        }

        // 3. วาดเส้น LineRenderer
        lineRenderer.startWidth = laserWidth;
        lineRenderer.endWidth = laserWidth;
        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, visualEndPoint);

        // 4. สร้างดาเมจและทำลายกล่อง
        PerformDamageRaycast(startPos, direction, Vector2.Distance(startPos, visualEndPoint));
    }

    private void PerformDamageRaycast(Vector2 start, Vector2 direction, float distance)
    {
        RaycastHit2D[] hits = Physics2D.CapsuleCastAll(start, new Vector2(laserWidth, laserWidth), CapsuleDirection2D.Vertical, 0f, direction, distance, damageLayers);

        foreach (var hit in hits)
        {
            if (hit.collider.CompareTag("Player"))
            {
                PlayerDeathSystem deathScript = hit.collider.GetComponent<PlayerDeathSystem>();
                if (deathScript != null) deathScript.Die(deathType);
            }
            else
            {
                CraftedObject craftedObj = hit.collider.GetComponent<CraftedObject>();
                if (craftedObj != null) craftedObj.ReturnToTrash();
                else Destroy(hit.collider.gameObject);
            }
        }
    }
}