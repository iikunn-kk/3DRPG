using UnityEngine;

/// <summary>
/// 返回出生点状态 - 怪物返回出生点
/// </summary>
public class MonsterReturnToSpawnState : MonsterStateBase
{
    public override MonsterState StateType => MonsterState.ReturnToSpawn;

    public override void Enter()
    {
        if (locomotion != null) locomotion.FaceTarget = null;
        ResetNavMeshPath();
    }

    public override void Update()
    {
        if (locomotion != null) locomotion.FaceTarget = null;

        Vector3 spawnPos = owner.SpawnPosition;
        if (!owner.UseCustomMovement && navMeshAgent != null)
        {
            navMeshAgent.SetDestination(spawnPos);
            navMeshAgent.speed = monsterBase.patrolSpeed;
        }
        else
        {
            owner.CustomMoveTowards(spawnPos, monsterBase.patrolSpeed);
        }
    }

    public override void CheckTransitions()
    {
        float distSqr = GetSqrDistanceToPlayer();
        float toSpawnSqr = (owner.transform.position - owner.SpawnPosition).sqrMagnitude;

        // 仅在玩家在生成范围内才重新进入交战
        if (!IsPlayerOutsideSpawnerBounds() && distSqr <= owner.ChaseRangeSqr)
        {
            owner.ChangeState(MonsterState.Chase);
        }
        else if (toSpawnSqr <= owner.ReturnToSpawnRangeSqr)
        {
            owner.ChangeState(MonsterState.Patrol);
        }
    }
}
