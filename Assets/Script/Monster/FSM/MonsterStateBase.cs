using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 怪物状态抽象基类 - 遵循绝区零 FSM 架构模式
/// 所有具体怪物状态应继承此类
/// </summary>
public abstract class MonsterStateBase
{
    /// <summary>当前状态对应的枚举值，用于字典索引</summary>
    public abstract MonsterState StateType { get; }

    // ==================== 组件引用（Init 时缓存） ====================
    protected MonsterStateMachine owner;
    protected Transform player;
    protected NavMeshAgent navMeshAgent;
    protected MonsterBase monsterBase;
    protected MonsterCombat combat;
    protected MonsterAnimationController animController;
    protected MonsterLocomotionDriver locomotion;
    protected MonsterSpawner monsterSpawner;

    /// <summary>
    /// 初始化状态 - 只在状态首次创建时调用一次
    /// 缓存所有需要的组件引用
    /// </summary>
    public virtual void Init(MonsterStateMachine owner)
    {
        this.owner = owner;
        player = owner.PlayerRef;
        navMeshAgent = owner.NavMeshAgentRef;
        monsterBase = owner.MonsterBaseRef;
        combat = owner.CombatRef;
        animController = owner.AnimControllerRef;
        locomotion = owner.LocomotionRef;
        monsterSpawner = owner.MonsterSpawnerRef;
    }

    /// <summary>进入状态 - 每次切换到此状态时调用</summary>
    public virtual void Enter() { }

    /// <summary>每帧更新 - 当前状态的行为逻辑</summary>
    public virtual void Update() { }

    /// <summary>退出状态 - 切换到其他状态前调用，用于清理</summary>
    public virtual void Exit() { }

    /// <summary>释放资源 - 状态机清理时调用</summary>
    public virtual void UnInit() { }

    /// <summary>
    /// 检查状态转换条件 - 每个状态自己决定何时切换到什么状态
    /// 在 MonsterStateMachine.UpdateStateMachine() 中每帧调用
    /// </summary>
    public virtual void CheckTransitions() { }

    // ==================== 帮助方法 ====================

    /// <summary>计算到玩家的平方距离（避免开方）</summary>
    protected float GetSqrDistanceToPlayer()
    {
        if (player == null) return float.MaxValue;
        return (owner.transform.position - player.position).sqrMagnitude;
    }

    /// <summary>玩家是否在生成器范围外</summary>
    protected bool IsPlayerOutsideSpawnerBounds()
    {
        if (monsterSpawner == null) return false;
        if (player == null) return true;
        return !monsterSpawner.IsWithinSpawnBounds(player.position);
    }

    /// <summary>指定位置是否在生成器范围外</summary>
    protected bool IsOutsideSpawnerBounds(Vector3 pos)
    {
        return monsterSpawner != null && !monsterSpawner.IsWithinSpawnBounds(pos);
    }

    /// <summary>重置 NavMeshAgent 路径（如果启用）</summary>
    protected void ResetNavMeshPath()
    {
        if (navMeshAgent != null && navMeshAgent.enabled && !owner.UseCustomMovement)
        {
            navMeshAgent.ResetPath();
        }
    }
}
