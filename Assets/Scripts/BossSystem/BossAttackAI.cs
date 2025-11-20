using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;

//[RequireComponent(typeof(BossController), typeof(BossAttackSystem), typeof(Animator))]
public class BossAttackAI : MonoBehaviour
{
    #region Types
    [System.Serializable]
    public class AttackEntry
    {
        public int attackIndex = 1;
        public string triggerName = "FireSpread";
        public float postCooldown = 1.5f;
        public bool enabled = true;
        // [เพิ่มใหม่] ถ้าช่องนี้ว่าง = ท่าโจมตีปกติ, ถ้าใส่ชื่อ = ท่า Passive
        //[Tooltip("Leave empty for normal attacks. If set, checking logic will ask BossPhasePassiveBehaviors instead.")]
        //public string passiveAbilityName = "";
    }

    [System.Serializable]
    public class PhaseAttackSet
    {
        public int phaseIndex = 0;
        public List<AttackEntry> entries = new List<AttackEntry>();
    }
    #endregion

    #region Inspector & Tuning
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
    #endregion

    #region Runtime State
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
    // When an attack finishes we set this flag; sequence index advances only when
    // any pause-for-summons is lifted so waitForWaveComplete abilities can finish.
    private bool attackFinishedPendingAdvance = false;
    #endregion

    #region Variant Selection
    // Variant selection: for ability names that have multiple inspector variants,
    // we keep a shuffled order per ability name and cycle through it so variants
    // are distributed predictably (no immediate repeats until cycle completes).
    private Dictionary<string, int[]> variantOrder = new Dictionary<string, int[]>();
    private Dictionary<string, int> variantPointers = new Dictionary<string, int>();
    #endregion

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

    #region Unity Callbacks

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
                if (entry.enabled && attackSystem.CanUseAttack(entry.attackIndex) /*&& attackFinishedPendingAdvance == false*/)
                {
                    StartAttack(entry.attackIndex);
                    // Do not advance sequenceCursor here. We advance it only after the
                    // attack completes (NotifyAttackComplete) and any active
                    // PauseForSummons has been cleared so wave-based passives finish.
                    break;
                }
                sequenceCursor = (sequenceCursor + 1) % seq.Count;
                tries++;
            }
        }
    }
    #endregion

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

        // Note: attacksStartedThisCycle increment moved to AdvanceSequenceCursor()
        // so a cycle count is only recorded when the attack actually finishes
        // and the sequence advances (prevents counting at attack start).
        //var ability = passiveBehaviorsComponent.FindAbilityByName("CreateObstacle");

        //if (attackFinishedPendingAdvance == false & ability.waitForWaveComplete)
        //{
            if (attacksStartedThisCycle >= cycleLength )
            {
                attacksStartedThisCycle = 0;
                cyclesCompleted++;
                if (controller != null && controller.showDebugLogs)
                    Debug.Log($"[AttackAI] Completed cycle {cyclesCompleted}/{cyclesBeforeExhaustion}");

                if (cyclesBeforeExhaustion > 0 && cyclesCompleted >= cyclesBeforeExhaustion+1)
                {
                    isExhausted = true;
                    exhaustionTimer = exhaustionDuration;
                    pausedForSummons = true;
                    if (animator != null) animator.SetTrigger("GoToIdle");
                    if (controller != null && controller.showDebugLogs)
                        Debug.Log($"[AttackAI] Entering exhaustion for {exhaustionDuration} seconds");
                }
            }
        //}
    }
    #region Attack Flow
    //public void TriggerAttackByIndex(int attackIndex)
    //{
    //    if (controller != null && controller.showDebugLogs)
    //        Debug.Log($"[AttackAI] TriggerAttackByIndex({attackIndex}) called");
    //    StartAttack(attackIndex);
    //}

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

        // Mark that the attack finished; advance the sequence cursor only when
        // we are not paused for summons. If we are paused, PauseForSummons(false)
        // will advance the cursor when the pause is lifted.
        attackFinishedPendingAdvance = true;
        if (!pausedForSummons)
        {
            AdvanceSequenceCursor();
        }
    }

    public void PauseForSummons(bool pause)
    {
        pausedForSummons = pause;
        if (controller != null && controller.showDebugLogs)
        {
            Debug.Log($"[AttackAI] PauseForSummons = {pause}");
        }

        // If a pause is being cleared and an attack finished while we were paused,
        // advance the sequence now so the AI continues to the next attack.
        if (!pause && attackFinishedPendingAdvance)
        {
            AdvanceSequenceCursor();
        }
    }

    void AdvanceSequenceCursor()
    {
        var seq = currentSequence != null ? currentSequence : attackSequence;
        if (seq != null && seq.Count > 0)
        {
            // Advance to next attack entry
            sequenceCursor = (sequenceCursor + 1) % seq.Count;

            // Recalculate cycle length (in case entries changed)
            int enabledCount = 0;
            foreach (var e in seq) if (e != null && e.enabled) enabledCount++;
            cycleLength = Mathf.Max(1, enabledCount);

            // Increment completed-attack counter now that the attack finished
            attacksStartedThisCycle++;

            // If we've completed a full cycle, update counters and possibly exhaust
            if (attacksStartedThisCycle >= cycleLength)
            {
                attacksStartedThisCycle = 0;
                cyclesCompleted++;
                if (controller != null && controller.showDebugLogs)
                    Debug.Log($"[AttackAI] Completed cycle {cyclesCompleted}/{cyclesBeforeExhaustion}");

                if (cyclesBeforeExhaustion > 0 && cyclesCompleted >= cyclesBeforeExhaustion+1)
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

        attackFinishedPendingAdvance = false;
    }
    #endregion

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

    #region Phase & Reset

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
    #endregion

    public void TriggerPassiveFromAnimation(string abilityName)
    {
        if (string.IsNullOrEmpty(abilityName)) return;

        // NOTE: This method is intended to be invoked by Animation Events
        // (add an Animation Event on the relevant animation clip that calls
        // `TriggerPassiveFromAnimation` with the ability name). We avoid calling
        // passive abilities directly from state logic to prevent duplicate
        // execution and potential spawn loops. If other systems need to invoke
        // a passive at runtime, consider calling TriggerAbilityInstance or
        // using the BossPhasePassiveBehaviors API directly.

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