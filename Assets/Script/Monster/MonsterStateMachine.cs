using UnityEngine;
using System.Collections;

/// <summary>
/// 怪物状态机管理类，负责处理怪物的各种状态转换和状态更新逻辑
/// </summary>
public class MonsterStateMachine : MonoBehaviour
{
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
    [SerializeField] [Tooltip("进入Idle状态的几率")] 
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
    private Vector3 targetPatrolPosition;      // 目标巡逻位置
    private Transform player;                  // 玩家Transform引用
    private UnityEngine.AI.NavMeshAgent navMeshAgent; // 导航网格代理
    private float chaseTimer;                  // 追击计时器
    private float idleTimer;                   // 空闲计时器
    private float attackTimer;                 // 攻击计时器
    private bool isPlayerInRange;              // 玩家是否在范围内
    private bool isPatrolling;                 // 是否正在巡逻
    private bool isDead = false;               // 是否已死亡
    private bool _playerDeadCelebrating;       // 玩家死亡庆祝状态

    private MonsterSpawner monsterSpawner;     // 怪物生成器引用
    private MonsterAnimationController _animController; // 动画管理器引用
    private MonsterLocomotionDriver _locomotion; // 新增：驱动模型朝向与动画 V/H
    
    private MonsterBase monsterBase;           // 怪物基础组件引用
    private MonsterCombat _combat;             // 怪物战斗组件引用
    
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

    private Vector3 _lastRandomPatrol; // 新增：调试用记录

    //== 新增：对外状态查询/控制（供 Spawner 使用） ==
    /// <summary>
    /// 是否处于与玩家的“交战”相关状态（Alert/Chase/Attack）
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

    //== 内部帮助方法：生成器范围束缚 ==
    private bool IsOutsideSpawnerBounds(Vector3 pos)
    {
        return monsterSpawner != null && !monsterSpawner.IsWithinSpawnBounds(pos);
    }

    private bool IsPlayerOutsideSpawnerBounds()
    {
        if (monsterSpawner == null) return false;
        if (player == null) return true;
        return !monsterSpawner.IsWithinSpawnBounds(player.position);
    }

    // 提前定义：设置巡逻目标点
    private void SetPatrolTarget()
    {
        targetPatrolPosition = monsterSpawner != null ? monsterSpawner.GetRandomPointInBounds() : spawnPosition;
        _lastRandomPatrol = targetPatrolPosition;
    }

    // 提前定义：Alert 状态逻辑
    private void UpdateAlertState()
    {
        // 朝向由 _locomotion 控制：这里只需要声明面向目标
        if (_locomotion != null) _locomotion.FaceTarget = player;
        if (!useCustomMovement && navMeshAgent != null)
        {
            navMeshAgent.ResetPath();
        }
    }

    private void Awake()
    {
        monsterBase = GetComponent<MonsterBase>();
        _combat = GetComponent<MonsterCombat>();
        _locomotion = GetComponent<MonsterLocomotionDriver>();
        if (_locomotion != null)
        {
            // 自定义移动时由本类控制旋转，避免与驱动器冲突
            _locomotion.enableRotation = !useCustomMovement;
        }
        // 初始化非分配缓存
        overlapBufferSize = Mathf.Clamp(overlapBufferSize, 8, 256);
        _overlapBuffer = new Collider[overlapBufferSize];
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
        currentState = MonsterState.Patrol;
        SetPatrolTarget(); // 直接调用本地已定义方法
        _animController?.PlayIdle();
        alertIcon?.SetActive(false);
        attackIcon?.SetActive(false);
        // 新增：统一设置靠近玩家的停止距离，略小于攻击距离，防止到达点后再往回拉
        if (navMeshAgent != null && !useCustomMovement)
        {
            navMeshAgent.stoppingDistance = Mathf.Max(0.1f, attackRange * 0.85f);
            navMeshAgent.updateRotation = false; // 关闭自动旋转，使用我们手动/驱动器的朝向避免抖动
            navMeshAgent.autoBraking = true;     // 逐渐减速，更平滑
        }
        if (_locomotion != null)
        {
            _locomotion.enableRotation = !useCustomMovement;
            // 初始不强制面向任何目标（巡逻靠速度方向）
            _locomotion.FaceTarget = null;
        }
    }
    
    public void UpdateStateMachine()
    {
        if (isDead && currentState == MonsterState.Death) return; // 死亡状态不再处理任何逻辑
        if (isDead && currentState != MonsterState.Death)
        {
            // 兜底：若已死亡但状态未及时切换
            ChangeState(MonsterState.Death);
            return;
        }
        
        UpdateTimers();
        UpdateState();
        CheckStateTransitions();
    }
    
    /// <summary>
    /// 更新计时器
    /// </summary>
    private void UpdateTimers()
    {
        if (currentState == MonsterState.Chase)
        {
            chaseTimer += Time.deltaTime;
        }
        attackTimer += Time.deltaTime;
    }
    
    /// <summary>
    /// 更新当前状态的行为
    /// </summary>
    private void UpdateState()
    {
        switch (currentState)
        {
            case MonsterState.Idle:
                UpdateIdleState();
                break;
            case MonsterState.Patrol:
                UpdatePatrolState();
                break;
            case MonsterState.Alert:
                UpdateAlertState();
                break;
            case MonsterState.Chase:
                UpdateChaseState();
                break;
            case MonsterState.Attack:
                UpdateAttackState();
                break;
            case MonsterState.ReturnToSpawn:
                UpdateReturnToSpawnState();
                break;
            case MonsterState.Death:
                // 死亡状态不执行任何更新逻辑
                break;
        }
    }
    
    private void UpdateIdleState()
    {
        // Idle状态下播放Idle动画
        _animController?.PlayIdle();
        // 面向清空
        if (_locomotion != null) _locomotion.FaceTarget = null;
        
        // 计时
        idleTimer += Time.deltaTime;
        if (idleTimer >= patrolPauseDuration)
        {
            ChangeState(MonsterState.Patrol);
        }
    }
    
    private void UpdateChaseState()
    {
        // 生成器范围束缚：玩家在生成范围外时立刻回程
        if (IsPlayerOutsideSpawnerBounds())
        {
            ChangeState(MonsterState.ReturnToSpawn);
            return;
        }
        if (player != null)
        {
            if (!useCustomMovement && navMeshAgent != null)
            {
                // 若自身已经跑出生成范围，优先回程，避免站桩播放移动
                if (IsOutsideSpawnerBounds(transform.position))
                {
                    navMeshAgent.SetDestination(spawnPosition);
                }
                else
                {
                    navMeshAgent.SetDestination(player.position);
                }
                navMeshAgent.speed = monsterBase.chaseSpeed;
            }
            else
            {
                // 自定义移动下：若出界则朝出生点
                if (IsOutsideSpawnerBounds(transform.position))
                {
                    CustomMoveTowards(spawnPosition, monsterBase.chaseSpeed);
                }
                else
                {
                    CustomMoveTowards(player.position, monsterBase.chaseSpeed);
                }
            }
            // 朝向由驱动器控制：指定目标
            if (_locomotion != null) _locomotion.FaceTarget = player;

        }
    }
    
    private void UpdatePatrolState()
    {
        if (_locomotion != null) _locomotion.FaceTarget = null; // 面向按速度
        if (!isPatrolling)
        {
            StartCoroutine(PatrolRoutine());
        }
    }
    
    private IEnumerator PatrolRoutine()
    {
        isPatrolling = true;
        while (currentState == MonsterState.Patrol)
        {
            if (!useCustomMovement && navMeshAgent != null)
            {
                navMeshAgent.SetDestination(targetPatrolPosition);
                navMeshAgent.speed = monsterBase.patrolSpeed;
            }
            else
            {
                CustomMoveTowards(targetPatrolPosition, monsterBase.patrolSpeed);
            }

            while ((transform.position - targetPatrolPosition).sqrMagnitude > 1f)
            {
                yield return null;
                if (currentState != MonsterState.Patrol) { isPatrolling = false; yield break; }
                if (useCustomMovement)
                {
                    CustomMoveTowards(targetPatrolPosition, monsterBase.patrolSpeed);
                }
            }
            if (Random.value < idleChance)
            {
                ChangeState(MonsterState.Idle);
                yield break;
            }
            if (!useCustomMovement && navMeshAgent != null) navMeshAgent.ResetPath();
            _animController?.PlayIdle();
            yield return new WaitForSeconds(patrolPauseDuration);
            SetPatrolTarget();
        }
        isPatrolling = false;
    }

    // 自定义移动 + 分离实现（使用 NonAlloc + 通过组件识别怪物，避免 Tag 依赖）
    private void CustomMoveTowards(Vector3 targetPos, float speed)
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
            // 自定义移动：本类负责旋转，驱动器旋转关闭
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(_customVelocity.normalized), 10f * Time.deltaTime);
        }
        else
        {
            // 慢速时衰减速度，防止误差积累
            _customVelocity = Vector3.zero;
        }
    }
    
    private void UpdateAttackState()
    {
        // 生成器范围束缚：玩家/怪物出了范围则退出攻击
        if (IsPlayerOutsideSpawnerBounds() || IsOutsideSpawnerBounds(transform.position))
        {
            ChangeState(MonsterState.ReturnToSpawn);
            return;
        }
        if (!useCustomMovement && navMeshAgent != null) navMeshAgent.ResetPath();
        if (player != null && _locomotion != null)
        {
            _locomotion.FaceTarget = player;
        }
        // 统一使用 AttackSequence，保证播放攻击动画就必定结算伤害
        if (!_attackInProgress && attackTimer >= attackCooldown)
        {
            attackTimer = 0f;
            StartCoroutine(AttackSequence());
        }
    }
    
    private void UpdateReturnToSpawnState()
    {
        if (_locomotion != null) _locomotion.FaceTarget = null;
        if (!useCustomMovement && navMeshAgent != null)
        {
            navMeshAgent.SetDestination(spawnPosition);
            navMeshAgent.speed = monsterBase.patrolSpeed;
        }
        else
        {
            CustomMoveTowards(spawnPosition, monsterBase.patrolSpeed);
        }
    }
    
    private void CheckStateTransitions()
    {
        if (currentState == MonsterState.Death) return; // 死亡状态不再进行转换
        float distanceToPlayerSqr = player != null ? (transform.position - player.position).sqrMagnitude : float.MaxValue;

        // 统一束缚：若玩家在生成器半径外，则任何“交战相关状态”转回出生
        if (IsPlayerOutsideSpawnerBounds())
        {
            if (currentState == MonsterState.Alert || currentState == MonsterState.Chase || currentState == MonsterState.Attack)
            {
                ChangeState(MonsterState.ReturnToSpawn);
                return;
            }
        }

        switch (currentState)
        {
            case MonsterState.Idle:
                if (distanceToPlayerSqr <= alertRangeSqr) ChangeState(MonsterState.Alert);
                break;
            case MonsterState.Patrol:
                if (distanceToPlayerSqr <= alertRangeSqr) ChangeState(MonsterState.Alert);
                break;
            case MonsterState.Alert:
                if (distanceToPlayerSqr <= attackRangeSqr) ChangeState(MonsterState.Attack);
                else if (distanceToPlayerSqr <= chaseRangeSqr) ChangeState(MonsterState.Chase);
                else if (distanceToPlayerSqr > alertRangeSqr) ChangeState(MonsterState.Patrol);
                break;
            case MonsterState.Chase:
                if (!isPlayerInRange && chaseTimer >= chaseDuration) ChangeState(MonsterState.ReturnToSpawn);
                if (distanceToPlayerSqr <= attackRangeSqr) ChangeState(MonsterState.Attack);
                break;
            case MonsterState.Attack:
                // 使用带缓冲的退出距离，避免边缘来回抖动
                if (distanceToPlayerSqr > attackLeaveRangeSqr) ChangeState(MonsterState.Chase);
                else if (player == null) ChangeState(MonsterState.ReturnToSpawn);
                break;
            case MonsterState.ReturnToSpawn:
                // 仅当玩家在生成范围内时才重新进入交战
                if (!IsPlayerOutsideSpawnerBounds() && distanceToPlayerSqr <= chaseRangeSqr) ChangeState(MonsterState.Chase);
                else if ((transform.position - spawnPosition).sqrMagnitude <= returnToSpawnRangeSqr) ChangeState(MonsterState.Patrol);
                break;
        }
    }
    
    private void ChangeState(MonsterState newState)
    {
        if (currentState == newState) return;
        ExitState(currentState);
        currentState = newState;
        EnterState(newState);
    }
    
    private void EnterState(MonsterState newState)
    {
        switch (newState)
        {
            case MonsterState.Idle:
                idleTimer = 0f; if (navMeshAgent != null && navMeshAgent.enabled && !useCustomMovement) navMeshAgent.ResetPath(); _animController?.PlayIdle(); if (_locomotion != null) _locomotion.FaceTarget = null;
                break;
            case MonsterState.Patrol:
                SetPatrolTarget(); isPatrolling = false;  if (_locomotion != null) _locomotion.FaceTarget = null;
                break;
            case MonsterState.Alert:
                alertIcon?.SetActive(true); _animController?.PlayAlert(); if (navMeshAgent != null && navMeshAgent.enabled && !useCustomMovement) navMeshAgent.ResetPath(); if (_locomotion != null) _locomotion.FaceTarget = player;
                break;
            case MonsterState.Chase:
                chaseTimer = 0f; attackIcon?.SetActive(true); if (_locomotion != null) _locomotion.FaceTarget = player;
                break;
            case MonsterState.Attack:
                attackTimer = attackCooldown; if (_locomotion != null) _locomotion.FaceTarget = player;
                break;
            case MonsterState.ReturnToSpawn:
                chaseTimer = 0f; if (_locomotion != null) _locomotion.FaceTarget = null; if (navMeshAgent != null && navMeshAgent.enabled && !useCustomMovement) navMeshAgent.ResetPath();
                break;
            case MonsterState.Death:
                // 进入死亡：彻底停止一切行为
                StopAllCoroutines();
                alertIcon?.SetActive(false);
                attackIcon?.SetActive(false);
                if (navMeshAgent != null && navMeshAgent.enabled)
                {
                    navMeshAgent.ResetPath();
                }
                // 明确清除面向并禁用旋转（双保险）
                if (_locomotion != null)
                {
                    _locomotion.FaceTarget = null;
                    _locomotion.enableRotation = false;
                }
                if (_combat != null)
                {
                    _combat.ExecuteDeathSequence();
                }
                break;
        }
    }
    
    private void ExitState(MonsterState state)
    {
        switch (state)
        {
            case MonsterState.Patrol:
                StopAllCoroutines(); isPatrolling = false; break;
            case MonsterState.Alert:
                alertIcon?.SetActive(false); break;
            case MonsterState.Chase:
                attackIcon?.SetActive(false); break;
            case MonsterState.Death:
                // 不会退出死亡状态
                break;
        }
    }
    
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
    
    private void PerformAttack()
    {
        // 旧的距离判断逻辑保留但不再被使用（AttackSequence 覆盖一切攻击）。
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

    private IEnumerator AttackSequence()
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
            yield return null;
            _attackInProgress = false;
            _cachedAttackTarget = null;
            yield break;
        }
        // 非立刻：执行前摇
        if (!immediateDamageOnAttackStart && attackWindup > 0f)
        {
            float t = 0f;
            while (t < attackWindup)
            {
                if (cancelIfTargetDeadDuringWindup && (_cachedAttackTarget == null || _cachedAttackTarget.CurrentHealth <= 0))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log("[Monster.AttackSequence] 目标在前摇中死亡, 取消伤害");
#endif
                    _attackInProgress = false;
                    yield break;
                }
                t += Time.deltaTime;
                yield return null;
            }
        }
        if (guaranteedHit && _cachedAttackTarget != null && !isDead)
        {
            ApplyLockedDamage();
        }
        _attackInProgress = false;
        _cachedAttackTarget = null;
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
        ChangeState(MonsterState.Patrol);
    }
}
