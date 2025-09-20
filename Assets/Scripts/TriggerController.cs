using UnityEngine;

public class TriggerController : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject prefabToSpawn;
    public Transform spawnPoint;

    [Tooltip("ถ้า true = spawn ซ้ำเรื่อยๆ, ถ้า false = spawn แค่ครั้งเดียว")]
    public bool repeatSpawn = false;


    [Tooltip("เวลาห่างแต่ละครั้ง (เฉพาะโหมด spawn ซ้ำๆ)")]
    public float spawnInterval = 2f;

    private bool spawning = false;
    private bool trigger = true;
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

    private void SpawnObject()
    {
     
        
            Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);
            Debug.Log("Spawned prefab once.");
            
        
    }

    private System.Collections.IEnumerator SpawnRoutine()
    {
        spawning = true;
        while (repeatSpawn)
        {
            SpawnObject();
            yield return new WaitForSeconds(spawnInterval);
        }
        spawning = false;
    }
}
