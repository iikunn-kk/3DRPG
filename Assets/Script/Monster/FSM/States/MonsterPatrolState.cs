using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Random = UnityEngine.Random;

/// <summary>
/// 巡逻状态 - 怪物在生成范围内随机巡逻
/// </summary>
public class MonsterPatrolState : MonsterStateBase
{
    public override MonsterState StateType => MonsterState.Patrol;

    private CancellationTokenSource _patrolCts;
    private bool _isPatrolling;
    private Vector3 _targetPatrolPosition;

    public override void Enter()
    {
        _isPatrolling = false;
        if (locomotion != null) locomotion.FaceTarget = null;
    }

    public override void Update()
    {
        if (locomotion != null) locomotion.FaceTarget = null;
        if (!_isPatrolling)
        {
            SetPatrolTarget();
            CancelAndDisposeCts();
            _patrolCts = CancellationTokenSource.CreateLinkedTokenSource(owner.GetCancellationTokenOnDestroy());
            PatrolRoutineAsync(_patrolCts.Token).Forget();
        }
    }

    public override void Exit()
    {
        CancelAndDisposeCts();
        _isPatrolling = false;
    }

    public override void CheckTransitions()
    {
        float distSqr = GetSqrDistanceToPlayer();
        if (distSqr <= owner.AlertRangeSqr)
        {
            owner.ChangeState(MonsterState.Alert);
        }
    }

    // ==================== 巡逻协程 ====================

    private async UniTaskVoid PatrolRoutineAsync(CancellationToken token)
    {
        try
        {
            _isPatrolling = true;
            while (owner.CurrentStateEnum == MonsterState.Patrol)
            {
                token.ThrowIfCancellationRequested();

                if (!owner.UseCustomMovement && navMeshAgent != null)
                {
                    navMeshAgent.SetDestination(_targetPatrolPosition);
                    navMeshAgent.speed = monsterBase.patrolSpeed;
                }
                else
                {
                    owner.CustomMoveTowards(_targetPatrolPosition, monsterBase.patrolSpeed);
                }

                while ((owner.transform.position - _targetPatrolPosition).sqrMagnitude > 1f)
                {
                    await UniTask.Yield(token);
                    if (owner.CurrentStateEnum != MonsterState.Patrol) { _isPatrolling = false; return; }
                    if (owner.UseCustomMovement)
                    {
                        owner.CustomMoveTowards(_targetPatrolPosition, monsterBase.patrolSpeed);
                    }
                }

                if (Random.value < owner.IdleChance)
                {
                    owner.ChangeState(MonsterState.Idle);
                    return;
                }

                if (!owner.UseCustomMovement && navMeshAgent != null)
                {
                    navMeshAgent.ResetPath();
                }
                animController?.PlayIdle();
                await UniTask.Delay(TimeSpan.FromSeconds(owner.PatrolPauseDuration), cancellationToken: token);
                SetPatrolTarget();
            }
            _isPatrolling = false;
        }
        catch (OperationCanceledException) { }
    }

    private void SetPatrolTarget()
    {
        _targetPatrolPosition = monsterSpawner != null
            ? monsterSpawner.GetRandomPointInBounds()
            : owner.SpawnPosition;
    }

    private void CancelAndDisposeCts()
    {
        if (_patrolCts != null)
        {
            _patrolCts.Cancel();
            _patrolCts.Dispose();
            _patrolCts = null;
        }
    }
}
