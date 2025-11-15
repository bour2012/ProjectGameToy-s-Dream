using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BossController))]
public class BossPassiveExecutor : MonoBehaviour
{
    public BossPhasePassiveBehaviors passiveConfig;
    private BossController bossController;

    void Awake()
    {
        bossController = GetComponent<BossController>();
        if (passiveConfig == null) passiveConfig = GetComponent<BossPhasePassiveBehaviors>();
    }

    public void ExecuteAbilityInstance(BossPhasePassiveBehaviors.PhasePassiveAbility ability)
    {
        if (ability == null) return;
        StartCoroutine(ExecuteAbilityRoutine(ability));
    }

    private IEnumerator ExecuteAbilityRoutine(BossPhasePassiveBehaviors.PhasePassiveAbility ability)
    {
        // start delay
        if (ability.startDelay > 0f)
            yield return new WaitForSeconds(ability.startDelay);

        // calculate spawn positions using the passiveConfig helper (if available)
        List<Vector2> positions = null;
        if (passiveConfig != null)
        {
            positions = passiveConfig.GetSpawnPositionsForAbility(ability);
        }
        else
        {
            // fallback: spawn around boss
            positions = new List<Vector2>();
            for (int i = 0; i < ability.spawnCount; i++) positions.Add((Vector2)transform.position + Random.insideUnitCircle * 2f);
        }

        // optionally show warnings and spawn sequentially or simultaneously
        if (ability.sequentialSpawn)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                Vector2 pos = positions[i];
                if (ability.warningEffectPrefab != null && ability.warningDuration > 0f)
                {
                    GameObject w = Instantiate(ability.warningEffectPrefab, pos, Quaternion.identity);
                    Destroy(w, ability.warningDuration);
                }
                else if (ability.warningDuration > 0f)
                {
                    // simple visual: none
                }

                if (ability.warningDuration > 0f)
                    yield return new WaitForSeconds(ability.warningDuration);

                SpawnOne(ability, pos);

                if (i < positions.Count - 1)
                    yield return new WaitForSeconds(ability.spawnDelay);
            }
        }
        else
        {
            if (ability.warningEffectPrefab != null && ability.warningDuration > 0f)
            {
                foreach (var pos in positions)
                {
                    GameObject w = Instantiate(ability.warningEffectPrefab, pos, Quaternion.identity);
                    Destroy(w, ability.warningDuration);
                }
                yield return new WaitForSeconds(ability.warningDuration);
            }

            foreach (var pos in positions)
            {
                SpawnOne(ability, pos);
            }
        }

        // If this ability expects waitForWaveComplete (typically for minion summons), pause boss actions until spawned minions die
        if (ability.waitForWaveComplete && ability.abilityType == BossPhasePassiveBehaviors.PassiveAbilityType.SpawnMinion)
        {
            if (bossController != null)
                bossController.PauseForSummons(true);

            // wait until all spawned objects (tracked in ability.currentWaveObjects) are destroyed
            while (ability.currentWaveObjects.Exists(o => o != null))
            {
                yield return new WaitForSeconds(0.5f);
            }

            if (bossController != null)
                bossController.PauseForSummons(false);
        }
    }

    private void SpawnOne(BossPhasePassiveBehaviors.PhasePassiveAbility ability, Vector2 pos)
    {
        if (ability.spawnPrefab == null) return;

        GameObject spawned = Instantiate(ability.spawnPrefab, pos, Quaternion.identity);

        // track for wave-complete logic
        if (ability.useWaveLimit || ability.waitForWaveComplete)
        {
            ability.currentWaveObjects.Add(spawned);
        }

        // setup common properties
        if (ability.abilityType == BossPhasePassiveBehaviors.PassiveAbilityType.SpawnMinion)
        {
            if (spawned.TryGetComponent<FlyingEnemy>(out var fe))
            {
                fe.chaseMode = FlyingEnemy.ChaseMode.Zone;
                if (passiveConfig != null && passiveConfig.playerTransform != null)
                {
                    fe.OnPlayerEnterZone(passiveConfig.playerTransform.gameObject);
                }
            }
        }

        if (ability.abilityType == BossPhasePassiveBehaviors.PassiveAbilityType.FallingHazard)
        {
            if (spawned.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.gravityScale = 2f;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
        }

        if (ability.autoDestroy && ability.destroyAfter > 0f)
        {
            Destroy(spawned, ability.destroyAfter);
        }

        if (ability.spawnSound != null)
        {
            AudioSource.PlayClipAtPoint(ability.spawnSound, transform.position, ability.soundVolume);
        }
    }
}
