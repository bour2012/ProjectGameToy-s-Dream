using System.Collections;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [Header("Level Configuration")]
    public LevelData[] levels;
    public int currentLevelIndex = 0;

    [Header("Player Settings")]
    public Transform player;
    public Vector3[] playerStartPositions; // Player spawn positions for each level

    [Header("Particle Effects")]
    public ParticleSystem spawnEffect;
    public ParticleSystem despawnEffect;
    public float effectDuration = 1f;
    public float spawnDelay = 0.1f; // Delay between spawning objects

    [Header("Boss Integration")]

    public Transform boss;

    public bool isTransitioning = false;

    // Expose transition state so other systems (e.g. BossController) can query
    // whether a level transition is currently in progress.
    public bool IsTransitioning { get { return isTransitioning; } }
    private const string BOSS_PHASE_KEY = "CurrentBossPhase";
    [System.Serializable]
    public class LevelData
    {
        public string levelName;
        [Header("Dialogs")]
        [Tooltip("Dialog triggers to play BEFORE transitioning away from this level. They will be played in order and LevelManager will wait for completion.")]
        public DialogTrigger[] preTransitionDialogs;
        public LevelObject[] levelObjects;

        [System.Serializable]
        public class LevelObject
        {
            public GameObject gameObject; // Direct reference to existing object
            [HideInInspector]
            public Vector3 storedPosition;
            [HideInInspector]
            public Quaternion storedRotation;
            [HideInInspector]
            public bool wasActiveAtStart;
        }
    }

    void Start()
    {
        // Store initial positions for all objects and hide them
        InitializeLevels();

        // Start with configured currentLevelIndex (clamped). This respects inspector override.
        if (PlayerPrefs.HasKey(BOSS_PHASE_KEY))
        {
            currentLevelIndex = PlayerPrefs.GetInt(BOSS_PHASE_KEY);
            Debug.Log($"<color=yellow>Load Boss Phase from Save: Phase {currentLevelIndex}</color>");
        }
        else
        {
            // ถ้าไม่มีเซฟ ให้ใช้ค่า Default (0)
            currentLevelIndex = 0;
        }

        // เริ่มโหลดด่านตาม Index ที่ได้มา (0 หรือค่าที่เซฟไว้)
        if (levels.Length > 0)
        {
            int startIdx = Mathf.Clamp(currentLevelIndex, 0, levels.Length - 1);

            // 1. โหลดฉาก
            StartCoroutine(LoadLevel(startIdx));

     
            MovePlayerToStartPosition();
        }

    }

    void InitializeLevels()
    {
        // Store positions and hide all objects except current level
        for (int levelIndex = 0; levelIndex < levels.Length; levelIndex++)
        {
            var level = levels[levelIndex];
            for (int objIndex = 0; objIndex < level.levelObjects.Length; objIndex++)
            {
                var levelObj = level.levelObjects[objIndex];
                if (levelObj.gameObject != null)
                {
                    // Store current position and state
                    levelObj.storedPosition = levelObj.gameObject.transform.position;
                    levelObj.storedRotation = levelObj.gameObject.transform.rotation;
                    levelObj.wasActiveAtStart = levelObj.gameObject.activeSelf;

                    // Hide all objects initially (will show current level later)
                    levelObj.gameObject.SetActive(false);
                }
            }
        }
    }

    public void NextLevel()
    {
        if (isTransitioning) return;

        int nextIndex = currentLevelIndex + 1;
        if (nextIndex < levels.Length)
        {
            
                StartCoroutine(TransitionToLevel(nextIndex));
           

        }
        else
        {
            //Debug.Log("Index Level" + nextIndex);
            Debug.Log("All levels completed!");
            // Handle game completion
        }
    }

    public void LoadSpecificLevel(int levelIndex)
    {
        if (isTransitioning || levelIndex < 0 || levelIndex >= levels.Length) return;

        StartCoroutine(TransitionToLevel(levelIndex));
    }

    private IEnumerator TransitionToLevel(int targetLevelIndex)
    {
        isTransitioning = true;
        PlayerPrefs.SetInt(BOSS_PHASE_KEY, targetLevelIndex);
        PlayerPrefs.Save();
        Debug.Log($"<color=cyan>Saved Boss Phase: {targetLevelIndex}</color>");
        // Despawn current level
        yield return StartCoroutine(DespawnCurrentLevel());

        // Wait a moment between transitions
        yield return new WaitForSeconds(0.5f);

        // Update current level index
        currentLevelIndex = targetLevelIndex;

        // Spawn new level
        yield return StartCoroutine(LoadLevel(targetLevelIndex));

        // Move player to new position
        MovePlayerToStartPosition();
        UpdateBossPhase(targetLevelIndex);
        //// Reset boss to idle state
        //SetBossIdleState();



        // Ensure boss glue accumulation state is reset when changing levels
        if (boss != null)
        {
            var bc = boss.GetComponent<BossController>();
            if (bc != null)
            {
                //bc.ResetGlueAccumulation();
                Debug.Log("LevelManager: Called ResetGlueAccumulation on boss.");
            }
        }

        isTransitioning = false;
    }

    private IEnumerator LoadLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= levels.Length) yield break;

        var level = levels[levelIndex];
        Debug.Log($"Loading level: {level.levelName}");

        // Play particle effects for all objects first
        for (int i = 0; i < level.levelObjects.Length; i++)
        {
            var levelObj = level.levelObjects[i];
            if (levelObj.gameObject != null && spawnEffect != null)
            {
                PlayParticleEffect(spawnEffect, levelObj.storedPosition);
            }
        }

        // Wait for particle effects to play
        yield return new WaitForSeconds(effectDuration * 0.5f);

        // Show all objects simultaneously
        for (int i = 0; i < level.levelObjects.Length; i++)
        {
            var levelObj = level.levelObjects[i];
            if (levelObj.gameObject != null)
            {
                // Restore position and show object
                levelObj.gameObject.transform.position = levelObj.storedPosition;
                levelObj.gameObject.transform.rotation = levelObj.storedRotation;
                levelObj.gameObject.SetActive(true);
            }
        }
        UpdateBossPhase(levelIndex);
        Debug.Log($"Level {level.levelName} loaded with {level.levelObjects.Length} objects");
    }
    private void UpdateBossPhase(int levelIndex)
    {
        if (boss != null)
        {
            var bossCtrl = boss.GetComponent<BossController>();
            if (bossCtrl != null)
            {
                bossCtrl.EnterPhase(levelIndex);
            }
        }
    }

    public void ClearBossPhaseSave()
    {
        PlayerPrefs.DeleteKey(BOSS_PHASE_KEY);
        PlayerPrefs.Save();
        Debug.Log("Cleared Boss Phase Save.");
    }
    private IEnumerator DespawnCurrentLevel()
    {
        if (currentLevelIndex < 0 || currentLevelIndex >= levels.Length) yield break;

        var level = levels[currentLevelIndex];
        Debug.Log($"Despawning level: {level.levelName}");

        // Play particle effects for all active objects
        for (int i = 0; i < level.levelObjects.Length; i++)
        {
            var levelObj = level.levelObjects[i];
            if (levelObj.gameObject != null && levelObj.gameObject.activeSelf && despawnEffect != null)
            {
                PlayParticleEffect(despawnEffect, levelObj.gameObject.transform.position);
            }
        }

        // Wait for particle effects
        yield return new WaitForSeconds(effectDuration * 0.3f);

        // Hide all objects simultaneously
        for (int i = 0; i < level.levelObjects.Length; i++)
        {
            var levelObj = level.levelObjects[i];
            if (levelObj.gameObject != null)
            {
                levelObj.gameObject.SetActive(false);
            }
        }

        Debug.Log($"Level {level.levelName} despawned");
    }

    private void PlayParticleEffect(ParticleSystem effect, Vector3 position)
    {
        if (effect != null)
        {
            // Instantiate a temporary particle system at the target position so multiple effects can overlap.
            ParticleSystem ps = Instantiate(effect, position, Quaternion.identity);

            // Ensure the particle system simulates in world space so it stays where spawned.
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ps.Play();

            // Destroy the particle system after its duration to avoid clutter.
            Destroy(ps.gameObject, effectDuration + 0.5f);
        }
    }

    private void MovePlayerToStartPosition()
    {
        if (player != null && currentLevelIndex < playerStartPositions.Length)
        {
            player.position = playerStartPositions[currentLevelIndex];

            // Reset player velocity if it has a Rigidbody2D
            var playerRb = player.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector2.zero;
                playerRb.angularVelocity = 0f;
            }

            Debug.Log($"Player moved to position: {playerStartPositions[currentLevelIndex]}");
        }
    }

    //private void SetBossIdleState()
    //{
    //    if (boss != null)
    //    {
    //        //// Disable boss movement/AI components
    //        //var bossController = boss.GetComponent<BossController>();
    //        //if (bossController != null)
    //        //{
    //        //    bossController.enabled = false;
    //        //}

    //        //var bossAI = boss.GetComponent<BossAttackAI>();
    //        //if (bossAI != null)
    //        //{
    //        //    bossAI.enabled = false;
    //        //}

    //        //// Stop boss movement
    //        //var bossRb = boss.GetComponent<Rigidbody2D>();
    //        //if (bossRb != null)
    //        //{
    //        //    bossRb.linearVelocity = Vector2.zero;
    //        //    bossRb.angularVelocity = 0f;
    //        //}

    //        // Reset glue meter
    //        if (bossGlueMeter != null)
    //        {
    //            bossGlueMeter.ResetMeter();
    //        }

    //        Debug.Log("Boss set to idle state");
    //    }
    //}

    //public IEnumerator MonitorBossGlueMeter()
    //{
    //    while (true)
    //    {
    //        if (bossGlueMeter != null && bossGlueMeter.IsMeterFull())
    //        {
    //            Debug.Log("Boss glue meter is full! Triggering level transition...");
    //            //NextLevel();
    //            StartCoroutine(PlayPreTransitionDialogsAndAdvance());
    //            // Wait a bit before checking again to avoid multiple triggers
    //            yield return new WaitForSeconds(2f);
    //        }

    //        yield return new WaitForSeconds(0.1f); // Check every frame
    //    }
    //}

    // Public methods for external triggers
    public void TriggerLevelTransition()
    {
        if (isTransitioning) return;
        StartCoroutine(PlayPreTransitionDialogsAndAdvance());
    }

    private System.Collections.IEnumerator PlayPreTransitionDialogsAndAdvance()
    {
        if (isTransitioning) yield break;
        isTransitioning = true; // reserve transition to prevent reentry while dialogs play

        int lvl = currentLevelIndex;
        if (levels != null && lvl >= 0 && lvl < levels.Length)
        {
            var dialogs = levels[lvl].preTransitionDialogs;
            if (dialogs != null && dialogs.Length > 0)
            {
                for (int i = 0; i < dialogs.Length; i++)
                {
                    var dlg = dialogs[i];
                    if (dlg == null) continue;

                    bool completed = false;
                    UnityEngine.Events.UnityAction onComplete = () => { completed = true; };

                    // attach listener
                    dlg.onDialogComplete.AddListener(onComplete);

                    // trigger dialog
                    dlg.TriggerDialog();

                    // wait until done
                    while (!completed)
                        yield return null;

                    // cleanup
                    dlg.onDialogComplete.RemoveListener(onComplete);
                }
            }
        }

        // done with dialogs — now actually advance
        isTransitioning = false; // allow NextLevel to set it and proceed normally
        NextLevel();
    }

    public void TriggerSpecificLevel(int levelIndex)
    {
        LoadSpecificLevel(levelIndex);
    }

    // Method to store current scene objects into a level data
    [ContextMenu("Store Current Scene Objects")]
    public void StoreCurrentSceneObjects()
    {
        if (currentLevelIndex >= 0 && currentLevelIndex < levels.Length)
        {
            var level = levels[currentLevelIndex];
            for (int i = 0; i < level.levelObjects.Length; i++)
            {
                var levelObj = level.levelObjects[i];
                if (levelObj.gameObject != null)
                {
                    levelObj.storedPosition = levelObj.gameObject.transform.position;
                    levelObj.storedRotation = levelObj.gameObject.transform.rotation;
                    levelObj.wasActiveAtStart = levelObj.gameObject.activeSelf;
                }
            }
            Debug.Log($"Stored positions for level: {level.levelName}");
        }
    }
}