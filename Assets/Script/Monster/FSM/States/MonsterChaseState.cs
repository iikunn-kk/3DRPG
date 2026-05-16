using UnityEngine;

/// <summary>
/// 追击状态 - 怪物追击玩家
/// </summary>
public class MonsterChaseState : MonsterStateBase
{
    public override MonsterState StateType => MonsterState.Chase;

    private float _chaseTimer;

    public override void Enter()
    {
        _chaseTimer = 0f;
        owner.AttackIcon?.SetActive(true);
        if (locomotion != null) locomotion.FaceTarget = player;
    }

    public override void Update()
    {
        _chaseTimer += Time.deltaTime;

        // 生成器范围束缚：玩家在生成范围外时立刻回程
        if (IsPlayerOutsideSpawnerBounds())
        {
            owner.ChangeState(MonsterState.ReturnToSpawn);
            return;
        }

        if (player != null)
        {
            // 若自身已经跑出生成范围，直接回程
            if (IsOutsideSpawnerBounds(owner.transform.position))
            {
                owner.ChangeState(MonsterState.ReturnToSpawn);
                return;
            }

            if (!owner.UseCustomMovement && navMeshAgent != null)
            {
                navMeshAgent.SetDestination(player.position);
                navMeshAgent.speed = monsterBase.chaseSpeed;
            }
            else
            {
                owner.CustomMoveTowards(player.position, monsterBase.chaseSpeed);
            }
            if (locomotion != null) locomotion.FaceTarget = player;
        }
    }

    public override void Exit()
    {
        owner.AttackIcon?.SetActive(false);
    }

    public override void CheckTransitions()
    {
        float distSqr = GetSqrDistanceToPlayer();

        // 追击超时且玩家不在范围内 → 返回
        if (!owner.IsPlayerInRangeFlag && _chaseTimer >= owner.ChaseDuration)
        {
            owner.ChangeState(MonsterState.ReturnToSpawn);
            return;
        }
        // 进入攻击范围 → 攻击
        if (distSqr <= owner.AttackRangeSqr)
        {
            owner.ChangeState(MonsterState.Attack);
        }
    }
}
