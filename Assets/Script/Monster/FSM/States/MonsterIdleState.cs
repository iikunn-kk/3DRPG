using UnityEngine;

/// <summary>
/// 待机状态 - 怪物暂停巡逻，等待后恢复巡逻
/// </summary>
public class MonsterIdleState : MonsterStateBase
{
    public override MonsterState StateType => MonsterState.Idle;

    private float _idleTimer;

    public override void Enter()
    {
        _idleTimer = 0f;
        ResetNavMeshPath();
        animController?.PlayIdle();
        if (locomotion != null) locomotion.FaceTarget = null;
    }

    public override void Update()
    {
        if (locomotion != null) locomotion.FaceTarget = null;
        _idleTimer += Time.deltaTime;
        if (_idleTimer >= owner.PatrolPauseDuration)
        {
            owner.ChangeState(MonsterState.Patrol);
        }
    }

    public override void CheckTransitions()
    {
        float distSqr = GetSqrDistanceToPlayer();
        if (distSqr <= owner.AlertRangeSqr)
        {
            owner.ChangeState(MonsterState.Alert);
        }
    }
}
