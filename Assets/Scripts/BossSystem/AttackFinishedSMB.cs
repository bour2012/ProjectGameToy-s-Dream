using UnityEngine;

/// <summary>
/// Attach to Attack animation states to notify AI when animation completes
/// </summary>
public class AttackFinishedSMB : StateMachineBehaviour
{
    private bool hasNotified = false;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        hasNotified = false;
    }

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Wait until animation is complete
        if (hasNotified || stateInfo.normalizedTime < 0.95f) return;

        // Notify AI that attack is complete
        var ai = animator.GetComponent<BossAttackAI>();
        if (ai != null)
        {
            int finishedIndex = animator.GetInteger("AttackIndex");
            ai.NotifyAttackComplete(finishedIndex);
            hasNotified = true;
        }

        // REMOVED: Don't trigger GoToIdle here - let AI handle it after cooldown
        // REMOVED: Don't reset triggers here - causes conflicts
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Clean up attack triggers when leaving state
        animator.ResetTrigger("FireSpread");
        animator.ResetTrigger("FireBeam");
    }
}