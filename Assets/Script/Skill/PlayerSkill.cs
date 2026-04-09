using UnityEngine;

/// <summary>
/// 在运行时管理单个技能状态的辅助类
/// </summary>
public class PlayerSkill
{
    public SkillSO SkillSO { get; }
    public int Level { get; private set; }
    public float CooldownTimer { get; set; }

    public PlayerSkill(SkillSO skillSO, int level)
    {
        SkillSO = skillSO;
        Level = level;
        CooldownTimer = 0;
    }

    /// <summary>
    /// 提升技能等级
    /// </summary>
    public void UpgradeLevel()
    {
        Level++;
    }

    /// <summary>
    /// 获取当前等级的预计总伤害（基础伤害+攻击力加成），用于运行时执行
    /// </summary>
    public float GetDamage()
    {
        var ctrl = GameManager.Instance != null ? GameManager.Instance.CurrentPlayerCharacter() : null;
        int atk = ctrl != null ? ctrl.Attack : 0;
        float basePart = SkillSO.baseDamage * (1f + SkillSO.perLevelBaseDamagePercent * Level);
        float atkPct = SkillSO.baseAttackScalePercent + SkillSO.perLevelAttackScalePercent * Level;
        return basePart + atk * (atkPct / 100f);
    }

    /// <summary>
    /// 获取当前等级的冷却时间
    /// </summary>
    public float GetCooldown()
    {
        if (SkillSO.skillType == SkillEffectType.普通攻击) return SkillSO.cooldown;
        float factor = Mathf.Max(0f, 1f - SkillSO.perLevelCooldownReducePercent * Level);
        return SkillSO.cooldown * factor;
    }
}

