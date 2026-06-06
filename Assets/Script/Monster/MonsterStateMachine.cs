using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// 怪物状态机管理类 - 重构为分布式状态类 FSM
/// 保留所有配置字段和公开 API，内部 switch-case 替换为状态类 Dictionary 分发
/// </summary>
public class MonsterStateMachine : MonoBehaviour
{
    // ==================== 序列化字段（保持原样，供 Inspector 配置） ====================

    [Header("状态机设置")]
    public MonsterState currentState;          // 当前状态

    [Header("状态参数")]
    [Tooltip("发现玩家的距离")]
    [SerializeField] private float alertRange = 15f;        // 警觉范围
    public float chaseDuration = 10f;                      // 追击最大持续时间
    [Header("攻击设置")]
    [SerializeField] protected float attackRange = 2f;      // 攻击范围
    [SerializeField] protected float attackCooldown = 3f;   // 攻击冷却时间
    [Header("追击距离")]
    [SerializeField] protected float chaseRange = 10f;      // 追击范围
    [Header("返回出生点的判定距离")]
    [SerializeField] protected float returnToSpawnRange = 1f; // 返回出生点的判定距离
    [Header("巡逻逻辑")]
    [SerializeField] protected float patrolPauseDuration = 2f; // 巡逻暂停时长
    [SerializeField]
    [Tooltip("进入Idle状态的几率")]
    private float idleChance = 0.2f;                       // 进入空闲状态的几率

    [Header("UI & FX")]
    [SerializeField] private GameObject alertIcon;         // 警觉状态图标
    [SerializeField] private GameObject attackIcon;        // 攻击状态图标

    // 使用平方距离比较以提高性能
    private float attackRangeSqr;              // 攻击范围的平方
    private float chaseRangeSqr;               // 追击范围的平方
    private float alertRangeSqr;               // 警觉范围的平方
    private float returnToSpawnRangeSqr;       // 返回出生点范围的平方
    private float attackLeaveRangeSqr;         // 缓冲后的退出攻击距离平方

    private Vector3 spawnPosition;             // 出生点位置
    private Transform player;                  // 玩家Transform引用
    private UnityEngine.AI.NavMeshAgent navMeshAgent; // 导航网格代理
    private bool isPlayerInRange;              // 玩家是否在范围内
    private bool isDead = false;               // 是否已死亡
    private bool _playerDeadCelebrating;       // 玩家死亡庆祝状态

    private MonsterSpawner monsterSpawner;     // 怪物生成器引用
    private MonsterAnimationController _animController; // 动画管理器引用
    private MonsterLocomotionDriver _locomotion; // 新增：驱动模型朝向与动画 V/H

    private MonsterBase monsterBase;           // 怪物基础组件引用
    private MonsterCombat _combat;             // 怪物战斗组件引用
    private CancellationTokenSource _stateCts; // 状态协程取消令牌（用于攻击序列）

    [Header("攻击高级设置")]
    [SerializeField][Tooltip("是否开启必中攻击（攻击发动后即锁定目标，后续不再检测距离）")] private bool guaranteedHit = true;
    [SerializeField][Tooltip("攻击动画前摇秒数（无需动画事件，通过代码延迟造成伤害）")] private float attackWindup = 0.15f;
    [SerializeField][Tooltip("前摇期间若目标死亡则取消伤害")] private bool cancelIfTargetDeadDuringWindup = true;
    [SerializeField][Tooltip("即使脱离一定距离仍然命中（开启必中时有效）")] private bool ignoreDistanceAfterLock = true;
    [SerializeField][Tooltip("攻击动画一触发立刻结算伤害（忽略前摇, 适用于必中锁定攻击）")] private bool immediateDamageOnAttackStart = true;
    [SerializeField][Tooltip("离开攻击状态的额外缓冲距离 (避免刚进入攻击又切回追击)")] private float attackLeaveBuffer = 0.75f;
    [SerializeField][Tooltip("追击/攻击时朝向玩家的旋转速度")] private float facePlayerRotationSpeed = 10f;

    [Header("移动替代方案 (避免互相阻挡)")]
    [SerializeField][Tooltip("不使用NavMeshAgent，使用简单 steering + 分离算法移动")] private bool useCustomMovement = false;
    [SerializeField][Tooltip("自定义移动：怪物之间的分离半径")] private float separationRadius = 1.0f;
    [SerializeField][Tooltip("自定义移动：分离力强度")] private float separationForce = 2.5f;
    [SerializeField][Tooltip("自定义移动：最大水平速度")] private float customMaxSpeed = 4f;
    [SerializeField][Tooltip("OverlapSphereNonAlloc 的缓存大小")] private int overlapBufferSize = 24;

    private bool _attackInProgress;           // 是否正在前摇或结算
    private IDamageable _cachedAttackTarget;  // 已锁定的伤害对象（必中时使用）

    // 自定义移动所需的临时向量缓存
    private Vector3 _customVelocity;
    private Collider[] _overlapBuffer; // 非分配物理查询缓存

    // ==================== 状态类管理（新增） ====================

    private Dictionary<MonsterState, MonsterStateBase> _stateDict;
    private MonsterStateBase _currentStateInstance;

    /// <summary>
    /// 初始化并注册所有状态类
    /// </summary>
    private void InitializeStates()
    {
        _stateDict = new Dictionary<MonsterState, MonsterStateBase>(8);

        // 注册所有状态，Init() 时会缓存组件引用
        RegisterState(new MonsterIdleState());
        RegisterState(new MonsterPatrolState());
        RegisterState(new MonsterAlertState());
        RegisterState(new MonsterChaseState());
        RegisterState(new MonsterAttackState());
        RegisterState(new MonsterReturnToSpawnState());
        RegisterState(new MonsterDeathState());
    }

    private void RegisterState(MonsterStateBase state)
    {
        state.Init(this);
        _stateDict[state.StateType] = state;
    }

    // ==================== 公开访问器（供状态类使用） ====================

    public Transform PlayerRef => player;
    public UnityEngine.AI.NavMeshAgent NavMeshAgentRef => navMeshAgent;
    public MonsterBase MonsterBaseRef => monsterBase;
    public MonsterCombat CombatRef => _combat;
    public MonsterAnimationController AnimControllerRef => _animController;
    public MonsterLocomotionDriver LocomotionRef => _locomotion;
    public MonsterSpawner MonsterSpawnerRef => monsterSpawner;

    public Vector3 SpawnPosition => spawnPosition;
    public MonsterState CurrentStateEnum => currentState;
    public bool IsPlayerInRangeFlag => isPlayerInRange;
    public bool IsAttackInProgress => _attackInProgress;
    public bool UseCustomMovement => useCustomMovement;

    // 序列化配置值
    public float PatrolPauseDuration => patrolPauseDuration;
    public float IdleChance => idleChance;
    public float AttackCooldown => attackCooldown;
    public float ChaseDuration => chaseDuration;

    // 平方距离值
    public float AlertRangeSqr => alertRangeSqr;
    public float ChaseRangeSqr => chaseRangeSqr;
    public float AttackRangeSqr => attackRangeSqr;
    public float AttackLeaveRangeSqr => attackLeaveRangeSqr;
    public float ReturnToSpawnRangeSqr => returnToSpawnRangeSqr;

    // UI 图标
    public GameObject AlertIcon => alertIcon;
    public GameObject AttackIcon => attackIcon;

    // ==================== 对外状态查询/控制（供 Spawner 使用，保持原样） ====================

    /// <summary>
    /// 是否处于与玩家的"交战"相关状态（Alert/Chase/Attack）
    /// </summary>
    public bool IsEngaged => currentState == MonsterState.Alert || currentState == MonsterState.Chase || currentState == MonsterState.Attack;

    /// <summary>
    /// 是否在出生点附近（用 returnToSpawnRange 判定）
    /// </summary>
    public bool IsNearSpawn => (transform.position - spawnPosition).sqrMagnitude <= returnToSpawnRangeSqr;

    /// <summary>
    /// 强制切换为返回出生点状态
    /// </summary>
    public void ForceReturnToSpawn()
    {
        if (currentState != MonsterState.Death)
        {
            ChangeState(MonsterState.ReturnToSpawn);
        }
    }

    // ==================== 内部帮助方法（保持原样） ====================

    public bool IsOutsideSpawnerBounds(Vector3 pos)
    {
        return monsterSpawner != null && !monsterSpawner.IsWithinSpawnBounds(pos);
    }

    public bool IsPlayerOutsideSpawnerBounds()
    {
        if (monsterSpawner == null) return false;
        if (player == null) return true;
        return !monsterSpawner.IsWithinSpawnBounds(player.position);
    }

    public void CancelAndDisposeStateCts()
    {
        if (_stateCts != null)
        {
            _stateCts.Cancel();
            _stateCts.Dispose();
            _stateCts = null;
        }
    }

    // ==================== Unity 生命周期 ====================

    private void Awake()
    {
        monsterBase = GetComponent<MonsterBase>();
        _combat = GetComponent<MonsterCombat>();
        _locomotion = GetComponent<MonsterLocomotionDriver>();
        if (_locomotion != null)
        {
            _locomotion.enableRotation = !useCustomMovement;
        }
        overlapBufferSize = Mathf.Clamp(overlapBufferSize, 8, 256);
        _overlapBuffer = new Collider[overlapBufferSize];

        // 初始化取消令牌源（用于攻击序列取消）
        _stateCts = new CancellationTokenSource();

        // 初始化状态字典
        InitializeStates();
    }

    public void Initialize(Vector3 spawnPos, Transform playerTransform, UnityEngine.AI.NavMeshAgent agent,
                          MonsterSpawner spawner, MonsterAnimationController animController)
    {
        spawnPosition = spawnPos;
        player = playerTransform;
        navMeshAgent = agent;
        monsterSpawner = spawner;
        _animController = animController;
        if (_locomotion == null) _locomotion = GetComponent<MonsterLocomotionDriver>();
        if (useCustomMovement && navMeshAgent != null) navMeshAgent.enabled = false;
        attackRangeSqr = attackRange * attackRange;
        chaseRangeSqr = chaseRange * chaseRange;
        alertRangeSqr = alertRange * alertRange;
        returnToSpawnRangeSqr = returnToSpawnRange * returnToSpawnRange;
        attackLeaveRangeSqr = (attackRange + attackLeaveBuffer) * (attackRange + attackLeaveBuffer);
        _animController?.PlayIdle();
        alertIcon?.SetActive(false);
        attackIcon?.SetActive(false);
        // 统一设置靠近玩家的停止距离
        if (navMeshAgent != null && !useCustomMovement)
        {
            navMeshAgent.stoppingDistance = Mathf.Max(0.1f, attackRange * 0.85f);
            navMeshAgent.updateRotation = false;
            navMeshAgent.autoBraking = true;
        }
        if (_locomotion != null)
        {
            _locomotion.enableRotation = !useCustomMovement;
            _locomotion.FaceTarget = null;
        }
        // 重新初始化所有状态实例，更新缓存的组件引用（player/navMeshAgent等在 Awake 时还是 null）
        ReinitializeStates();
        // 初始状态通过状态类系统设置
        ChangeState(MonsterState.Patrol);
    }

    /// <summary>
    /// 重新初始化所有已注册的状态实例，刷新缓存的组件引用
    /// </summary>
    private void ReinitializeStates()
    {
        if (_stateDict == null) return;
        foreach (var state in _stateDict.Values)
        {
            state.Init(this);
        }
    }

    // ==================== 帧更新（主入口） ====================

    public void UpdateStateMachine()
    {
        // MMO 模式：AI 由服务器驱动，客户端只从快照同步状态和位置
        if (GameModeConfig.IsMmoMode)
        {
            if (navMeshAgent != null && navMeshAgent.enabled)
                navMeshAgent.ResetPath(); // 清除残留寻路目标，防止与快照位置冲突
            return;
        }

        if (isDead && currentState == MonsterState.Death) return;
        if (isDead && currentState != MonsterState.Death)
        {
            ChangeState(MonsterState.Death);
            return;
        }

        // 0. 玩家死亡 → 战斗中的怪物立即回出生点
        var ps = monsterBase.PlayerState;
        if (ps != null && ps.IsDead)
        {
            if (currentState == MonsterState.Alert || currentState == MonsterState.Chase || currentState == MonsterState.Attack)
            {
                ChangeState(MonsterState.ReturnToSpawn);
                return;
            }
        }

        // 1. 全局转换检查：生成器范围束缚
        if (IsPlayerOutsideSpawnerBounds())
        {
            if (currentState == MonsterState.Alert || currentState == MonsterState.Chase || currentState == MonsterState.Attack)
            {
                ChangeState(MonsterState.ReturnToSpawn);
                return;
            }
        }

        // 2. 状态自身行为更新
        _currentStateInstance?.Update();

        // 3. 状态自身转换条件检查
        if (currentState != MonsterState.Death)
        {
            _currentStateInstance?.CheckTransitions();
        }
    }

    // ==================== 状态切换（核心） ====================

    /// <summary>
    /// 切换到新状态 - 调用旧状态 Exit → 记录新状态 → 调用新状态 Enter
    /// </summary>
    public void ChangeState(MonsterState newState)
    {
        if (currentState == newState) return;

        _currentStateInstance?.Exit();
        currentState = newState;
        _currentStateInstance = _stateDict[newState];
        _currentStateInstance.Enter();
    }

    // ==================== 外部接口（保持原样） ====================

    public void SetPlayerInRange(bool inRange) => isPlayerInRange = inRange;

    public void SetDead(bool dead)
    {
        if (dead && !isDead)
        {
            isDead = true;
            ChangeState(MonsterState.Death);
        }
    }

    public void AnimationAttackHit() => PerformAttack();

    public void ForceAggroToPlayer()
    {
        if (isDead || player == null) return;
        float distanceSqr = (transform.position - player.position).sqrMagnitude;
        if (distanceSqr <= attackRangeSqr) ChangeState(MonsterState.Attack);
        else if (distanceSqr <= chaseRangeSqr) ChangeState(MonsterState.Chase);
        else if (distanceSqr <= alertRangeSqr) ChangeState(MonsterState.Alert);
    }

    public void OnPlayerDeadVictory(GameObject playerObj)
    {
        if (isDead || _playerDeadCelebrating || (transform.position - playerObj.transform.position).sqrMagnitude >= 25f) return;
        _playerDeadCelebrating = true;
        if (!useCustomMovement && navMeshAgent != null)
        {
            navMeshAgent.ResetPath();
        }
        attackIcon?.SetActive(false);
        alertIcon?.SetActive(false);
        _animController?.PlayCelebrate();
    }

    public void OnPlayerRespawn(Transform newPlayer)
    {
        if (isDead) return;
        player = newPlayer;
        _playerDeadCelebrating = false;
        ReinitializeStates(); // 玩家引用变化，刷新状态实例缓存
        ChangeState(MonsterState.Patrol);
    }

    // ==================== 攻击系统（保持原样，供 AttackState 调用） ====================

    /// <summary>
    /// 由 MonsterAttackState 调用，开始攻击序列
    /// </summary>
    public void StartAttackSequence()
    {
        AttackSequenceAsync(_stateCts != null ? _stateCts.Token : this.GetCancellationTokenOnDestroy()).Forget();
    }

    private void PerformAttack()
    {
        if (player == null || isDead) return;
        int attackDamage = 1;
        if (monsterBase != null && monsterBase.monsterData != null)
        {
            attackDamage = Mathf.Max(0, monsterBase.monsterData.damage);
        }
        else if (_combat != null)
        {
            attackDamage = Mathf.Max(1, _combat.GetDamageValue());
        }
        var dmgable2 = player.GetComponent<IDamageable>();
        if (dmgable2 != null)
        {
            dmgable2.TakeDamage(attackDamage, AttackType.物理攻击);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Monster.PerformAttack] (LEGACY) 对玩家造成伤害 damage={attackDamage} 玩家当前血量={(dmgable2.CurrentHealth)} ");
#endif
            return;
        }
        var characterState = player.GetComponent<CharacterState>();
        if (characterState is IDamageable asDmg)
        {
            asDmg.TakeDamage(attackDamage, AttackType.物理攻击);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Monster.PerformAttack] (LEGACY-Fallback) 伤害 damage={attackDamage} 玩家血量={(asDmg.CurrentHealth)} ");
#endif
        }
    }

    private async UniTaskVoid AttackSequenceAsync(CancellationToken token)
    {
        try
        {
            _attackInProgress = true;
            _cachedAttackTarget = AcquireTargetDamageable();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Monster.AttackSequence] 锁定目标={_cachedAttackTarget?.ToString() ?? "null"} windup={attackWindup}s guaranteedHit={guaranteedHit} immediate={immediateDamageOnAttackStart}");
#endif
            _animController?.PlayAttack();
            // 立刻伤害：忽略前摇
            if (immediateDamageOnAttackStart && _cachedAttackTarget != null && _cachedAttackTarget.CurrentHealth > 0)
            {
                ApplyLockedDamage();
                await UniTask.Yield(token);
                _attackInProgress = false;
                _cachedAttackTarget = null;
                return;
            }
            // 非立刻：执行前摇
            if (!immediateDamageOnAttackStart && attackWindup > 0f)
            {
                float t = 0f;
                while (t < attackWindup)
                {
                    token.ThrowIfCancellationRequested();
                    if (cancelIfTargetDeadDuringWindup && (_cachedAttackTarget == null || _cachedAttackTarget.CurrentHealth <= 0))
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log("[Monster.AttackSequence] 目标在前摇中死亡, 取消伤害");
#endif
                        _attackInProgress = false;
                        return;
                    }
                    t += Time.deltaTime;
                    await UniTask.Yield(token);
                }
            }
            if (guaranteedHit && _cachedAttackTarget != null && !isDead)
            {
                ApplyLockedDamage();
            }
            _attackInProgress = false;
            _cachedAttackTarget = null;
        }
        catch (OperationCanceledException) { }
    }

    private void ApplyLockedDamage()
    {
        int dmg = 1;
        if (monsterBase != null && monsterBase.monsterData != null) dmg = Mathf.Max(0, monsterBase.monsterData.damage);
        else if (_combat != null) dmg = Mathf.Max(1, _combat.GetDamageValue());
        if (!ignoreDistanceAfterLock && player != null)
        {
            var comp = _cachedAttackTarget as Component;
            if (comp != null)
            {
                float ds = (transform.position - comp.transform.position).sqrMagnitude;
                if (ds > attackRangeSqr)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[Monster.ApplyLockedDamage] 距离超出取消 ds={ds} rangeSqr={attackRangeSqr}");
#endif
                    return;
                }
            }
        }
        _cachedAttackTarget?.TakeDamage(dmg, AttackType.物理攻击);
    }

    private IDamageable AcquireTargetDamageable()
    {
        if (player == null) return null;
        var dmgable = player.GetComponent<IDamageable>();
        if (dmgable != null) return dmgable;
        var cs = player.GetComponent<CharacterState>();
        if (cs is IDamageable asDmg) return asDmg;
        return null;
    }

    // ==================== 自定义移动系统（保持原样） ====================

    public void CustomMoveTowards(Vector3 targetPos, float speed)
    {
        Vector3 toTarget = targetPos - transform.position;
        toTarget.y = 0f;
        Vector3 desire = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized * speed : Vector3.zero;

        // 分离（NonAlloc）
        Vector3 separation = Vector3.zero;
        int count = Physics.OverlapSphereNonAlloc(transform.position, separationRadius, _overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        int valid = 0;
        for (int i = 0; i < count; i++)
        {
            var c = _overlapBuffer[i];
            if (c == null) continue;
            var other = c.GetComponentInParent<MonsterBase>();
            if (other == null) continue;
            if (other.transform == transform) continue;
            Vector3 diff = transform.position - other.transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;
            if (dist < 0.001f) continue;
            separation += diff.normalized * (1f - Mathf.Clamp01(dist / separationRadius));
            valid++;
        }
        if (valid > 0)
        {
            separation /= valid;
            separation *= separationForce;
        }
        Vector3 accel = desire + separation;
        _customVelocity += accel * Time.deltaTime;
        _customVelocity = Vector3.ClampMagnitude(_customVelocity, customMaxSpeed);
        if (_customVelocity.sqrMagnitude > 0.0001f)
        {
            transform.position += _customVelocity * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(_customVelocity.normalized), 10f * Time.deltaTime);
        }
        else
        {
            _customVelocity = Vector3.zero;
        }
    }
}
