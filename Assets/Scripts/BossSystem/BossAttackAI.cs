using UnityEngine;
using System.Collections;
using System.Collections.Generic;

//[RequireComponent(typeof(BossController), typeof(BossAttackSystem), typeof(Animator))]
public class BossAttackAI : MonoBehaviour
{
    [System.Serializable]
    public class AttackEntry
    {
        public int attackIndex = 1;
        public string triggerName = "FireSpread";
        public float postCooldown = 1.5f;
        public bool enabled = true;
    }

    [System.Serializable]
    public class PhaseAttackSet
    {
        public int phaseIndex = 0;
        public List<AttackEntry> entries = new List<AttackEntry>();
    }

    [Header("Core References")]
    public BossController controller;
    public BossAttackSystem attackSystem;
    public Animator animator;

    [Header("Attack Sequence (editable)")]
    [Tooltip("Define attack order: attackIndex must match Animator/AttackSystem, triggerName must exist in Animator")]
    public List<AttackEntry> attackSequence = new List<AttackEntry>()
    {
        new AttackEntry(){ attackIndex = 1, triggerName = "FireSpread", postCooldown = 1.5f },
        new AttackEntry(){ attackIndex = 2, triggerName = "FireBeam",   postCooldown = 2.0f }
    };

    [Header("Per-Phase Attack Sets")]
    public List<PhaseAttackSet> phaseAttackSets = new List<PhaseAttackSet>();

    [Header("Exhaustion / Stun Settings")]
    [Tooltip("How many full cycles of the attack sequence the boss will perform before entering exhaustion/stun state.")]
    public int cyclesBeforeExhaustion = 2;

    [Tooltip("How long (seconds) the boss stays exhausted/stunned when the threshold is reached.")]
    public float exhaustionDuration = 5f;

    [Header("Inspector Passive Abilities (optional)")]
    [Tooltip("Define passive abilities here if you want the AI to expose them to Animation Events. These will be forwarded to BossPhasePassiveBehaviors for execution.")]
    public BossPhasePassiveBehaviors.PhasePassiveAbility[] passiveAbilitiesFromAI;

    private int sequenceCursor = 0;
    private bool isAttacking = false;
    private bool isInCooldown = false;
    private float cooldownTimer = 0f;
    private bool pausedForSummons = false;

    private int attacksStartedThisCycle = 0;
    private int cycleLength = 0;
    private int cyclesCompleted = 0;
    private bool isExhausted = false;
    private float exhaustionTimer = 0f;

    private List<AttackEntry> currentSequence = null;
    private BossPhasePassiveBehaviors passiveBehaviorsComponent;

    // Variant selection: for ability names that have multiple inspector variants,
    // we keep a shuffled order per ability name and cycle through it so variants
    // are distributed predictably (no immediate repeats until cycle completes).
    private Dictionary<string, int[]> variantOrder = new Dictionary<string, int[]>();
    private Dictionary<string, int> variantPointers = new Dictionary<string, int>();

    void Awake()
    {
        if (controller == null) controller = GetComponent<BossController>();
        if (attackSystem == null) attackSystem = GetComponent<BossAttackSystem>();
        if (animator == null) animator = GetComponent<Animator>();
        passiveBehaviorsComponent = GetComponent<BossPhasePassiveBehaviors>();
    }

    // Ensure we have a shuffled order for a set of N variants for the given ability name
    void EnsureVariantOrder(string abilityName, int count)
    {
        if (count <= 0) return;
        if (variantOrder.ContainsKey(abilityName) && variantOrder[abilityName] != null && variantOrder[abilityName].Length == count)
            return;

        int[] order = new int[count];
        for (int i = 0; i < count; i++) order[i] = i;

        // Fisher-Yates shuffle
        for (int i = count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
        }

        variantOrder[abilityName] = order;
        variantPointers[abilityName] = 0;
    }

    // Get next variant index (0..count-1) for abilityName, advancing the pointer
    int GetNextVariantChoice(string abilityName, int count)
    {
        EnsureVariantOrder(abilityName, count);
        int ptr = variantPointers.ContainsKey(abilityName) ? variantPointers[abilityName] : 0;
        int val = variantOrder[abilityName][ptr % count];
        variantPointers[abilityName] = (ptr + 1) % count;
        return val;
    }

    void Update()
    {
        if (controller == null || attackSystem == null || animator == null) return;
        if (controller.isCurrentlyFalling || controller.isInvincible) return;

        if (isExhausted)
        {
            exhaustionTimer -= Time.deltaTime;
            if (exhaustionTimer <= 0f)
            {
                isExhausted = false;
                cyclesCompleted = 0;
                attacksStartedThisCycle = 0;
                exhaustionTimer = 0f;
                pausedForSummons = false;
                ResetAIState();
                if (controller != null && controller.showDebugLogs)
                    Debug.Log("[AttackAI] Exhaustion ended, resuming attacks");
            }
            return;
        }

        if (pausedForSummons) return;

        if (isInCooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
            {
                isInCooldown = false;
                cooldownTimer = 0f;
                animator.SetTrigger("GoToIdle");
                if (controller.showDebugLogs)
                    Debug.Log("[AttackAI] Cooldown finished -> GoToIdle");
            }
            return;
        }

        if (!isAttacking && (currentSequence != null ? currentSequence.Count > 0 : (attackSequence != null && attackSequence.Count > 0)))
        {
            var seq = currentSequence != null ? currentSequence : attackSequence;
            int tries = 0;
            while (tries < seq.Count)
            {
                var entry = seq[sequenceCursor];
                if (entry.enabled && attackSystem.CanUseAttack(entry.attackIndex))
                {
                    StartAttack(entry.attackIndex);
                    sequenceCursor = (sequenceCursor + 1) % seq.Count;
                    break;
                }
                sequenceCursor = (sequenceCursor + 1) % seq.Count;
                tries++;
            }
        }
    }

    private void StartAttack(int attackIndex)
    {
        isAttacking = true;

        if (animator == null && controller != null)
        {
            animator = controller.GetComponent<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning("[AttackAI] Animator is null when trying to start attack. Aborting StartAttack.");
            isAttacking = false;
            return;
        }

        animator.ResetTrigger("GoToIdle");
        animator.ResetTrigger("FlyToAttack");

        animator.SetInteger("AttackIndex", attackIndex);
        animator.SetTrigger("FlyToAttack");

        if (controller != null && controller.showDebugLogs)
            Debug.Log($"[AttackAI] StartAttack index={attackIndex} -> Animator set AttackIndex={attackIndex} and triggered FlyToAttack");

        var seq = currentSequence != null ? currentSequence : attackSequence;
        if (seq != null && seq.Count > 0)
        {
            int enabledCount = 0;
            foreach (var e in seq) if (e != null && e.enabled) enabledCount++;
            cycleLength = Mathf.Max(1, enabledCount);
        }

        attacksStartedThisCycle++;

        if (attacksStartedThisCycle >= cycleLength)
        {
            attacksStartedThisCycle = 0;
            cyclesCompleted++;
            if (controller != null && controller.showDebugLogs)
                Debug.Log($"[AttackAI] Completed cycle {cyclesCompleted}/{cyclesBeforeExhaustion}");

            if (cyclesBeforeExhaustion > 0 && cyclesCompleted >= cyclesBeforeExhaustion)
            {
                isExhausted = true;
                exhaustionTimer = exhaustionDuration;
                pausedForSummons = true;
                if (animator != null) animator.SetTrigger("GoToIdle");
                if (controller != null && controller.showDebugLogs)
                    Debug.Log($"[AttackAI] Entering exhaustion for {exhaustionDuration} seconds");
            }
        }
    }

    public void TriggerAttackByIndex(int attackIndex)
    {
        if (controller != null && controller.showDebugLogs)
            Debug.Log($"[AttackAI] TriggerAttackByIndex({attackIndex}) called");
        StartAttack(attackIndex);
    }

    public void NotifyAttackComplete(int finishedAttackIndex)
    {
        isAttacking = false;

        float chosenCooldown = 0f;
        var seq = currentSequence != null ? currentSequence : attackSequence;
        var entry = seq != null ? seq.Find(e => e.attackIndex == finishedAttackIndex) : null;
        if (entry != null)
            chosenCooldown = entry.postCooldown;
        else
        {
            if (finishedAttackIndex == 1) chosenCooldown = 1.5f;
            else if (finishedAttackIndex == 2) chosenCooldown = 2.0f;
            else chosenCooldown = 1.0f;
        }

        float multiplier = 1f;
        if (controller != null && controller.phases != null && controller.phases.Length > controller.currentPhaseIndex)
            multiplier = controller.phases[controller.currentPhaseIndex].attackSpeedMultiplier;

        cooldownTimer = Mathf.Max(0f, chosenCooldown / Mathf.Max(0.0001f, multiplier));
        isInCooldown = cooldownTimer > 0f;

        animator.ResetTrigger("FlyToAttack");
        if (controller.showDebugLogs)
            Debug.Log($"[AttackAI] Attack {finishedAttackIndex} finished, cooldown={cooldownTimer:F2}s");
    }

    public void PauseForSummons(bool pause)
    {
        pausedForSummons = pause;
        if (controller != null && controller.showDebugLogs)
        {
            Debug.Log($"[AttackAI] PauseForSummons = {pause}");
        }
    }

    public string GetTriggerNameForIndex(int attackIndex)
    {
        var seq = currentSequence != null ? currentSequence : attackSequence;
        if (seq == null) return null;
        var entry = seq.Find(e => e.attackIndex == attackIndex);
        return entry != null ? entry.triggerName : null;
    }

    public void OnPhaseChanged(int newPhaseIndex)
    {
        sequenceCursor = 0;
        isAttacking = false;
        isInCooldown = false;
        cooldownTimer = 0f;

        attacksStartedThisCycle = 0;
        cyclesCompleted = 0;
        isExhausted = false;
        exhaustionTimer = 0f;

        currentSequence = null;
        var set = phaseAttackSets.Find(s => s.phaseIndex == newPhaseIndex);
        if (set != null) currentSequence = set.entries;

        animator.SetInteger("AttackIndex", 0);
        animator.ResetTrigger("FlyToAttack");
        animator.ResetTrigger("GoToIdle");
        if (controller.showDebugLogs)
            Debug.Log($"[AttackAI] Phase changed to {newPhaseIndex}, reset sequence (loaded {(currentSequence != null ? currentSequence.Count : attackSequence.Count)} entries)");
    }

    public void ResetAIState()
    {
        sequenceCursor = 0;
        isAttacking = false;
        isInCooldown = false;
        cooldownTimer = 0f;
        pausedForSummons = false;

        attacksStartedThisCycle = 0;
        cyclesCompleted = 0;
        isExhausted = false;
        exhaustionTimer = 0f;

        if (animator != null)
        {
            animator.ResetTrigger("FlyToAttack");
            animator.ResetTrigger("GoToIdle");
            animator.SetInteger("AttackIndex", 0);
        }

        if (controller != null && controller.showDebugLogs)
            Debug.Log("[AttackAI] ResetAIState called");
    }

    public void TriggerPassiveFromAnimation(string abilityName)
    {
        if (string.IsNullOrEmpty(abilityName)) return;

        // If the AI has inspector-assigned passive variants, pick one using
        // a shuffled-cycle selection so variants are distributed across uses.
        if (passiveAbilitiesFromAI != null && passiveAbilitiesFromAI.Length > 0)
        {
            // collect indices that match the requested ability name
            List<int> matches = new List<int>();
            for (int i = 0; i < passiveAbilitiesFromAI.Length; i++)
            {
                var a = passiveAbilitiesFromAI[i];
                if (a != null && a.abilityName == abilityName)
                    matches.Add(i);
            }

            if (matches.Count > 0)
            {
                int chosenIndexInMatches = 0;
                if (matches.Count == 1)
                {
                    chosenIndexInMatches = 0;
                }
                else
                {
                    chosenIndexInMatches = GetNextVariantChoice(abilityName, matches.Count);
                }

                int chosenArrayIndex = matches[chosenIndexInMatches];
                var chosenAbility = passiveAbilitiesFromAI[chosenArrayIndex];
                if (chosenAbility != null && passiveBehaviorsComponent != null)
                {
                    var runtimeCopy = chosenAbility.Clone();
                    passiveBehaviorsComponent.ExecuteAbilityOnce(runtimeCopy);
                    if (controller != null && controller.showDebugLogs)
                        Debug.Log($"[AttackAI] ExecuteAbilityOnce (variant #{chosenIndexInMatches}) for AI passive '{abilityName}' -> arrayIndex={chosenArrayIndex}");
                    return;
                }
            }
        }

        if (passiveBehaviorsComponent != null)
        {
            var found = passiveBehaviorsComponent.FindAbilityByName(abilityName);
            if (found != null)
            {
                passiveBehaviorsComponent.ExecuteAbilityOnce(found.Clone());
                if (controller != null && controller.showDebugLogs)
                    Debug.Log($"[AttackAI] ExecuteAbilityOnce fallback for passive '{abilityName}'");
                return;
            }

            if (controller != null && controller.showDebugLogs)
                Debug.LogWarning($"[AttackAI] Passive ability not found anywhere: {abilityName}");
            return;
        }

        if (controller != null && controller.showDebugLogs)
            Debug.LogWarning($"[AttackAI] No passive component present to trigger '{abilityName}'");
    }
}