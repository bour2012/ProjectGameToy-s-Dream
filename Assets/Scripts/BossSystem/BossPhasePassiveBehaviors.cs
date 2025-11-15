using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using System.Collections.Generic;

public class BossPhasePassiveBehaviors : MonoBehaviour
{
    [System.Serializable]
    public class PhasePassiveAbility
    {
        [System.Serializable]
        public class ObstacleSlot
        {
            [Tooltip("Local position relative to the boss when 'relativeToBoss' is true, otherwise world position.")]
            public Vector2 slotPosition = Vector2.zero;

            [Tooltip("If true, slotPosition is interpreted relative to the boss transform; otherwise world coordinates.")]
            public bool relativeToBoss = true;

            [Tooltip("List of prefabs that can be used for this slot. Spawning will pick from these using the start index and sequence rules.")]
            public GameObject[] prefabs;

            [Tooltip("How many objects to spawn at this slot (will pick prefabs in sequence).")]
            public int spawnCount = 1;

            [Tooltip("Starting index into the 'prefabs' array for the first spawned object at this slot.")]
            public int startPrefabIndex = 0;

            [Tooltip("If true, spawned objects at this slot will use subsequent prefab indices (wraparound). If false, the same prefab will be used for all items at this slot.")]
            public bool usePrefabSequence = true;

            [Tooltip("Optional spacing (local X axis) to place multiple items at this slot in order.")]
            public float spacingBetweenItems = 0.5f;
        }

        [Header("Phase Info")]
        public string abilityName = "Falling Rocks";
        public int phaseIndex = 1;

        [Header("Ability Type")]
        public PassiveAbilityType abilityType;

        [Header("Auto-Start Settings")]
        public bool autoStartOnPhaseChange = true;

        [Header("Spawn Settings")]
        public GameObject spawnPrefab;
        public float spawnInterval = 3f;
        public int spawnCount = 0;

        [Header("Wave Limit Settings (for Minion/Obstacle)")]
        public bool useWaveLimit = false;
        public int maxWaves = 3;
        public int enemiesPerWave = 5;
        public bool waitForWaveComplete = true;

        [Header("Spawn Area")]
        public SpawnAreaType areaType = SpawnAreaType.AbovePlayer;
        public Vector2 spawnOffset = Vector2.zero;
        public float spawnHeight = 10f;
        public float randomRadius = 3f;

        [Header("Spawn Pattern")]
        public SpawnPattern pattern = SpawnPattern.Random;
        public float patternSpacing = 2f;

        [Header("Obstacle Slots (CreateObstacle only)")]
        [Tooltip("Define explicit slots for CreateObstacle abilities. Each slot can hold multiple prefabs and spawn counts.")]
        public ObstacleSlot[] obstacleSlots;

        [Header("Spawn Timing")]
        public bool sequentialSpawn = true;
        public float spawnDelay = 0.2f;

        [Header("Object Lifetime")]
        public bool autoDestroy = true;
        public float destroyAfter = 5f;

        [Header("Advanced Settings")]
        public bool onlyWhenGrounded = false;
        public bool onlyWhenFlying = false;
        public float startDelay = 0f;
        public int maxActiveCount = -1;

        [Header("Visual Effects")]
        public GameObject warningEffectPrefab;
        public float warningDuration = 0.5f;
        public Color warningColor = Color.red;

        [Header("Audio")]
        public AudioClip spawnSound;
        [Range(0f, 1f)]
        public float soundVolume = 1f;

        [HideInInspector] public int currentWave = 0;
        [HideInInspector] public List<GameObject> currentWaveObjects = new List<GameObject>();

        public PhasePassiveAbility Clone()
        {
            var c = new PhasePassiveAbility();
            c.abilityName = this.abilityName;
            c.phaseIndex = this.phaseIndex;
            c.abilityType = this.abilityType;
            c.spawnPrefab = this.spawnPrefab;
            c.spawnInterval = this.spawnInterval;
            c.spawnCount = this.spawnCount;
            c.useWaveLimit = this.useWaveLimit;
            c.maxWaves = this.maxWaves;
            c.enemiesPerWave = this.enemiesPerWave;
            c.waitForWaveComplete = this.waitForWaveComplete;
            c.areaType = this.areaType;
            c.spawnOffset = this.spawnOffset;
            c.spawnHeight = this.spawnHeight;
            c.randomRadius = this.randomRadius;
            c.pattern = this.pattern;
            c.patternSpacing = this.patternSpacing;
            c.sequentialSpawn = this.sequentialSpawn;
            c.spawnDelay = this.spawnDelay;
            c.autoDestroy = this.autoDestroy;
            c.destroyAfter = this.destroyAfter;
            c.onlyWhenGrounded = this.onlyWhenGrounded;
            c.onlyWhenFlying = this.onlyWhenFlying;
            c.startDelay = this.startDelay;
            c.maxActiveCount = this.maxActiveCount;
            c.warningEffectPrefab = this.warningEffectPrefab;
            c.warningDuration = this.warningDuration;
            c.warningColor = this.warningColor;
            c.spawnSound = this.spawnSound;
            c.soundVolume = this.soundVolume;
            c.autoStartOnPhaseChange = this.autoStartOnPhaseChange;

            if (this.obstacleSlots != null)
            {
                c.obstacleSlots = new ObstacleSlot[this.obstacleSlots.Length];
                for (int i = 0; i < this.obstacleSlots.Length; i++)
                {
                    var s = new ObstacleSlot();
                    s.slotPosition = this.obstacleSlots[i].slotPosition;
                    s.relativeToBoss = this.obstacleSlots[i].relativeToBoss;
                    s.prefabs = this.obstacleSlots[i].prefabs;
                    s.spawnCount = this.obstacleSlots[i].spawnCount;
                    s.startPrefabIndex = this.obstacleSlots[i].startPrefabIndex;
                    s.usePrefabSequence = this.obstacleSlots[i].usePrefabSequence;
                    s.spacingBetweenItems = this.obstacleSlots[i].spacingBetweenItems;
                    c.obstacleSlots[i] = s;
                }
            }

            c.currentWave = 0;
            c.currentWaveObjects = new List<GameObject>();

            return c;
        }
    }

    public enum PassiveAbilityType
    {
        FallingHazard,
        GroundHazard,
        SpawnMinion,
        CreateObstacle,
        PoisonCloud
    }

    public enum SpawnAreaType
    {
        AbovePlayer,
        AroundPlayer,
        AroundBoss,
        RandomInArena,
        BehindPlayer
    }

    public enum SpawnPattern
    {
        Random,
        Line,
        Circle,
        Grid
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

    public bool TriggerAbilityByName(string abilityName)
    {
        if (phaseAbilities == null || phaseAbilities.Length == 0) return false;

        foreach (var ability in phaseAbilities)
        {
            if (ability != null && ability.abilityName == abilityName)
            {
                var runtimeCopy = ability.Clone();
                StartCoroutine(PassiveAbilityRoutine(runtimeCopy));
                if (showDebugLogs) Debug.Log($"[PhasePassive] TriggerAbilityByName started: {abilityName}");
                return true;
            }
        }

        if (showDebugLogs) Debug.LogWarning($"[PhasePassive] TriggerAbilityByName: ability '{abilityName}' not found");
        return false;
    }

    public PhasePassiveAbility FindAbilityByName(string abilityName)
    {
        if (phaseAbilities == null || phaseAbilities.Length == 0) return null;
        foreach (var ability in phaseAbilities)
        {
            if (ability != null && ability.abilityName == abilityName) return ability;
        }
        return null;
    }

    public void TriggerAbilityInstance(PhasePassiveAbility abilityInstance)
    {
        if (abilityInstance == null) return;
        StartCoroutine(PassiveAbilityRoutine(abilityInstance));
        if (showDebugLogs) Debug.Log($"[PhasePassive] TriggerAbilityInstance started: {abilityInstance.abilityName}");
    }

    void Update()
    {
        if (bossController != null && bossController.currentPhaseIndex != currentPhase)
        {
            OnPhaseChanged(bossController.currentPhaseIndex);
        }

        CleanupDestroyedObjects();
    }

    public void OnPhaseChanged(int newPhase)
    {
        if (showDebugLogs)
            Debug.Log($"[PhasePassive] Phase changed to {newPhase}");

        StopAllPhaseAbilities(currentPhase);

        currentPhase = newPhase;

        foreach (var ability in phaseAbilities)
        {
            if (ability.phaseIndex == newPhase)
            {
                ability.currentWave = 0;
                ability.currentWaveObjects.Clear();
            }
        }

        StartPhaseAbilities(newPhase);
    }

    void StartPhaseAbilities(int phaseIndex)
    {
        if (phaseAbilities == null || phaseAbilities.Length == 0) return;

        foreach (var ability in phaseAbilities)
        {
            if (ability.phaseIndex == phaseIndex && (ability.spawnPrefab != null || (ability.abilityType == PassiveAbilityType.CreateObstacle && ability.obstacleSlots != null && ability.obstacleSlots.Length > 0)))
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
        if (ability.startDelay > 0)
        {
            yield return new WaitForSeconds(ability.startDelay);
        }

        if (ability.useWaveLimit)
        {
            yield return StartCoroutine(WaveLimitRoutine(ability));
        }
        else
        {
            yield return StartCoroutine(NormalRoutine(ability));
        }
    }

    IEnumerator WaveLimitRoutine(PhasePassiveAbility ability)
    {
        while (true)
        {
            if (ability.maxWaves > 0 && ability.currentWave >= ability.maxWaves)
            {
                if (showDebugLogs)
                    Debug.Log($"[PhasePassive] {ability.abilityName} reached max waves ({ability.maxWaves})");
                yield break;
            }

            if (!ShouldActivateAbility(ability))
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            if (ability.waitForWaveComplete && ability.currentWave > 0)
            {
                yield return new WaitUntil(() => IsWaveComplete(ability));

                if (showDebugLogs)
                    Debug.Log($"[PhasePassive] {ability.abilityName} wave {ability.currentWave} completed!");

                yield return new WaitForSeconds(ability.spawnInterval);
            }

            ability.currentWave++;
            ability.currentWaveObjects.Clear();

            if (showDebugLogs)
                Debug.Log($"[PhasePassive] {ability.abilityName} spawning wave {ability.currentWave}/{ability.maxWaves}");

            int originalSpawnCount = ability.spawnCount;
            ability.spawnCount = ability.enemiesPerWave;

            if (bossController != null && ability.abilityType == PassiveAbilityType.SpawnMinion && ability.waitForWaveComplete)
            {
                bossController.PauseForSummons(true);
            }

            yield return StartCoroutine(ExecuteAbility(ability));

            if (ability.waitForWaveComplete && ability.abilityType == PassiveAbilityType.SpawnMinion)
            {
                yield return new WaitUntil(() => IsWaveComplete(ability));
                if (bossController != null)
                    bossController.PauseForSummons(false);
            }

            ability.spawnCount = originalSpawnCount;

            if (!ability.waitForWaveComplete)
            {
                yield return new WaitForSeconds(ability.spawnInterval);
            }
        }
    }

    IEnumerator NormalRoutine(PhasePassiveAbility ability)
    {
        while (true)
        {
            if (ShouldActivateAbility(ability))
            {
                if (ability.maxActiveCount > 0)
                {
                    int activeCount = GetActiveSpawnCount(ability.phaseIndex);
                    if (activeCount >= ability.maxActiveCount)
                    {
                        yield return new WaitForSeconds(0.5f);
                        continue;
                    }
                }

                if (bossController != null && ability.abilityType == PassiveAbilityType.SpawnMinion && ability.waitForWaveComplete)
                {
                    bossController.PauseForSummons(true);
                }

                yield return StartCoroutine(ExecuteAbility(ability));

                if (ability.waitForWaveComplete && ability.abilityType == PassiveAbilityType.SpawnMinion)
                {
                    yield return new WaitUntil(() => IsWaveComplete(ability));
                    if (bossController != null)
                        bossController.PauseForSummons(false);
                }
            }

            yield return new WaitForSeconds(ability.spawnInterval);
        }
    }

    bool IsWaveComplete(PhasePassiveAbility ability)
    {
        ability.currentWaveObjects.RemoveAll(obj => obj == null);
        return ability.currentWaveObjects.Count == 0;
    }

    bool ShouldActivateAbility(PhasePassiveAbility ability)
    {
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
        if (ability.abilityType == PassiveAbilityType.CreateObstacle && ability.obstacleSlots != null && ability.obstacleSlots.Length > 0)
        {
            yield return StartCoroutine(ExecuteCreateObstacles(ability));
            yield break;
        }

        bool pausedByThis = false;
        if (ability.abilityType == PassiveAbilityType.SpawnMinion && ability.waitForWaveComplete && bossController != null)
        {
            bossController.PauseForSummons(true);
            pausedByThis = true;
            if (showDebugLogs) Debug.Log($"[PhasePassive] Pausing boss for SpawnMinion '{ability.abilityName}' (one-shot)");
        }

        List<Vector2> spawnPositions = CalculateSpawnPositions(ability);

        if (ability.sequentialSpawn)
        {
            yield return StartCoroutine(SpawnSequentially(ability, spawnPositions));
        }
        else
        {
            yield return StartCoroutine(SpawnSimultaneously(ability, spawnPositions));
        }

        if (pausedByThis)
        {
            if (showDebugLogs) Debug.Log($"[PhasePassive] Waiting for SpawnMinion '{ability.abilityName}' spawned objects to be destroyed...");
            yield return new WaitUntil(() => IsWaveComplete(ability));
            bossController.PauseForSummons(false);
            if (showDebugLogs) Debug.Log($"[PhasePassive] SpawnMinion '{ability.abilityName}' cleared, resuming boss actions");
        }
    }

    IEnumerator ExecuteCreateObstacles(PhasePassiveAbility ability)
    {
        if (ability.obstacleSlots == null || ability.obstacleSlots.Length == 0) yield break;

        if (bossController != null && ability.waitForWaveComplete && ability.abilityType == PassiveAbilityType.CreateObstacle)
        {
            bossController.PauseForSummons(true);
            if (showDebugLogs) Debug.Log($"[PhasePassive] Pausing boss for CreateObstacle '{ability.abilityName}' until spawned objects clear");
        }

        for (int s = 0; s < ability.obstacleSlots.Length; s++)
        {
            var slot = ability.obstacleSlots[s];
            Vector2 basePos = slot.relativeToBoss ? (Vector2)transform.position + slot.slotPosition : slot.slotPosition;

            if (ability.warningEffectPrefab != null && ability.warningDuration > 0)
            {
                GameObject warning = Instantiate(ability.warningEffectPrefab, basePos, Quaternion.identity);
                Destroy(warning, ability.warningDuration);
                yield return new WaitForSeconds(ability.warningDuration);
            }

            for (int i = 0; i < Mathf.Max(0, slot.spawnCount); i++)
            {
                GameObject prefabToUse = ability.spawnPrefab;
                if (slot.prefabs != null && slot.prefabs.Length > 0)
                {
                    int idx = slot.startPrefabIndex;
                    if (slot.usePrefabSequence)
                    {
                        idx = (slot.startPrefabIndex + i) % slot.prefabs.Length;
                    }
                    idx = Mathf.Clamp(idx, 0, slot.prefabs.Length - 1);
                    prefabToUse = slot.prefabs[idx];
                }

                if (prefabToUse == null) continue;

                Vector2 itemPos = basePos + new Vector2(i * slot.spacingBetweenItems, 0f);

                GameObject spawned = Instantiate(prefabToUse, itemPos, Quaternion.identity);
                spawnedObjects[spawned] = ability.phaseIndex;

                if (ability.waitForWaveComplete || ability.useWaveLimit)
                {
                    ability.currentWaveObjects.Add(spawned);
                }

                SetupSpawnedObject(spawned, ability);

                if (ability.autoDestroy && ability.destroyAfter > 0)
                {
                    Destroy(spawned, ability.destroyAfter);
                }

                if (ability.spawnSound != null)
                {
                    AudioSource.PlayClipAtPoint(ability.spawnSound, transform.position, ability.soundVolume);
                }

                if (showDebugLogs)
                    Debug.Log($"[PhasePassive] CreateObstacle spawned '{spawned.name}' at {itemPos} (slot {s + 1}/{ability.obstacleSlots.Length})");

                if (i < slot.spawnCount - 1)
                    yield return new WaitForSeconds(ability.spawnDelay > 0 ? ability.spawnDelay : 0.05f);
            }

            if (ability.sequentialSpawn && s < ability.obstacleSlots.Length - 1)
            {
                yield return new WaitForSeconds(ability.spawnInterval);
            }
        }

        if (bossController != null && ability.waitForWaveComplete && ability.abilityType == PassiveAbilityType.CreateObstacle)
        {
            if (showDebugLogs) Debug.Log($"[PhasePassive] Waiting for CreateObstacle '{ability.abilityName}' spawned objects to be destroyed...");
            yield return new WaitUntil(() => IsWaveComplete(ability));
            bossController.PauseForSummons(false);
            if (showDebugLogs) Debug.Log($"[PhasePassive] CreateObstacle '{ability.abilityName}' cleared, resuming boss actions");
        }
    }

    IEnumerator SpawnSequentially(PhasePassiveAbility ability, List<Vector2> spawnPositions)
    {
        for (int i = 0; i < spawnPositions.Count; i++)
        {
            Vector2 pos = spawnPositions[i];

            if (ability.warningEffectPrefab != null && ability.warningDuration > 0)
            {
                GameObject warning = Instantiate(ability.warningEffectPrefab, pos, Quaternion.identity);
                Destroy(warning, ability.warningDuration);
            }
            else if (ability.warningDuration > 0)
            {
                CreateSimpleWarning(pos, ability.warningDuration, ability.warningColor);
            }

            if (ability.warningDuration > 0)
            {
                yield return new WaitForSeconds(ability.warningDuration);
            }

            GameObject spawned = Instantiate(ability.spawnPrefab, pos, Quaternion.identity);

            spawnedObjects[spawned] = ability.phaseIndex;

            if (ability.useWaveLimit || ability.waitForWaveComplete)
            {
                ability.currentWaveObjects.Add(spawned);
            }

            SetupSpawnedObject(spawned, ability);

            if (ability.autoDestroy && ability.destroyAfter > 0 && ability.abilityType != PassiveAbilityType.SpawnMinion)
            {
                Destroy(spawned, ability.destroyAfter);
            }

            if (ability.spawnSound != null)
            {
                AudioSource.PlayClipAtPoint(ability.spawnSound, transform.position, ability.soundVolume);
            }

            if (showDebugLogs)
                Debug.Log($"[PhasePassive] Spawned {ability.abilityName} at {pos} ({i + 1}/{spawnPositions.Count})");

            if (i < spawnPositions.Count - 1)
            {
                yield return new WaitForSeconds(ability.spawnDelay);
            }
        }
    }

    IEnumerator SpawnSimultaneously(PhasePassiveAbility ability, List<Vector2> spawnPositions)
    {
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

        foreach (Vector2 pos in spawnPositions)
        {
            GameObject spawned = Instantiate(ability.spawnPrefab, pos, Quaternion.identity);

            spawnedObjects[spawned] = ability.phaseIndex;

            if (ability.useWaveLimit || ability.waitForWaveComplete)
            {
                ability.currentWaveObjects.Add(spawned);
            }

            SetupSpawnedObject(spawned, ability);

            if (ability.autoDestroy && ability.destroyAfter > 0 && ability.abilityType != PassiveAbilityType.SpawnMinion)
            {
                Destroy(spawned, ability.destroyAfter);
            }

            if (showDebugLogs)
                Debug.Log($"[PhasePassive] Spawned {ability.abilityName} at {pos}");
        }

        if (ability.spawnSound != null)
        {
            AudioSource.PlayClipAtPoint(ability.spawnSound, transform.position, ability.soundVolume);
        }
    }

    public List<Vector2> GetSpawnPositionsForAbility(PhasePassiveAbility ability)
    {
        return CalculateSpawnPositions(ability);
    }

    public void ExecuteAbilityOnce(PhasePassiveAbility abilityInstance)
    {
        if (abilityInstance == null) return;
        StartCoroutine(ExecuteAbility(abilityInstance));
        if (showDebugLogs) Debug.Log($"[PhasePassive] ExecuteAbilityOnce: {abilityInstance.abilityName}");
    }

    List<Vector2> CalculateSpawnPositions(PhasePassiveAbility ability)
    {
        List<Vector2> positions = new List<Vector2>();
        Vector2 basePosition = Vector2.zero;

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

        if (phaseAbilities != null)
        {
            foreach (var ability in phaseAbilities)
            {
                if (Application.isPlaying && ability.phaseIndex != currentPhase) continue;

                Gizmos.color = new Color(1f, 0f, 0f, 0.3f);

                if (ability.abilityType == PassiveAbilityType.CreateObstacle && ability.obstacleSlots != null && ability.obstacleSlots.Length > 0)
                {
                    for (int i = 0; i < ability.obstacleSlots.Length; i++)
                    {
                        var slot = ability.obstacleSlots[i];
                        Vector2 basePos = slot.relativeToBoss ? (Vector2)transform.position + slot.slotPosition : slot.slotPosition;

                        int displayCount = Mathf.Max(1, slot.spawnCount);
                        for (int item = 0; item < displayCount; item++)
                        {
                            Vector2 itemPos = basePos + new Vector2(item * slot.spacingBetweenItems, 0f);
                            Gizmos.DrawWireSphere(itemPos, 0.5f);
                        }
                    }
                }
                else
                {
                    List<Vector2> positions = null;
                    try
                    {
                        positions = CalculateSpawnPositions(ability);
                    }
                    catch
                    {
                        positions = new List<Vector2>();
                    }

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

public interface IDamageable
{
    void TakeDamage(float damage);
}