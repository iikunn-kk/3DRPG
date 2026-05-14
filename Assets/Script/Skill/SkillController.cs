using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Collections.Generic;

public class SkillController : MonoBehaviour
{
    [Header("EventSOs（在 Inspector 中绑定具体的事件资源）")]
    [Tooltip("技能冷却期间每帧广播一次（参数：SkillCooldownUpdatePayload），用于更新冷却UI")]
    [SerializeField] private SkillCooldownUpdateEventSO cooldownUpdateEvent;
    [Tooltip("技能冷却结束时广播（参数：string skillID），用于隐藏冷却遮罩等")]
    [SerializeField] private SkillReadyEventSO skillReadyEvent;
    [Tooltip("当当前玩家技能快照就绪时广播（参数：SkillController），用于让 UI（如技能快捷栏）构建与加载快捷键显示。")]
    [SerializeField] private SkillControllerEventSO skillsInitializedEvent;
    [Tooltip("技能提示（字符串），例如：需要目标、目标距离过远。")]
    [SerializeField] private StringEventSO skillToastEvent;

    [Header("控制器引用（可选）")]
    [SerializeField] private GlobalCooldownController globalCooldown; // 公共冷却
    [SerializeField] private NormalAttackController normalAttackController; // 普通攻击引导控制
    [SerializeField] private LockOnController lockOn; // 锁定控制（用于技能需要目标时)
    [SerializeField] private Transform firePoint; // 通用开火点（例如玩家手部），将传递给 Skill 实例

    [Header("动画（可选）")]
    [Tooltip("可在 Inspector 指定 CharacterAnimationController，用于在施法时播放相应动画；若不指定会尝试从同一 GameObject 获取。")]
    [SerializeField] private CharacterAnimationController characterAnimationController;

    [Header("动画锁定时长（秒）- 无剪辑事件时的兜底值")]
    [SerializeField] private float defaultSkillLockDuration = 0.6f;
    [SerializeField] private float defaultBuffLockDuration = 0.4f;
    [SerializeField] private float defaultAttackLockDuration = 0.5f;

    // 以技能ID为键的运行时技能实例表。切换角色会重建该表。
    private readonly Dictionary<string, PlayerSkill> _playerSkills = new();

    // 缓存对 SkillManager 的引用
    private SkillManager _skillManager;
    private void Awake()
    {
        _skillManager = SkillManager.Instance;
        // 尝试自动获取动画控制器
        if (characterAnimationController == null)
        {
            characterAnimationController = GetComponent<CharacterAnimationController>();
        }
        // 自动查找 GlobalCooldownController（如果 Inspector 未显式绑定）
        if (globalCooldown == null)
        {
            globalCooldown = GetComponent<GlobalCooldownController>() ?? GetComponentInChildren<GlobalCooldownController>() ?? GetComponentInParent<GlobalCooldownController>();
        }
    }

    private void OnEnable()
    {
        if (_skillManager != null)
        {
            _skillManager.PlayerSkillsInitialized += OnManagerPlayerSkillsInitialized;
        }
        // 订阅连环踢事件
        ChainKicksSkill.StageCompleted += OnChainKicksStageCompleted;
        ChainKicksSkill.ComboEnded += OnChainKicksComboEnded;
    }

    private void OnDisable()
    {
        if (_skillManager != null)
        {
            _skillManager.PlayerSkillsInitialized -= OnManagerPlayerSkillsInitialized;
        }
        // 取消订阅
        ChainKicksSkill.StageCompleted -= OnChainKicksStageCompleted;
        ChainKicksSkill.ComboEnded -= OnChainKicksComboEnded;
    }

    private void Start()
    {
        // 启动时由 SkillManager 负责构建当前玩家技能，并通过事件回传快照
#if UNITY_EDITOR
        Debug.Log("[SkillController] Start -> Requesting rebuild of current player skills");
#endif
        _skillManager?.RebuildCurrentPlayerSkillsFromGame();
    }

    private void OnManagerPlayerSkillsInitialized(IReadOnlyDictionary<string, PlayerSkill> snapshot)
    {
        // 覆盖本地快照
        _playerSkills.Clear();
        foreach (var kv in snapshot)
        {
            _playerSkills[kv.Key] = kv.Value;
        }
        // 通知 UI：技能就绪，可构建快捷栏并加载快捷键
        skillsInitializedEvent?.RaiseEvent(this, this);
    }

    // 移除基于 CharacterData 的重建，改为请求管理器重建
    public void RefreshSkills()
    {
        _skillManager?.RebuildCurrentPlayerSkillsFromGame();
    }

    public int GetPlayerLevel() => GameManager.Instance?.CurrentPlayerCharacter()?.PlayerCharacterData?.level ?? 0;

    /// <summary>
    /// 冷却计时驱动：对所有处于冷却期的技能做逐帧倒计时，并广播冷却更新/结束事件。
    /// 注意：该广播每帧都会发，监听方需避免昂贵操作（如不必要的字符串拼接/GC）。
    /// </summary>
    private void Update()
    {
        foreach (var skill in _playerSkills.Values)
        {
            if (skill.CooldownTimer <= 0) continue;
            skill.CooldownTimer -= Time.deltaTime;

            // 冷却进度事件（UI用于更新遮罩与数字）
            if (cooldownUpdateEvent != null)
            {
                var payload = new SkillCooldownUpdatePayload
                {
                    SkillID = skill.SkillSO.SkillID,
                    Remaining = skill.CooldownTimer,
                    Total = _skillManager.GetCooldownAtLevel(skill.SkillSO, skill.Level)
                };
                cooldownUpdateEvent.RaiseEvent(payload, this);
            }

            // 冷却结束（只触发一次）
            if (skill.CooldownTimer <= 0)
            {
                skill.CooldownTimer = 0f;
                skillReadyEvent?.RaiseEvent(skill.SkillSO.SkillID, this);
            }
        }
    }

    /// <summary>
    /// 施放技能：
    /// - 检查公共冷却；
    /// - 若正在进行普通攻击引导，优先打断（也可选择直接拦截，按项目需求）；
    /// - 非“普通攻击”会按公式设置本次冷却时间；
    /// - 实例化技能预制体并执行；
    /// - 成功后启动 GCD。
    /// </summary>
    public void CastSkill(string skillID)
    {
        if (UIManager.Instance.isOpenedPanel)
        {
            return;
        }
        if (!_playerSkills.TryGetValue(skillID, out var ps)) return;
        var so = ps.SkillSO;

        // 若为连环踢并且已经在执行中，直接尝试登记下一段输入然后返回，避免重复实例化与误触发其它动画
        if (so.skillName == SkillType.连环踢 && ChainKicksSkill.IsActive(transform))
        {
            ChainKicksSkill.RegisterPressIfActive(transform);
            return;
        }
        float cd = _skillManager.GetCooldownAtLevel(so, ps.Level);
        // 仅“普通攻击”不在此处触发GCD（其由 NormalAttackController 负责）
        bool gcdEligible = (globalCooldown != null) && ps.SkillSO.skillType != SkillEffectType.普通攻击;

        // === 冷却与公共冷却前置校验 ===
        // 1) 自身冷却：若该技能尚在冷却中，则直接提示并拦截
        if (ps.CooldownTimer > 0f)
        {
            skillToastEvent?.RaiseEvent($"技能冷却中（剩余 {ps.CooldownTimer:F1}s）", this);
            return;
        }

        // 2) 公共冷却（GCD）：若正在GCD中，且本技能会触发GCD，则禁止施放
        if (gcdEligible && globalCooldown.IsOnGCD)
        {
            skillToastEvent?.RaiseEvent("技能处于公共冷却中", this);
            return;
        }

        // 若正在进行普通攻击的引导，先打断（按项目需求）
        if (normalAttackController != null && normalAttackController.IsChanneling)
        {
            normalAttackController.InterruptChannel();
        }

        // 需要目标的技能做目标与距离校验
        Transform target = null;
        if (so.requiresTarget)
        {
            var monster = lockOn != null ? lockOn.GetCurrentTarget() : null; // MonsterBase
            target = monster != null ? monster.transform : null;

            if (target == null)
            {
                skillToastEvent?.RaiseEvent("我必须有一个目标才可以", this);
                return;
            }
            float dist = (transform.position - target.position).sqrMagnitude;
            if (dist > so.castRange * so.castRange)
            {
                skillToastEvent?.RaiseEvent("目标距离过远", this);
                return;
            }
        }

        // 通过所有前置校验，可以施放技能
        // 1. 设置技能自身冷却（普通攻击与连环踢除外）
        if (ps.SkillSO.skillType != SkillEffectType.普通攻击 && so.skillName != SkillType.连环踢)
        {
            ps.CooldownTimer = cd;
        }
        
        // 2. 如果符合条件，触发公共冷却（连环踢改为由技能内部每段触发，此处跳过）
        if (gcdEligible && so.skillName != SkillType.连环踢)
        {
            globalCooldown.StartGCD();
        }

        // 播放对应的动画（如果有动画控制器）
        if (characterAnimationController != null && ps.SkillSO != null)
        {
            // 连环踢拥有自己在 ChainKicksSkill 中分段触发的 3 个独立动画，这里跳过默认动画播放，避免被默认 Skill 动画覆盖
            if (so.skillName == SkillType.连环踢)
            {
                // 不做任何播放——ChainKicksSkill.DoCombo 内部会调用 AnimationController.TriggerByName(stageTrigger)
            }
            else
            {
                switch (ps.SkillSO.skillType)
                {
                    case SkillEffectType.法术:
                        characterAnimationController.PlaySkill(defaultSkillLockDuration);
                        break;
                    case SkillEffectType.Buff:
                    case SkillEffectType.持续性技能:
                        characterAnimationController.PlayBuff(defaultBuffLockDuration);
                        break;
                    case SkillEffectType.普通攻击:
                        characterAnimationController.PlayAttack(defaultAttackLockDuration);
                        break;
                    default:
                        characterAnimationController.PlaySkill(defaultSkillLockDuration);
                        break;
                }
            }
        }

        if (ps.SkillSO.skillPrefab != null)
        {
            var go = Instantiate(ps.SkillSO.skillPrefab, transform.position, transform.rotation);
            var comp = go.GetComponent<Skill>();

            // 调整顺序：先注入 firePoint / target，再执行
            if (comp != null && firePoint != null) comp.SetFirePoint(firePoint);
            if (comp != null && target != null) comp.SetTarget(target);
            comp?.Execute(transform, ps);

            // 安全兜底：若该预制体未挂载 Skill 组件，则在一段时间后自动销毁
            if (comp == null)
            {
                Destroy(go, 6f);
#if UNITY_EDITOR
                Debug.LogWarning($"Skill prefab '{ps.SkillSO.skillPrefab.name}' 没有 Skill 组件，已启用自动销毁兜底。");
#endif
            }
        }
        // 注意：GCD的启动已经移到前面，无论有无prefab都执行
    }
    
    /// <summary>
    /// 获取当前技能表的只读快照（用于 UI 一次性构建）。
    /// 切换角色或改变技能集后，应重新调用 Init 并监听 skillsInitializedEvent 来获取新快照。
    /// </summary>
    public IReadOnlyDictionary<string, PlayerSkill> GetAllSkillsSnapshot() => _playerSkills;

    private void OnChainKicksStageCompleted(Transform caster, int stage, bool isLast)
    {
        // 改为：不在这里启动公共冷却。连环踢每段的GCD由技能内部在“每段开始”时触发。
        if (!isLast)
        {
            // 等待GCD结束后开启下一段输入窗口
            OpenChainKickNextWindowAfterGcdAsync(caster).Forget();
        }
    }

    private async UniTaskVoid OpenChainKickNextWindowAfterGcdAsync(Transform caster)
    {
        try
        {
            await UniTask.WaitWhile(() => globalCooldown != null && globalCooldown.IsOnGCD);
            ChainKicksSkill.RequestOpenWindow(caster);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SkillController] OpenChainKickNextWindowAfterGcdAsync failed: {ex}");
        }
    }

    private void OnChainKicksComboEnded(Transform caster)
    {
        // 这里可以扩展：记录统计、播放结束特效等
    }

    public GlobalCooldownController GlobalCooldown => globalCooldown; // existing

    // 新增：对外暴露动画控制器只读引用
    public CharacterAnimationController AnimationController => characterAnimationController;
}
