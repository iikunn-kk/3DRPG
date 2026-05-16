using UnityEngine;

/// <summary>
/// 死亡状态 - 最终状态，不再进行任何行为逻辑
/// </summary>
public class MonsterDeathState : MonsterStateBase
{
    public override MonsterState StateType => MonsterState.Death;

    public override void Enter()
    {
        // 彻底停止一切行为
        owner.CancelAndDisposeStateCts();
        owner.AlertIcon?.SetActive(false);
        owner.AttackIcon?.SetActive(false);
        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            navMeshAgent.ResetPath();
        }
        // 明确清除面向并禁用旋转
        if (locomotion != null)
        {
            locomotion.FaceTarget = null;
            locomotion.enableRotation = false;
        }
        if (combat != null)
        {
            combat.ExecuteDeathSequence();
        }
    }

    // 死亡状态不执行任何更新和转换
}
