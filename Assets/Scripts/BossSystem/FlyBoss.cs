// ...existing code...
using UnityEngine;

public class FlyBoss : StateMachineBehaviour
{
    private Transform player;
    private Transform bossTransform;
    private BossAttackSystem attackSystem;
    private BossAttackAI attackAI;

    private bool hasTriggeredAttack; // ป้องกันการ trigger ซ้ำ

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        bossTransform = animator.transform;
        attackSystem = animator.GetComponent<BossAttackSystem>();
        attackAI = animator.GetComponent<BossAttackAI>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        hasTriggeredAttack = false;
    }

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (player == null || attackSystem == null || bossTransform == null || hasTriggeredAttack) return;

        float moveSpeed = attackSystem.moveSpeed;
        float alignmentThreshold = attackSystem.alignmentThreshold;
        float targetX = player.position.x;

        if (Mathf.Abs(bossTransform.position.x - targetX) > alignmentThreshold)
        {
            Vector3 targetPosition = new Vector3(targetX, bossTransform.position.y, bossTransform.position.z);
            bossTransform.position = Vector3.MoveTowards(bossTransform.position, targetPosition, moveSpeed * Time.deltaTime);
        }
        else
        {
            hasTriggeredAttack = true;

            int attackIndex = animator.GetInteger("AttackIndex");

            // Query AI for triggerName mapped to this index
            string triggerName = null;
            if (attackAI != null)
                triggerName = attackAI.GetTriggerNameForIndex(attackIndex);

            if (!string.IsNullOrEmpty(triggerName))
            {
                animator.SetTrigger(triggerName);
                if (attackAI != null && attackAI.controller != null && attackAI.controller.showDebugLogs)
                    Debug.Log($"[FlyBoss] Triggered {triggerName} for index {attackIndex}");
            }
            else
            {
                // Fallback: if nothing mapped, try legacy names
                if (attackIndex == 1) animator.SetTrigger("FireSpread");
                else if (attackIndex == 2) animator.SetTrigger("FireBeam");
                else animator.SetTrigger("GoToIdle");
            }
        }
    }
}
// ...existing code...