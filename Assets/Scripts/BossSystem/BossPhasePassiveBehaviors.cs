using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ========================================
// Phase Behavior System - Wave Limit Version
// เพิ่มระบบควบคุม Wave สำหรับการ Spawn ศัตรู
// ========================================

/// <summary>
/// ระบบจัดการ Passive Behaviors ของแต่ละ Phase (รองรับ Wave Limit)
/// </summary>
public class BossPhasePassiveBehaviors : MonoBehaviour
{
    [System.Serializable]
    public class PhasePassiveAbility
    {
        [Header("Phase Info")]
        public string abilityName = "Falling Rocks";
        public int phaseIndex = 1; // ใช้ใน Phase ไหน

        [Header("Ability Type")]
        public PassiveAbilityType abilityType;

        [Header("Spawn Settings")]
        public GameObject spawnPrefab;
        public float spawnInterval = 3f;
        public int spawnCount = 1;

        [Header("Wave Limit Settings (สำหรับ Minion/Obstacle)")]
        public bool useWaveLimit = false; // เปิด/ปิด wave limit
        public int maxWaves = 3; // จำนวน wave สูงสุด (0 = ไม่จำกัด)
        public int enemiesPerWave = 5; // จำนวนศัตรูต่อ wave
        public bool waitForWaveComplete = true; // รอให้ wave ปัจจุบันตายหมดก่อน spawn wave ใหม่

        [Header("Spawn Area")]
        public SpawnAreaType areaType = SpawnAreaType.AbovePlayer;
        public Vector2 spawnOffset = Vector2.zero;
        public float spawnHeight = 10f; // ความสูงเหนือเป้าหมาย
        public float randomRadius = 3f; // รัศมีการสุ่ม

        [Header("Spawn Pattern")]
        public SpawnPattern pattern = SpawnPattern.Random;
        public float patternSpacing = 2f;

        [Header("Spawn Timing")]
        public bool sequentialSpawn = true; // spawn ทีละตัว
        public float spawnDelay = 0.2f; // ดีเลย์ระหว่างการ spawn แต่ละตัว

        [Header("Object Lifetime")]
        public bool autoDestroy = true;
        public float destroyAfter = 5f; // ทำลายหลังกี่วินาที

        [Header("Advanced Settings")]
        public bool onlyWhenGrounded = false;
        public bool onlyWhenFlying = false;
        public float startDelay = 0f;
        public int maxActiveCount = -1; // -1 = ไม่จำกัด (ไม่ใช้กับ wave mode)

        [Header("Visual Effects")]
        public GameObject warningEffectPrefab;
        public float warningDuration = 0.5f;
        public Color warningColor = Color.red;

        [Header("Audio")]
        public AudioClip spawnSound;
        [Range(0f, 1f)]
        public float soundVolume = 1f;

        // Runtime wave tracking (internal)
        [HideInInspector] public int currentWave = 0;
        [HideInInspector] public List<GameObject> currentWaveObjects = new List<GameObject>();
    }

    public enum PassiveAbilityType
    {
        FallingHazard,      // ของตกจากบน (เช่น หิน)
        GroundHazard,       // อันตรายจากพื้น (เช่น หนาม)
        SpawnMinion,        // เสก minion
        CreateObstacle,     // สร้างสิ่งกีดขวาง
        PoisonCloud         // เมฆพิษ
    }

    public enum SpawnAreaType
    {
        AbovePlayer,        // เหนือผู้เล่น
        AroundPlayer,       // รอบๆ ผู้เล่น (แนวนอน)
        AroundBoss,         // รอบๆ Boss
        RandomInArena,      // สุ่มทั่วสนาม
        BehindPlayer        // ด้านหลังผู้เล่น
    }

    public enum SpawnPattern
    {
        Random,             // สุ่มตำแหน่ง
        Line,               // เรียงเป็นแนว
        Circle,             // วงกลม
        Grid                // ตาราง
    }

    [Header("Phase Abilities")]
    public PhasePassiveAbility[] phaseAbilities;

    [Header("Arena Bounds")]
    public Vector2 arenaMin = new Vector2(-20, -10);
    public Vector2 arenaMax = new Vector2(20, 10);

    [Header("References")]
    public BossController bossController;
    public Transform playerTransform;

    [Header("Debug")]
    public bool showDebugGizmos = true;
    public bool showDebugLogs = false;

    private Dictionary<int, List<Coroutine>> activeCoroutines = new Dictionary<int, List<Coroutine>>();
    private Dictionary<GameObject, int> spawnedObjects = new Dictionary<GameObject, int>();
    private int currentPhase = -1;

    void Start()
    {
        if (bossController == null)
        {
            bossController = GetComponent<BossController>();
        }

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
    }

    void Update()
    {
        // ตรวจสอบว่า Phase เปลี่ยนหรือไม่
        if (bossController != null && bossController.currentPhaseIndex != currentPhase)
        {
            OnPhaseChanged(bossController.currentPhaseIndex);
        }

        // ทำความสะอาด spawned objects ที่โดนทำลายแล้ว
        CleanupDestroyedObjects();
    }

    void OnPhaseChanged(int newPhase)
    {
        if (showDebugLogs)
            Debug.Log($"[PhasePassive] Phase changed to {newPhase}");

        // หยุด abilities ทั้งหมดของ phase เก่า
        StopAllPhaseAbilities(currentPhase);

        currentPhase = newPhase;

        // Reset wave counters
        foreach (var ability in phaseAbilities)
        {
            if (ability.phaseIndex == newPhase)
            {
                ability.currentWave = 0;
                ability.currentWaveObjects.Clear();
            }
        }

        // เริ่ม abilities ของ phase ใหม่
        StartPhaseAbilities(newPhase);
    }

    void StartPhaseAbilities(int phaseIndex)
    {
        if (phaseAbilities == null || phaseAbilities.Length == 0) return;

        foreach (var ability in phaseAbilities)
        {
            if (ability.phaseIndex == phaseIndex && ability.spawnPrefab != null)
            {
                Coroutine coroutine = StartCoroutine(PassiveAbilityRoutine(ability));

                if (!activeCoroutines.ContainsKey(phaseIndex))
                {
                    activeCoroutines[phaseIndex] = new List<Coroutine>();
                }
                activeCoroutines[phaseIndex].Add(coroutine);

                if (showDebugLogs)
                    Debug.Log($"[PhasePassive] Started ability: {ability.abilityName}");
            }
        }
    }

    void StopAllPhaseAbilities(int phaseIndex)
    {
        if (activeCoroutines.ContainsKey(phaseIndex))
        {
            foreach (var coroutine in activeCoroutines[phaseIndex])
            {
                if (coroutine != null)
                {
                    StopCoroutine(coroutine);
                }
            }
            activeCoroutines[phaseIndex].Clear();
        }
    }

    IEnumerator PassiveAbilityRoutine(PhasePassiveAbility ability)
    {
        // Start delay
        if (ability.startDelay > 0)
        {
            yield return new WaitForSeconds(ability.startDelay);
        }

        // ถ้าใช้ wave limit mode
        if (ability.useWaveLimit)
        {
            yield return StartCoroutine(WaveLimitRoutine(ability));
        }
        else
        {
            // โหมดปกติ (ไม่มี wave limit)
            yield return StartCoroutine(NormalRoutine(ability));
        }
    }

    /// <summary>
    /// โหมด Wave Limit - Spawn ตาม wave ที่กำหนด รอให้ตายหมดก่อน spawn wave ใหม่
    /// </summary>
    IEnumerator WaveLimitRoutine(PhasePassiveAbility ability)
    {
        while (true)
        {
            // ตรวจสอบว่าครบจำนวน wave สูงสุดหรือยัง
            if (ability.maxWaves > 0 && ability.currentWave >= ability.maxWaves)
            {
                if (showDebugLogs)
                    Debug.Log($"[PhasePassive] {ability.abilityName} reached max waves ({ability.maxWaves})");
                yield break; // หยุดการ spawn
            }

            // ตรวจสอบเงื่อนไข
            if (!ShouldActivateAbility(ability))
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // ถ้าต้องรอให้ wave ปัจจุบันตายหมดก่อน
            if (ability.waitForWaveComplete && ability.currentWave > 0)
            {
                // รอให้ศัตรูใน wave ปัจจุบันตายหมด
                yield return new WaitUntil(() => IsWaveComplete(ability));

                if (showDebugLogs)
                    Debug.Log($"[PhasePassive] {ability.abilityName} wave {ability.currentWave} completed!");

                // รอ interval ก่อน spawn wave ใหม่
                yield return new WaitForSeconds(ability.spawnInterval);
            }

            // Spawn wave ใหม่
            ability.currentWave++;
            ability.currentWaveObjects.Clear();

            if (showDebugLogs)
                Debug.Log($"[PhasePassive] {ability.abilityName} spawning wave {ability.currentWave}/{ability.maxWaves}");

            // Override spawnCount ด้วย enemiesPerWave
            int originalSpawnCount = ability.spawnCount;
            ability.spawnCount = ability.enemiesPerWave;

            // Execute spawn
            yield return StartCoroutine(ExecuteAbility(ability));

            // Restore original spawnCount
            ability.spawnCount = originalSpawnCount;

            // ถ้าไม่ต้องรอให้ wave เสร็จ ให้รอ interval แทน
            if (!ability.waitForWaveComplete)
            {
                yield return new WaitForSeconds(ability.spawnInterval);
            }
        }
    }

    /// <summary>
    /// โหมดปกติ - Spawn ตามเวลาปกติ
    /// </summary>
    IEnumerator NormalRoutine(PhasePassiveAbility ability)
    {
        while (true)
        {
            // ตรวจสอบเงื่อนไข
            if (ShouldActivateAbility(ability))
            {
                // ตรวจสอบ max active count
                if (ability.maxActiveCount > 0)
                {
                    int activeCount = GetActiveSpawnCount(ability.phaseIndex);
                    if (activeCount >= ability.maxActiveCount)
                    {
                        yield return new WaitForSeconds(0.5f);
                        continue;
                    }
                }

                // ทำ ability
                yield return StartCoroutine(ExecuteAbility(ability));
            }

            yield return new WaitForSeconds(ability.spawnInterval);
        }
    }

    /// <summary>
    /// ตรวจสอบว่า wave ปัจจุบันเสร็จหรือยัง (ศัตรูตายหมดแล้ว)
    /// </summary>
    bool IsWaveComplete(PhasePassiveAbility ability)
    {
        // ลบ objects ที่เป็น null ออก
        ability.currentWaveObjects.RemoveAll(obj => obj == null);

        // ถ้าไม่มีอะไรเหลือแล้ว = wave เสร็จ
        return ability.currentWaveObjects.Count == 0;
    }

    bool ShouldActivateAbility(PhasePassiveAbility ability)
    {
        // ตรวจสอบ Boss state
        if (bossController != null)
        {
            if (ability.onlyWhenGrounded && bossController.isCurrentlyFalling)
                return false;

            if (ability.onlyWhenFlying && !bossController.isCurrentlyFalling)
                return false;
        }

        return true;
    }

    IEnumerator ExecuteAbility(PhasePassiveAbility ability)
    {
        // คำนวณตำแหน่งที่จะ spawn
        List<Vector2> spawnPositions = CalculateSpawnPositions(ability);

        if (ability.sequentialSpawn)
        {
            // Spawn ทีละตัวแบบมีดีเลย์
            yield return StartCoroutine(SpawnSequentially(ability, spawnPositions));
        }
        else
        {
            // Spawn พร้อมกันทั้งหมด
            yield return StartCoroutine(SpawnSimultaneously(ability, spawnPositions));
        }
    }

    IEnumerator SpawnSequentially(PhasePassiveAbility ability, List<Vector2> spawnPositions)
    {
        for (int i = 0; i < spawnPositions.Count; i++)
        {
            Vector2 pos = spawnPositions[i];

            // แสดง warning
            if (ability.warningEffectPrefab != null && ability.warningDuration > 0)
            {
                GameObject warning = Instantiate(ability.warningEffectPrefab, pos, Quaternion.identity);
                Destroy(warning, ability.warningDuration);
            }
            else if (ability.warningDuration > 0)
            {
                CreateSimpleWarning(pos, ability.warningDuration, ability.warningColor);
            }

            // รอให้ warning เสร็จ
            if (ability.warningDuration > 0)
            {
                yield return new WaitForSeconds(ability.warningDuration);
            }

            // Spawn object
            GameObject spawned = Instantiate(ability.spawnPrefab, pos, Quaternion.identity);

            // Track spawned object
            spawnedObjects[spawned] = ability.phaseIndex;

            // ถ้าใช้ wave mode ให้เพิ่มเข้า currentWaveObjects
            if (ability.useWaveLimit)
            {
                ability.currentWaveObjects.Add(spawned);
            }

            // Setup spawned object
            SetupSpawnedObject(spawned, ability);

            // Auto destroy (ไม่ใช้กับ minion ที่ต้องรอให้ตาย)
            if (ability.autoDestroy && ability.destroyAfter > 0 && ability.abilityType != PassiveAbilityType.SpawnMinion)
            {
                Destroy(spawned, ability.destroyAfter);
            }

            // Play sound
            if (ability.spawnSound != null)
            {
                AudioSource.PlayClipAtPoint(ability.spawnSound, transform.position, ability.soundVolume);
            }

            if (showDebugLogs)
                Debug.Log($"[PhasePassive] Spawned {ability.abilityName} at {pos} ({i + 1}/{spawnPositions.Count})");

            // ดีเลย์ก่อน spawn ตัวถัดไป (ยกเว้นตัวสุดท้าย)
            if (i < spawnPositions.Count - 1)
            {
                yield return new WaitForSeconds(ability.spawnDelay);
            }
        }
    }

    IEnumerator SpawnSimultaneously(PhasePassiveAbility ability, List<Vector2> spawnPositions)
    {
        // แสดง warning ทั้งหมดพร้อมกัน
        if (ability.warningEffectPrefab != null && ability.warningDuration > 0)
        {
            foreach (Vector2 pos in spawnPositions)
            {
                GameObject warning = Instantiate(ability.warningEffectPrefab, pos, Quaternion.identity);
                Destroy(warning, ability.warningDuration);
            }
            yield return new WaitForSeconds(ability.warningDuration);
        }
        else if (ability.warningDuration > 0)
        {
            foreach (Vector2 pos in spawnPositions)
            {
                CreateSimpleWarning(pos, ability.warningDuration, ability.warningColor);
            }
            yield return new WaitForSeconds(ability.warningDuration);
        }

        // Spawn objects ทั้งหมดพร้อมกัน
        foreach (Vector2 pos in spawnPositions)
        {
            GameObject spawned = Instantiate(ability.spawnPrefab, pos, Quaternion.identity);

            // Track spawned object
            spawnedObjects[spawned] = ability.phaseIndex;

            // ถ้าใช้ wave mode ให้เพิ่มเข้า currentWaveObjects
            if (ability.useWaveLimit)
            {
                ability.currentWaveObjects.Add(spawned);
            }

            // Setup spawned object
            SetupSpawnedObject(spawned, ability);

            // Auto destroy (ไม่ใช้กับ minion ที่ต้องรอให้ตาย)
            if (ability.autoDestroy && ability.destroyAfter > 0 && ability.abilityType != PassiveAbilityType.SpawnMinion)
            {
                Destroy(spawned, ability.destroyAfter);
            }

            if (showDebugLogs)
                Debug.Log($"[PhasePassive] Spawned {ability.abilityName} at {pos}");
        }

        // Play sound
        if (ability.spawnSound != null)
        {
            AudioSource.PlayClipAtPoint(ability.spawnSound, transform.position, ability.soundVolume);
        }
    }

    List<Vector2> CalculateSpawnPositions(PhasePassiveAbility ability)
    {
        List<Vector2> positions = new List<Vector2>();
        Vector2 basePosition = Vector2.zero;

        // กำหนดจุด base ตาม area type
        switch (ability.areaType)
        {
            case SpawnAreaType.AbovePlayer:
                if (playerTransform != null)
                {
                    basePosition = (Vector2)playerTransform.position + ability.spawnOffset;
                    basePosition.y += ability.spawnHeight;
                }
                else
                {
                    basePosition = (Vector2)transform.position + new Vector2(0, ability.spawnHeight);
                }
                break;

            case SpawnAreaType.AroundPlayer:
                if (playerTransform != null)
                {
                    basePosition = (Vector2)playerTransform.position + ability.spawnOffset;
                }
                else
                {
                    basePosition = (Vector2)transform.position;
                }
                break;

            case SpawnAreaType.AroundBoss:
                basePosition = (Vector2)transform.position + ability.spawnOffset;
                break;

            case SpawnAreaType.BehindPlayer:
                if (playerTransform != null)
                {
                    float direction = playerTransform.localScale.x > 0 ? -1 : 1;
                    basePosition = (Vector2)playerTransform.position + new Vector2(direction * 3f, 0) + ability.spawnOffset;
                }
                else
                {
                    basePosition = (Vector2)transform.position;
                }
                break;

            case SpawnAreaType.RandomInArena:
                basePosition = GetRandomArenaPosition();
                break;
        }

        // สร้างตำแหน่งตาม pattern
        switch (ability.pattern)
        {
            case SpawnPattern.Random:
                for (int i = 0; i < ability.spawnCount; i++)
                {
                    Vector2 randomOffset = Random.insideUnitCircle * ability.randomRadius;
                    Vector2 finalPos = basePosition + randomOffset;

                    finalPos.x = Mathf.Clamp(finalPos.x, arenaMin.x, arenaMax.x);
                    finalPos.y = Mathf.Clamp(finalPos.y, arenaMin.y, arenaMax.y);

                    positions.Add(finalPos);
                }
                break;

            case SpawnPattern.Line:
                float totalWidth = (ability.spawnCount - 1) * ability.patternSpacing;
                float startX = basePosition.x - totalWidth / 2f;

                for (int i = 0; i < ability.spawnCount; i++)
                {
                    Vector2 pos = new Vector2(
                        startX + i * ability.patternSpacing,
                        basePosition.y
                    );

                    pos.x = Mathf.Clamp(pos.x, arenaMin.x, arenaMax.x);
                    pos.y = Mathf.Clamp(pos.y, arenaMin.y, arenaMax.y);

                    positions.Add(pos);
                }
                break;

            case SpawnPattern.Circle:
                float angleStep = 360f / ability.spawnCount;
                for (int i = 0; i < ability.spawnCount; i++)
                {
                    float angle = i * angleStep * Mathf.Deg2Rad;
                    Vector2 offset = new Vector2(
                        Mathf.Cos(angle) * ability.patternSpacing,
                        Mathf.Sin(angle) * ability.patternSpacing
                    );
                    Vector2 pos = basePosition + offset;

                    pos.x = Mathf.Clamp(pos.x, arenaMin.x, arenaMax.x);
                    pos.y = Mathf.Clamp(pos.y, arenaMin.y, arenaMax.y);

                    positions.Add(pos);
                }
                break;

            case SpawnPattern.Grid:
                int gridSize = Mathf.CeilToInt(Mathf.Sqrt(ability.spawnCount));
                for (int i = 0; i < ability.spawnCount; i++)
                {
                    int x = i % gridSize;
                    int y = i / gridSize;
                    Vector2 offset = new Vector2(
                        (x - gridSize / 2f) * ability.patternSpacing,
                        (y - gridSize / 2f) * ability.patternSpacing
                    );
                    Vector2 pos = basePosition + offset;

                    pos.x = Mathf.Clamp(pos.x, arenaMin.x, arenaMax.x);
                    pos.y = Mathf.Clamp(pos.y, arenaMin.y, arenaMax.y);

                    positions.Add(pos);
                }
                break;
        }

        return positions;
    }

    Vector2 GetRandomArenaPosition()
    {
        return new Vector2(
            Random.Range(arenaMin.x, arenaMax.x),
            Random.Range(arenaMin.y, arenaMax.y)
        );
    }

    void CreateSimpleWarning(Vector2 position, float duration, Color color)
    {
        GameObject warningObj = new GameObject("Warning");
        warningObj.transform.position = position;

        SpriteRenderer sr = warningObj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = new Color(color.r, color.g, color.b, 0.5f);
        sr.sortingOrder = 10;

        StartCoroutine(AnimateWarning(warningObj, duration));

        Destroy(warningObj, duration);
    }

    IEnumerator AnimateWarning(GameObject obj, float duration)
    {
        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
        float elapsed = 0f;
        Vector3 startScale = Vector3.one * 0.5f;
        Vector3 endScale = Vector3.one * 1.5f;

        while (elapsed < duration && obj != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            obj.transform.localScale = Vector3.Lerp(startScale, endScale, t);

            if (sr != null)
            {
                float alpha = Mathf.PingPong(Time.time * 5f, 0.7f) + 0.3f;
                Color c = sr.color;
                c.a = alpha;
                sr.color = c;
            }

            yield return null;
        }
    }

    Sprite CreateCircleSprite()
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= radius)
                {
                    pixels[y * size + x] = Color.white;
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    void SetupSpawnedObject(GameObject spawned, PhasePassiveAbility ability)
    {
        if (ability.abilityType == PassiveAbilityType.SpawnMinion)
        {
            if (spawned.TryGetComponent<FlyingEnemy>(out var flyingEnemy))
            {
                flyingEnemy.chaseMode = FlyingEnemy.ChaseMode.Zone;
                if (playerTransform != null)
                {
                    flyingEnemy.OnPlayerEnterZone(playerTransform.gameObject);
                }
            }
        }

        if (ability.abilityType == PassiveAbilityType.FallingHazard)
        {
            if (spawned.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.gravityScale = 2f;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
        }

        if (ability.abilityType == PassiveAbilityType.GroundHazard)
        {
            RaycastHit2D hit = Physics2D.Raycast(spawned.transform.position, Vector2.down, 50f, LayerMask.GetMask("Ground"));
            if (hit.collider != null)
            {
                spawned.transform.position = hit.point;
            }
        }
    }

    int GetActiveSpawnCount(int phaseIndex)
    {
        int count = 0;
        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Key != null && kvp.Value == phaseIndex)
            {
                count++;
            }
        }
        return count;
    }

    void CleanupDestroyedObjects()
    {
        List<GameObject> toRemove = new List<GameObject>();

        foreach (var kvp in spawnedObjects)
        {
            if (kvp.Key == null)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var obj in toRemove)
        {
            spawnedObjects.Remove(obj);
        }
    }

    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Gizmos.color = Color.yellow;
        Vector3 center = new Vector3((arenaMin.x + arenaMax.x) / 2f, (arenaMin.y + arenaMax.y) / 2f, 0);
        Vector3 size = new Vector3(arenaMax.x - arenaMin.x, arenaMax.y - arenaMin.y, 0);
        Gizmos.DrawWireCube(center, size);

        if (phaseAbilities != null && Application.isPlaying)
        {
            foreach (var ability in phaseAbilities)
            {
                if (ability.phaseIndex == currentPhase && ability.spawnPrefab != null)
                {
                    Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                    List<Vector2> positions = CalculateSpawnPositions(ability);
                    foreach (Vector2 pos in positions)
                    {
                        Gizmos.DrawWireSphere(pos, 0.5f);
                    }
                }
            }
        }
    }

    void OnDisable()
    {
        foreach (var kvp in activeCoroutines)
        {
            foreach (var coroutine in kvp.Value)
            {
                if (coroutine != null)
                {
                    StopCoroutine(coroutine);
                }
            }
        }
        activeCoroutines.Clear();
    }
}

//// ========================================
//// Hazard Objects (เหมือนเดิม)
//// ========================================

//public class FallingRock : MonoBehaviour
//{
//    [Header("Settings")]
//    public float damage = 10f;
//    public float lifetime = 10f;
//    public LayerMask damageableLayers;

//    [Header("Effects")]
//    public GameObject impactEffect;
//    public AudioClip impactSound;
//    [Range(0f, 1f)]
//    public float impactVolume = 0.7f;

//    [Header("Visual")]
//    public SpriteRenderer spriteRenderer;
//    public float rotationSpeed = 180f;

//    private bool hasHit = false;
//    private Rigidbody2D rb;

//    void Start()
//    {
//        rb = GetComponent<Rigidbody2D>();
//        if (rb != null)
//        {
//            rb.gravityScale = 2f;
//            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
//        }

//        if (spriteRenderer == null)
//        {
//            spriteRenderer = GetComponent<SpriteRenderer>();
//        }

//        Destroy(gameObject, lifetime);
//    }

//    void Update()
//    {
//        if (rb != null && !hasHit)
//        {
//            transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
//        }
//    }

//    void OnCollisionEnter2D(Collision2D collision)
//    {
//        if (hasHit) return;

//        hasHit = true;

//        if (((1 << collision.gameObject.layer) & damageableLayers) != 0)
//        {
//            if (collision.gameObject.TryGetComponent<IDamageable>(out var damageable))
//            {
//                damageable.TakeDamage(damage);
//            }
//        }

//        if (impactEffect != null)
//        {
//            Instantiate(impactEffect, transform.position, Quaternion.identity);
//        }

//        if (impactSound != null)
//        {
//            AudioSource.PlayClipAtPoint(impactSound, transform.position, impactVolume);
//        }

//        Destroy(gameObject, 0.1f);
//    }
//}

//public class GroundSpike : MonoBehaviour
//{
//    [Header("Settings")]
//    public float damage = 15f;
//    public float riseSpeed = 5f;
//    public float riseHeight = 2f;
//    public float stayDuration = 2f;
//    public float sinkSpeed = 3f;

//    [Header("Effects")]
//    public AudioClip riseSound;
//    [Range(0f, 1f)]
//    public float soundVolume = 0.7f;

//    private Vector3 startPosition;
//    private Vector3 targetPosition;
//    private enum State { Rising, Staying, Sinking }
//    private State currentState = State.Rising;
//    private float stayTimer = 0f;
//    private HashSet<GameObject> damagedObjects = new HashSet<GameObject>();

//    void Start()
//    {
//        startPosition = transform.position;
//        targetPosition = startPosition + Vector3.up * riseHeight;

//        if (riseSound != null)
//        {
//            AudioSource.PlayClipAtPoint(riseSound, transform.position, soundVolume);
//        }
//    }

//    void Update()
//    {
//        switch (currentState)
//        {
//            case State.Rising:
//                transform.position = Vector3.MoveTowards(transform.position, targetPosition, riseSpeed * Time.deltaTime);

//                if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
//                {
//                    currentState = State.Staying;
//                    stayTimer = 0f;
//                }
//                break;

//            case State.Staying:
//                stayTimer += Time.deltaTime;

//                if (stayTimer >= stayDuration)
//                {
//                    currentState = State.Sinking;
//                }
//                break;

//            case State.Sinking:
//                transform.position = Vector3.MoveTowards(transform.position, startPosition, sinkSpeed * Time.deltaTime);

//                if (Vector3.Distance(transform.position, startPosition) < 0.01f)
//                {
//                    Destroy(gameObject);
//                }
//                break;
//        }
//    }

//    void OnTriggerEnter2D(Collider2D other)
//    {
//        if (currentState == State.Rising || currentState == State.Staying)
//        {
//            if (!damagedObjects.Contains(other.gameObject))
//            {
//                if (other.TryGetComponent<IDamageable>(out var damageable))
//                {
//                    damageable.TakeDamage(damage);
//                    damagedObjects.Add(other.gameObject);
//                }
//            }
//        }
//    }
//}

//public class PoisonCloud : MonoBehaviour
//{
//    [Header("Settings")]
//    public float damagePerSecond = 5f;
//    public float lifetime = 10f;
//    public float damageInterval = 0.5f;
//    public float cloudRadius = 2f;

//    [Header("Visual")]
//    public SpriteRenderer cloudSprite;
//    public float fadeInDuration = 1f;
//    public float fadeOutDuration = 2f;
//    public Color cloudColor = new Color(0f, 1f, 0f, 0.5f);

//    private HashSet<IDamageable> affectedTargets = new HashSet<IDamageable>();
//    private float damageTimer = 0f;
//    private float lifeTimer = 0f;

//    void Start()
//    {
//        if (cloudSprite != null)
//        {
//            cloudSprite.color = cloudColor;
//            StartCoroutine(FadeInOut());
//        }

//        CircleCollider2D collider = GetComponent<CircleCollider2D>();
//        if (collider != null)
//        {
//            collider.radius = cloudRadius;
//            collider.isTrigger = true;
//        }
//    }

//    void Update()
//    {
//        lifeTimer += Time.deltaTime;
//        damageTimer += Time.deltaTime;

//        if (damageTimer >= damageInterval)
//        {
//            DamageAffectedTargets();
//            damageTimer = 0f;
//        }

//        if (lifeTimer >= lifetime)
//        {
//            Destroy(gameObject);
//        }
//    }

//    void DamageAffectedTargets()
//    {
//        List<IDamageable> toRemove = new List<IDamageable>();

//        foreach (var target in affectedTargets)
//        {
//            if (target != null && target is MonoBehaviour mb && mb != null)
//            {
//                target.TakeDamage(damagePerSecond * damageInterval);
//            }
//            else
//            {
//                toRemove.Add(target);
//            }
//        }

//        foreach (var target in toRemove)
//        {
//            affectedTargets.Remove(target);
//        }
//    }

//    void OnTriggerEnter2D(Collider2D other)
//    {
//        if (other.TryGetComponent<IDamageable>(out var damageable))
//        {
//            affectedTargets.Add(damageable);
//        }
//    }

//    void OnTriggerExit2D(Collider2D other)
//    {
//        if (other.TryGetComponent<IDamageable>(out var damageable))
//        {
//            affectedTargets.Remove(damageable);
//        }
//    }

//    IEnumerator FadeInOut()
//    {
//        float elapsed = 0f;
//        Color startColor = cloudSprite.color;
//        startColor.a = 0f;
//        cloudSprite.color = startColor;

//        while (elapsed < fadeInDuration)
//        {
//            elapsed += Time.deltaTime;
//            float alpha = elapsed / fadeInDuration;
//            Color color = cloudSprite.color;
//            color.a = alpha * cloudColor.a;
//            cloudSprite.color = color;
//            yield return null;
//        }

//        yield return new WaitForSeconds(lifetime - fadeInDuration - fadeOutDuration);

//        elapsed = 0f;
//        while (elapsed < fadeOutDuration)
//        {
//            elapsed += Time.deltaTime;
//            float alpha = 1f - (elapsed / fadeOutDuration);
//            Color color = cloudSprite.color;
//            color.a = alpha * cloudColor.a;
//            cloudSprite.color = color;
//            yield return null;
//        }
//    }
//}

public interface IDamageable
{
    void TakeDamage(float damage);
}
