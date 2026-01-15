using UnityEngine;
using System.Collections; // จำเป็นสำหรับการใช้ IEnumerator

public class TriggerController : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject prefabToSpawn;
    public Transform spawnPoint;

    [Tooltip("ถ้า true = spawn ซ้ำเรื่อยๆ, ถ้า false = spawn แค่ครั้งเดียว")]
    public bool repeatSpawn = false;

    [Header("Repeat Mode Settings")]
    [Tooltip("ถ้า true = จะรอให้ตัวเก่าถูกทำลายก่อนค่อยเสกตัวใหม่ (ไม่สนเวลา Interval)")]
    public bool spawnWaitDestruction = false;

    [Tooltip("เวลาห่างแต่ละครั้ง (ใช้เฉพาะเมื่อ spawnWaitDestruction = false)")]
    public float spawnInterval = 2f;

    // สถานะภายใน
    private bool spawning = false;
    private bool trigger = true;
    private GameObject currentSpawnedObject; // ตัวแปรสำหรับจำตัวที่เสกออกมาล่าสุด

    public float destroyDelay = 5f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (prefabToSpawn == null || spawnPoint == null) return;

            if (repeatSpawn)
            {
                if (!spawning)
                    StartCoroutine(SpawnRoutine());
            }
            else
            {
                if (trigger)
                {
                    SpawnObject(); // spawn ครั้งเดียว
                    trigger = false;
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (repeatSpawn)
            {
                StopAllCoroutines();
                spawning = false;
            }
        }
    }

    private void SpawnObject()
    {
        // เก็บ Object ที่เสกออกมาไว้ในตัวแปร currentSpawnedObject
        currentSpawnedObject = Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);

        Debug.Log("Spawned prefab.");

        // ถ้าต้องการให้มันทำลายตัวเองอัตโนมัติตามเวลาด้วย (เผื่อกรณีไม่ได้ถูกผู้เล่นทำลาย) ก็เปิดบรรทัดนี้ได้
        // Destroy(currentSpawnedObject, destroyDelay); 
    }

    private IEnumerator SpawnRoutine()
    {
        spawning = true;
        while (repeatSpawn)
        {
            // 1. ตรวจสอบเงื่อนไขก่อน Spawn (เฉพาะรอบที่ไม่ใช่รอบแรก)
            if (spawnWaitDestruction && currentSpawnedObject != null)
            {
                // ถ้ายืนยันจะรอของเก่าตาย และของเก่ายังอยู่ -> ให้รอจนกว่ามันจะหายไป (เป็น null)
                yield return new WaitUntil(() => currentSpawnedObject == null);
            }

            // 2. ทำการ Spawn
            SpawnObject();

            // 3. การรอหลัง Spawn
            if (spawnWaitDestruction)
            {
                // โหมดใหม่: รอจนกว่าตัวปัจจุบันจะหายไป (เป็น null)
                yield return new WaitUntil(() => currentSpawnedObject == null);

                // (Optional) เพิ่มดีเลย์นิดหน่อยหลังจากตายแล้วค่อยเกิดใหม่ไหม? ถ้าไม่เอาก็เอาบรรทัดล่างนี้ออก
                yield return new WaitForSeconds(0.1f);
            }
            else
            {
                // โหมดเดิม: รอตามเวลา
                yield return new WaitForSeconds(spawnInterval);
            }
        }
        spawning = false;
    }
}