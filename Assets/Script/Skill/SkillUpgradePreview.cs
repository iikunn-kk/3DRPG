using UnityEngine;

/// <summary>
/// UI使用的技能升级预览数据包
/// </summary>
public struct SkillUpgradePreview
{
    public string SkillID;
    public string DisplayName;
    public Sprite Icon;
    public int CurrentLevel;
    public int NextLevel;
    public int MaxLevel;

    public float CurrentBaseDamage;
    public float NextBaseDamage;
    public float CurrentAttackScalePercent;
    public float NextAttackScalePercent;

    public float CurrentCooldown;
    public float NextCooldown;

    public float CurrentHealAmount;
    public float NextHealAmount;
    public float CurrentBuffValue;
    public float NextBuffValue;

    public float CurrentTotalDamage;
    public float NextTotalDamage;

    public float DamageIncreasePercent;
    public float CooldownReducePercent;

    public int Cost;
    public int RequiredPlayerLevel;
    public string SpecialUnlockNote;
    public string AttackTypeText;
}
