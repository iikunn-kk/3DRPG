using UnityEngine;

// 经验 / 治疗 / 消耗品相关逻辑
public partial class CharacterState
{
    #region 事件
    public static event System.Action<int> OnConsumableHealed; // healed amount from consumable
    public static event System.Action<float, bool> OnAttackBuffItemUsed; // value, isPercentage
    public static event System.Action<float, bool> OnDefenceBuffItemUsed;
    public static event System.Action<float, bool> OnMagicAttackBuffItemUsed;
    #endregion

    #region 治疗 & 消耗品
    /// <summary>
    /// 治疗角色（显示治疗数字并触发属性更新）
    /// </summary>
    public void Heal(int amount)
    {
        if (_isDead) return;
        int healed = Mathf.Clamp(amount, 0, MaxHealth - CurrentHealth);
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + healed);
        OnValueChange();
        if (healthRegenDamageNumber != null && healed > 0)
        {
            var dn = healthRegenDamageNumber.Spawn(transform.position + Vector3.up * 2.5f, healed);
            dn.SetFollowedTarget(transform);
        }
    }

    /// <summary>
    /// 应用单个消耗品效果
    /// </summary>
    public void ApplyConsumableEffect(ConsumablesData consumableData)
    {
        if (consumableData == null) return;
        switch (consumableData.consumablesType)
        {
            case ConsumablesType.回血:
                int healAmount = consumableData.isPercentage ? Mathf.RoundToInt(MaxHealth * (consumableData.value / 100f)) : Mathf.RoundToInt(consumableData.value);
                int before = CurrentHealth;
                Heal(healAmount);
                int healed = CurrentHealth - before;
                if (healed > 0) OnConsumableHealed?.Invoke(healed);
                break;
            case ConsumablesType.加攻击力:
                OnAttackBuffItemUsed?.Invoke(consumableData.value, consumableData.isPercentage);
                break;
            case ConsumablesType.加防御力:
                OnDefenceBuffItemUsed?.Invoke(consumableData.value, consumableData.isPercentage);
                break;
            case ConsumablesType.加魔法攻击力:
                OnMagicAttackBuffItemUsed?.Invoke(consumableData.value, consumableData.isPercentage);
                break;
            default:
                Debug.LogWarning($"未知的消耗品类型: {consumableData.consumablesType}");
                break;
        }
    }
    #endregion

    #region 经验 & 升级
    public void AddExp(int amount)
    {
        if (Level >= MaxLevel) return;
        int startingLevel = Level; // 记录调用前的等级
        Exp += amount;
        while (Exp >= NeedExp && Level < MaxLevel)
        {
            Exp -= NeedExp;
            LevelUp();
        }
        OnValueChange();

        // 如果本次 AddExp 导致等级上升（一次或多次），仅播放一次升级特效
        if (Level > startingLevel)
        {
            PlayLevelUpEffect();
        }
        GameManager.Instance.SaveCurrentCharacterData();
    }

    private void LevelUp()
    {
        Level++;
        var baseData = GameManager.Instance.playerCharacterStateDataSo.GetPlayerCharacterStateBaseData(Profession);
        MaxHealth = baseData.GetMaxHp(Level);
        Attack = baseData.GetAttack(Level);
        Defence = baseData.GetDefence(Level);
        NeedExp = baseData.GetNeedExp(Level);
        HpRecoverySpeed = baseData.GetRegenHp(Level);
        CurrentHealth = MaxHealth; // 升级回满血
        OnValueChange();
    }
    #endregion
}
