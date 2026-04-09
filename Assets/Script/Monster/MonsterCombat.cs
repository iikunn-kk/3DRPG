using UnityEngine;
using System.Collections;
using DamageNumbersPro;

/// <summary>
/// 怪物战斗系统，负责处理受伤、死亡和掉落逻辑
/// </summary>
public class MonsterCombat : MonoBehaviour, IDamageable
{
    [Header("死亡和掉落")]
    [SerializeField] private float corpseDisappearTime = 10f;
    [SerializeField] private float dropForce = 5f;
    [SerializeField][Tooltip("死亡后动画持续时间 (秒)，超过后才进入尸体计时")] private float deathAnimationDuration = 2.5f;
    [SerializeField][Tooltip("死亡后切换到的物理层 (避免与玩家/其它怪物继续交互)，为空则保持原层")] private string deathLayerName = "Dead";
    [SerializeField][Tooltip("死亡后是否禁用 NavMeshAgent (一般需要禁用)")] private bool disableAgentOnDeath = true;
    [SerializeField][Tooltip("死亡后是否禁用 AI/状态机组件")] private bool disableStateMachineOnDeath = true;
    
    [Header("命中特效挂点（艺术家在怪物 prefab 上设置）")]
    [Tooltip("用于放置被击中时的命中特效位置（例如胸口或头顶），如果为空会回退到射线击中点。")]
    [SerializeField] protected Transform _hitVfxPoint;
    public Transform hitVfxPoint => _hitVfxPoint;
    
    public int CurrentHealth { get; set; }
    public int MaxHealth { get; set; }
    [Header("伤害数字预制体 (在 Inspector 中分配)")]
    [SerializeField] private DamageNumber physicsDamageNumber;
    [SerializeField] private DamageNumber magicDamageNumber;
    [SerializeField] private DamageNumber healthRegenDamageNumber;


    private MonsterData monsterData;                         // 怪物数据
    private MonsterSpawner monsterSpawner;                   // 怪物生成器引用
    private MonsterAnimationController _animController;      // 动画控制器引用
    private UnityEngine.AI.NavMeshAgent navMeshAgent;        // 导航网格代理
    private bool isDead;                             // 是否已死亡
    

    [Header("事件 SO（可选）")]
    [SerializeField] private TargetHealthEventSO targetHealthEventSO;

    private MonsterBase _monsterBase;                        // 怪物基础组件引用
    private MonsterStateMachine _stateMachine;               // 状态机引用

    // 防止死亡序列被执行多次
    private bool _deathSequenceExecuted;

    // ===== 补回：锁定血量广播相关方法（上移，避免分析器遗漏） =====
    public void RaiseHealthSnapshot()
    {
        if (_monsterBase == null) _monsterBase = GetComponent<MonsterBase>();
        if (_monsterBase != null && _monsterBase.IsLocked)
        {
            RaiseHealthPayload(CurrentHealth, MaxHealth);
        }
    }
    private void BroadcastHealthIfLocked()
    {
        if (_monsterBase == null) _monsterBase = GetComponent<MonsterBase>();
        if (_monsterBase != null && _monsterBase.IsLocked)
        {
            RaiseHealthPayload(CurrentHealth, MaxHealth);
        }
    }
    private void RaiseHealthPayload(int current, int max)
    {
        if (targetHealthEventSO == null) return;
        if (_monsterBase == null) _monsterBase = GetComponent<MonsterBase>();
        var payload = new TargetHealthPayload
        {
            target = _monsterBase,
            current = current,
            max = max
        };
        targetHealthEventSO.RaiseEvent(payload, this);
    }

    private void Awake()
    {
        _monsterBase = GetComponent<MonsterBase>();
        _stateMachine = GetComponent<MonsterStateMachine>();
    }
    
    public void Initialize(MonsterData data, MonsterSpawner spawner, 
                          MonsterAnimationController animController, UnityEngine.AI.NavMeshAgent agent)
    {
        monsterData = data;
        monsterSpawner = spawner;
        _animController = animController;
        navMeshAgent = agent;
        
        MaxHealth = data.health;
        CurrentHealth = data.health;

        if (_monsterBase == null) _monsterBase = GetComponent<MonsterBase>();
        if (_stateMachine == null) _stateMachine = GetComponent<MonsterStateMachine>();
    }
    
    /// <summary>
    /// 受到伤害（兼容带攻击类型的版本）
    /// </summary>
    /// <param name="damage">伤害值</param>
    /// <param name="attackType">伤害类型（物理/魔法/回血）</param>
    public void TakeDamage(int damage, AttackType attackType = AttackType.物理攻击)
    {
        if (isDead) return;

        // 回血技能：作为治疗处理
        if (attackType == AttackType.回血技能)
        {
            int healed = Mathf.Clamp(damage, 0, MaxHealth - CurrentHealth);
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + damage);

            // 显示回血数字
            if (healthRegenDamageNumber != null && healed > 0)
            {
                var dn = healthRegenDamageNumber.Spawn(transform.position + Vector3.up * 0.5f, healed);
                dn.SetFollowedTarget(transform);
                dn.SetColor(new Color(0.3f, 1f, 0.3f));
                dn.SetScale(1f);
            }

            BroadcastHealthIfLocked();
            return;
        }

        // 普通物理和魔法伤害（怪物无防御，直接受伤）
        CurrentHealth -= damage;

        // 触发受击动画
        _animController?.PlayHit();

        // 新增：受击后尝试拉仇恨（若玩家不算太远，会立刻进入追击/攻击）
        _stateMachine?.ForceAggroToPlayer();

        // 显示伤害数字（区分物理/魔法）
        if (attackType == AttackType.物理攻击 && physicsDamageNumber != null)
        {
            var dn = physicsDamageNumber.Spawn(transform.position + Vector3.up * 0.5f, damage);
            dn.SetFollowedTarget(transform);
            dn.SetColor(new Color(1f, 0.6f, 0.5f));
            dn.SetScale(1f);
        }
        else if (attackType == AttackType.魔法攻击 && magicDamageNumber != null)
        {
            var dn = magicDamageNumber.Spawn(transform.position + Vector3.up * 0.6f, damage);
            dn.SetFollowedTarget(transform);
            dn.SetColor(new Color(0.8f, 0.8f, 1f));
            dn.SetScale(1.05f);
        }
        else
        {
            if (healthRegenDamageNumber != null)
            {
                var dn = healthRegenDamageNumber.Spawn(transform.position + Vector3.up * 0.5f, damage);
                dn.SetFollowedTarget(transform);
                dn.SetColor(new Color(1f, 0.6f, 0.5f));
                dn.SetScale(1f);
            }
        }

        // 仅当该怪物处于被锁定状态时广播血量变化
        BroadcastHealthIfLocked();

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 外部请求怪物死亡（例如血量归零）。
    /// 简化为：设置 isDead 并请求状态机切换到 Death，由状态机驱动死亡序列。
    /// 若没有状态机，直接执行死亡序列。
    /// </summary>
    public void Die()
    {
        if (isDead) return;
        isDead = true;

        // 请求状态机进入死亡状态（会在 EnterState 中调用 ExecuteDeathSequence）
        if (_stateMachine != null)
        {
            _stateMachine.SetDead(true);
        }
        else
        {
            // 兜底：无状态机时直接执行
            ExecuteDeathSequence();
        }
    }

    /// <summary>
    /// 由状态机 Death 状态或无状态机场景直接调用。执行一次性死亡逻辑：动画、掉落、事件、清理。
    /// </summary>
    public void ExecuteDeathSequence()
    {
        if (_deathSequenceExecuted) return; // 防止重复执行
        _deathSequenceExecuted = true;

        // 报告死亡（统计/刷新生成逻辑）
        monsterSpawner?.ReportMonsterDeath(GetComponent<MonsterBase>());

        // 如果正被锁定，推送最终血量 0
        if (_monsterBase == null) _monsterBase = GetComponent<MonsterBase>();
        if (_monsterBase != null && _monsterBase.IsLocked)
        {
            RaiseHealthPayload(0, MaxHealth);
            _monsterBase.SetLocked(false); // 关闭锁定
        }

        // 播放死亡动画
        _animController?.PlayDeath();

        // 改为：不直接禁用 Collider，避免模型刚体/动画立即失效；而是切换到一个“虚空”层不再相互阻挡
        if (!string.IsNullOrEmpty(deathLayerName))
        {
            int dl = LayerMask.NameToLayer(deathLayerName);
            if (dl != -1) gameObject.layer = dl;
        }

        // 可选禁用 NavMeshAgent 和状态机避免继续移动
        if (disableAgentOnDeath && navMeshAgent != null)
        {
            navMeshAgent.enabled = false;
        }
        if (disableStateMachineOnDeath && _stateMachine != null)
        {
            _stateMachine.enabled = false; // 状态机不再更新
        }
        // 给玩家经验并弹出 Toast 通知
        GivePlayerExp();

        // 声音 & 任务事件
        AudioManager.Instance.PlayMonsterSound(MonsterSoundType.死亡);
        if (monsterData != null)
        {
            TaskEvents.TriggerEnemyKilled(monsterData.monsterID);
        }

        // 启动新的死亡处理协程（先等动画完成再进入尸体存在阶段）
        StartCoroutine(DeathFlowRoutine());
    }

    private IEnumerator DeathFlowRoutine()
    {
        // 先等待死亡动画时间
        if (deathAnimationDuration > 0f)
            yield return new WaitForSeconds(deathAnimationDuration);
        // 再等待尸体消失时间
        if (corpseDisappearTime > 0f)
            yield return new WaitForSeconds(corpseDisappearTime);
        Destroy(gameObject);
    }
    
    // 将经验授予玩家并显示 Toast（若配置了 expReward）
    private void GivePlayerExp()
    {
        if (monsterData == null) return;
        int exp = monsterData.expReward;
        if (exp <= 0) return;

        var player = GameManager.Instance?.CurrentPlayerCharacter();
        if (player == null)
        {
            Debug.LogWarning("[MonsterCombat] 无法找到当前玩家，经验未发放。");
            return;
        }

        // 给予经验
        player.AddExp(exp);

        // 显示 Toast 给玩家（UIManager 会自动创建 ToastManager 若未创建）
        string msg = $"获得 {exp} 点经验";
        try
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowToast(msg, null, 2f);
            }
            else
            {
                Debug.Log(msg);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MonsterCombat] 显示经验 Toast 失败: {e}");
        }
    }
    
    public bool IsDead => isDead;

    public int GetDamageValue()
    {
        return monsterData != null ? monsterData.damage : 1;
    }
}
