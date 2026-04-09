using UnityEngine;
// 这个特性让我们可以直接在Unity编辑器中通过右键菜单创建这个SO的实例
[CreateAssetMenu(fileName = "NewSkill", menuName = "Skills/技能基础数据")]
public class SkillSO : ScriptableObject
{
    [Header("基础信息 (Basic Info)")]
    public SkillType skillName; // 技能名称

    [Header("伤害属性")]
    public AttackType attackType;

    [Header("唯一标识 (Unique ID)")]
    [SerializeField] private string skillID; // 私有字段
    public string SkillID => skillID; // 公开的只读属性

    [Header("技能描述")]
    [TextArea]
    public string description; // 技能描述

    [Header("技能图标")]
    public Sprite icon; // 技能图标

    [Header("技能类型")]
    public SkillEffectType skillType; // 技能类型

    [Header("核心逻辑 (Core Logic)")]
    public GameObject skillPrefab; // 技能效果的Prefab (例如火球的Prefab)

    [Header("施法距离")]
    public float castRange;

    [Header("目标需求与投射体配置 (Targeting & Projectile)")]
    [Tooltip("该技能是否需要一个锁定目标")]
    public bool requiresTarget = true;

    [Header("投射物飞行速度")]
    [Tooltip("投射物飞行速度（单位/秒），0则使用脚本默认值")]
    public float projectileSpeed = 12f;

    [Header("多发间隔")]
    [Tooltip("多发子弹/多段释放之间的间隔（秒）")]
    public float missileInterval = 0.15f;

    [Header("冷却时间 (Cooldown)")]
    public float cooldown = 5f; // 基础冷却时间（秒）

    [Header("冷却缩减")]
    [Tooltip("每级冷却缩减百分比，例如0.03表示每级-3%冷却，普通攻击不生效")]
    [Range(0f, 0.5f)] public float perLevelCooldownReducePercent = 0.03f;

    [Header("伤害与效果 (Damage & Effects)")]
    [Tooltip("基础伤害（不含攻击力加成）")]
    public float baseDamage = 100f; // 基础伤害值

    [Header("基础伤害增长")]
    [Tooltip("每级基础伤害按基础值线性增加的百分比。例如0.05表示每级+5%基础伤害（线性）")]
    [Range(0f, 1f)] public float perLevelBaseDamagePercent = 0.05f;

    [Header("攻击力加成")]
    [Tooltip("初始攻击力加成百分比，例如3表示3%")]
    public float baseAttackScalePercent = 3f;

    [Header("攻击力加成增长")]
    [Tooltip("每级攻击力加成提升的百分比，例如1表示每级+1%")]
    public float perLevelAttackScalePercent = 1f;

    [Header("治疗与Buff (Healing & Buffs)")]
    [Tooltip("基础治疗量")]
    public float baseHealAmount;

    [Header("治疗量增长")]
    [Tooltip("每级基础治疗量提升百分比")]
    [Range(0f, 1f)] public float perLevelHealAmountPercent = 0.05f;

    [Header("Buff效果值")]
    [Tooltip("Buff效果值（例如：攻击力提升的百分比）")]
    public float buffValue;

    [Header("Buff效果增长")]
    [Tooltip("每级Buff效果值提升")]
    public float perLevelBuffValue = 0.1f;

    [Header("等级上限 (Max Level)")]
    public int maxLevel = 10; // 技能最大等级

    [Header("关键等级解锁 (Special Unlock Notes)")]
    [TextArea] public string specialUnlockNoteLv5;
    [TextArea] public string specialUnlockNoteLv10;

}


public enum SkillType
{
    奥术智慧,
    奥术飞弹,
    回春术,
    连环踢,
    奥术护盾,
    奥术射线,
    七月自定义技能
}
public enum SkillEffectType
{
    Buff,
    法术,
    体术,
    持续性技能,
    普通攻击
}
public enum AttackType
{
    物理攻击,
    魔法攻击,
    回血技能,
    Buff技能
}