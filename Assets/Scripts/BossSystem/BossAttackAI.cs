// ...existing code...
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(BossController), typeof(BossAttackSystem), typeof(Animator))]
public class BossAttackAI : MonoBehaviour
{
    [Header("Core References")]
    public BossController controller;
    public BossAttackSystem attackSystem;
    public Animator animator;

    [Header("Attack Sequence (editable)")]
    [Tooltip("กำหนดลำดับท่า: attackIndex ต้องตรงกับที่ Animator/AttackSystem ใช้, triggerName ต้องมีใน Animator")]
    public List<AttackEntry> attackSequence = new List<AttackEntry>()
    {
        // ตัวอย่าง default (สามารถแก้/ลบ/เพิ่มใน Inspector)
        new AttackEntry(){ attackIndex = 1, triggerName = "FireSpread", postCooldown = 1.5f },
        new AttackEntry(){ attackIndex = 2, triggerName = "FireBeam",   postCooldown = 2.0f }
    };

    [System.Serializable]
    public class AttackEntry
    {
        public int attackIndex = 1;
        public string triggerName = "FireSpread";
        public float postCooldown = 1.5f;
        public bool enabled = true; // ถ้าต้องการปิดท่านี้ชั่วคราว
    }

    // State tracking
    private int sequenceCursor = 0;
    private bool isAttacking = false;
    private bool isInCooldown = false;
    private float cooldownTimer = 0f;

    void Awake()
    {
        if (controller == null) controller = GetComponent<BossController>();
        if (attackSystem == null) attackSystem = GetComponent<BossAttackSystem>();
        if (animator == null) animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (controller == null || attackSystem == null || animator == null) return;
        if (controller.isCurrentlyFalling || controller.isInvincible) return;

        // cooldown countdown
        if (isInCooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
            {
                isInCooldown = false;
                cooldownTimer = 0f;
                animator.SetTrigger("GoToIdle");
                if (controller.showDebugLogs) Debug.Log("[AttackAI] Cooldown finished -> GoToIdle");
            }
            return; // block queuing while cooldown
        }

        // run sequence loop
        if (!isAttacking && attackSequence != null && attackSequence.Count > 0)
        {
            // advance to next enabled entry
            int tries = 0;
            while (tries < attackSequence.Count)
            {
                var entry = attackSequence[sequenceCursor];
                if (entry.enabled && attackSystem.CanUseAttack(entry.attackIndex))
                {
                    // queue this attack
                    StartAttack(entry.attackIndex);
                    // advance cursor for next time
                    sequenceCursor = (sequenceCursor + 1) % attackSequence.Count;
                    break;
                }
                // skip disabled or unavailable entry
                sequenceCursor = (sequenceCursor + 1) % attackSequence.Count;
                tries++;
            }
        }
    }

    private void StartAttack(int attackIndex)
    {
        isAttacking = true;

        // Clear triggers to avoid conflicts
        animator.ResetTrigger("GoToIdle");
        animator.ResetTrigger("FlyToAttack");
        // Note: do NOT reset specific Fire triggers here because FlyBoss will set correct one

        // set attack type and trigger fly
        animator.SetInteger("AttackIndex", attackIndex);
        animator.SetTrigger("FlyToAttack");

        if (controller.showDebugLogs)
            Debug.Log($"[AttackAI] StartAttack index={attackIndex}");
    }

    /// <summary>
    /// Called by AttackFinishedSMB when animation completes.
    /// AI uses the attackSequence list to pick postCooldown (fallback uses default values)
    /// </summary>
    public void NotifyAttackComplete(int finishedAttackIndex)
    {
        isAttacking = false;

        // find entry to determine post cooldown
        float chosenCooldown = 0f;
        var entry = attackSequence.Find(e => e.attackIndex == finishedAttackIndex);
        if (entry != null) chosenCooldown = entry.postCooldown;
        else
        {
            // fallback: try known defaults
            if (finishedAttackIndex == 1) chosenCooldown = 1.5f;
            else if (finishedAttackIndex == 2) chosenCooldown = 2.0f;
            else chosenCooldown = 1.0f;
        }

        cooldownTimer = Mathf.Max(0f, chosenCooldown);
        isInCooldown = cooldownTimer > 0f;

        // Reset triggers that may remain
        animator.ResetTrigger("FlyToAttack");
        if (controller.showDebugLogs) Debug.Log($"[AttackAI] Attack {finishedAttackIndex} finished, cooldown={cooldownTimer:F2}s");
    }

    /// <summary>
    /// Utility: FlyBoss SMB will call this to get trigger name for the attack index
    /// </summary>
    public string GetTriggerNameForIndex(int attackIndex)
    {
        var entry = attackSequence.Find(e => e.attackIndex == attackIndex);
        return entry != null ? entry.triggerName : null;
    }

    public void OnPhaseChanged(int newPhaseIndex)
    {
        sequenceCursor = 0;
        isAttacking = false;
        isInCooldown = false;
        cooldownTimer = 0f;

        animator.SetInteger("AttackIndex", 0);
        animator.ResetTrigger("FlyToAttack");
        animator.ResetTrigger("GoToIdle");
        if (controller.showDebugLogs) Debug.Log($"[AttackAI] Phase changed to {newPhaseIndex}, reset sequence");
    }
}
// ...existing code...