using UnityEngine;

/// <summary>
/// 所有技能效果预制件的基类。
/// 具体的技能逻辑（如火球的飞行、爆炸）需要继承这个类来实现。
/// </summary>
public abstract class Skill : MonoBehaviour
{
    protected Transform Caster { get; private set; }
    protected PlayerSkill PlayerSkill { get; private set; }

    [Header("Lifecycle")]
    [Tooltip("非 0 则在指定秒数后自动销毁该技能实例；适合一次性技能/容器技能的兜底清理。")]
    [SerializeField] private float autoDestroyAfter = 5f;
    [Tooltip("是否启用自动销毁（用于通道类技能关闭该选项防止被意外销毁）")]
    [SerializeField] protected bool enableAutoDestroy = true;

    // 新增：缓存施法者的 CharacterState 供统一伤害派发使用
    protected CharacterState CasterState { get; private set; }

    /// <summary>
    /// 由SkillController在实例化技能时调用，用于初始化技能。
    /// </summary>
    /// <param name="caster">施法者的Transform。</param>
    /// <param name="playerSkill">当前技能的运行时数据。</param>
    public virtual void Execute(Transform caster, PlayerSkill playerSkill)
    {
        Caster = caster;
        PlayerSkill = playerSkill;
        // 缓存施法者状态（大多数情况下为玩家）
        CasterState = Caster != null ? Caster.GetComponent<CharacterState>() : null;
        if (CasterState == null)
        {
            // 回退：从 GameManager 取当前玩家（在单人玩家的场景中适用）
            CasterState = GameManager.Instance != null ? CharacterRuntimeManager.Instance.CurrentPlayerCharacter() : null;
        }
        // 可选：为一次性技能提供兜底的自动销毁，避免遗漏清理
        if (enableAutoDestroy && autoDestroyAfter > 0f)
        {
            Destroy(gameObject, autoDestroyAfter);
        }
    }

    /// <summary>
    /// 可选的目标注入（需要目标的技能可重写）。
    /// 默认不做任何事，避免必须实现接口。
    /// </summary>
    public virtual void SetTarget(Transform target) { }

    /// <summary>
    /// 可选的发射点注入（例如玩家的手/枪口），由控制器传入。
    /// 技能实现可以重写以保存并使用该发射点。
    /// </summary>
    public virtual void SetFirePoint(Transform firePoint) { }

    // 新增：统一的伤害派发入口。优先通过 CasterState.DealDamageTo（带暴击），否则回退到直接 IDamageable。
    protected void DealDamage(Transform target, float baseDamage, bool forceCrit = false)
    {
        if (target == null) return;
        AttackType atkType = AttackType.物理攻击;
        if (PlayerSkill != null && PlayerSkill.SkillSO != null)
        {
            atkType = PlayerSkill.SkillSO.attackType;
        }

        if (CasterState != null)
        {
            CasterState.DealDamageTo(target, baseDamage, forceCrit, atkType);
            return;
        }
        // 回退：直接调用 IDamageable 或 MonsterCombat
        var dmgable = target.GetComponentInParent<IDamageable>();
        if (dmgable != null)
        {
            dmgable.TakeDamage(Mathf.Max(0, Mathf.RoundToInt(baseDamage)), atkType);
            return;
        }
        var mc = target.GetComponentInParent<MonsterCombat>();
        if (mc != null)
        {
            mc.TakeDamage(Mathf.Max(0, Mathf.RoundToInt(baseDamage)));
        }
    }
}