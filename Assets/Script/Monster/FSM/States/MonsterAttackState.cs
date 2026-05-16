using UnityEngine;

/// <summary>
/// 攻击状态 - 怪物对玩家进行攻击
/// </summary>
public class MonsterAttackState : MonsterStateBase
{
    public override MonsterState StateType => MonsterState.Attack;

    private float _attackTimer;

    public override void Enter()
    {
        _attackTimer = owner.AttackCooldown; // 首次帧即可触发攻击（Update中先+=deltaTime再判断）
        if (locomotion != null) locomotion.FaceTarget = player;
    }

    public override void Update()
    {
        _attackTimer += Time.deltaTime;

        // 生成器范围束缚：玩家/怪物出了范围则退出攻击
        if (IsPlayerOutsideSpawnerBounds() || IsOutsideSpawnerBounds(owner.transform.position))
        {
            owner.ChangeState(MonsterState.ReturnToSpawn);
            return;
        }

        if (!owner.UseCustomMovement && navMeshAgent != null)
        {
            navMeshAgent.ResetPath();
        }
        if (player != null && locomotion != null)
        {
            locomotion.FaceTarget = player;
        }

        // 攻击冷却结束 → 执行攻击序列
        if (!owner.IsAttackInProgress && _attackTimer >= owner.AttackCooldown)
        {
            _attackTimer = 0f;
            owner.StartAttackSequence();
        }
    }

    public override void CheckTransitions()
    {
        float distSqr = GetSqrDistanceToPlayer();

        // 使用带缓冲的退出距离，避免边缘来回抖动
        if (distSqr > owner.AttackLeaveRangeSqr)
        {
            owner.ChangeState(MonsterState.Chase);
        }
        else if (player == null)
        {
            owner.ChangeState(MonsterState.ReturnToSpawn);
        }
    }
}
