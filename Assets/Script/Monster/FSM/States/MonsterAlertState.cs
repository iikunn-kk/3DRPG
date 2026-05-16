using UnityEngine;

/// <summary>
/// 警觉状态 - 面向玩家并显示警觉特效
/// </summary>
public class MonsterAlertState : MonsterStateBase
{
    public override MonsterState StateType => MonsterState.Alert;

    public override void Enter()
    {
        owner.AlertIcon?.SetActive(true);
        animController?.PlayAlert();
        ResetNavMeshPath();
        if (locomotion != null) locomotion.FaceTarget = player;
    }

    public override void Update()
    {
        if (locomotion != null) locomotion.FaceTarget = player;
        ResetNavMeshPath();
    }

    public override void Exit()
    {
        owner.AlertIcon?.SetActive(false);
    }

    public override void CheckTransitions()
    {
        float distSqr = GetSqrDistanceToPlayer();
        if (distSqr <= owner.AttackRangeSqr)
        {
            owner.ChangeState(MonsterState.Attack);
        }
        else if (distSqr <= owner.ChaseRangeSqr)
        {
            owner.ChangeState(MonsterState.Chase);
        }
        else if (distSqr > owner.AlertRangeSqr)
        {
            owner.ChangeState(MonsterState.Patrol);
        }
    }
}
