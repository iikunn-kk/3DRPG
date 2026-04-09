using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class CharacterState
{
    #region 装备字段
    // 已装备的物品列表，按装备类型分类
    private readonly Dictionary<EquipmentType, StoredEquipment> _equippedItems = new Dictionary<EquipmentType, StoredEquipment>();
    #endregion

    #region 装备系统方法
    /// <summary>
    /// 装备物品
    /// </summary>
    /// <param name="equipmentData">要装备的物品数据</param>
    /// <returns>被替换的装备（如果有）</returns>
    public StoredEquipment EquipItem(StoredEquipment equipmentData)
    {
        var equipmentItemData = GameManager.Instance.ItemDataSo.GetEquipmentDataById(equipmentData.itemId);
        if (equipmentItemData == null)
        {
            Debug.LogError($"无法找到ID为 {equipmentData.itemId} 的装备数据");
            return null;
        }
        EquipmentType equipmentType = equipmentItemData.equipmentType;
        StoredEquipment oldEquipment = null;
        if (_equippedItems.TryGetValue(equipmentType, out var existing))
        {
            oldEquipment = existing;
        }
        _equippedItems[equipmentType] = equipmentData;
        UpdateCharacterStats(); // 内部已触发 OnValueChange
        return oldEquipment;
    }

    /// <summary>
    /// 卸下指定类型的装备
    /// </summary>
    public StoredEquipment UnEquipItem(EquipmentType equipmentType)
    {
        if (!_equippedItems.ContainsKey(equipmentType)) return null;
        var equipment = _equippedItems[equipmentType];
        _equippedItems.Remove(equipmentType);
        UpdateCharacterStats(); // 内部已触发 OnValueChange
        return equipment;
    }

    /// <summary>
    /// 获取指定类型的已装备物品
    /// </summary>
    public StoredEquipment GetEquippedItem(EquipmentType equipmentType)
    {
        return _equippedItems.TryGetValue(equipmentType, out var eq) ? eq : null;
    }

    /// <summary>
    /// 获取所有已装备的物品
    /// </summary>
    public List<StoredEquipment> GetAllEquippedItems()
    {
        return _equippedItems.Values.ToList();
    }

    /// <summary>
    /// 更新角色属性（根据已装备的物品）
    /// </summary>
    private void UpdateCharacterStats()
    {
        var baseData = GameManager.Instance.playerCharacterStateDataSo.GetPlayerCharacterStateBaseData(Profession);
        // 保存变更前的最大生命与当前生命百分比（仅在核心初始化完成后才保留百分比，否则保持满血）
        int oldMaxHp = MaxHealth;
        int oldCurrentHp = CurrentHealth;
        float prevHpPercent = 1f;
        if (_hasRunCoreInit && oldMaxHp > 0)
        {
            prevHpPercent = Mathf.Clamp01(oldCurrentHp / (float)oldMaxHp);
        }

        MaxHealth = baseData.GetMaxHp(Level);
        _attackBeforeBuffs = baseData.GetAttack(Level);
        Defence = baseData.GetDefence(Level);
        HpRecoverySpeed = baseData.GetRegenHp(Level);
        Speed = baseData.Speed;
        PhysicalDamage = 0f;
        MagicDamage = 0f;
        foreach (var equipment in _equippedItems.Values)
        {
            var equipmentData = GameManager.Instance.ItemDataSo.GetEquipmentDataById(equipment.itemId);
            if (equipmentData == null) continue;
            foreach (var property in equipment.generatedProperties)
            {
                ApplyPropertyToCharacter(property);
            }
        }
        Attack = _attackBeforeBuffs; // Buff 系统会覆盖

        // 根据之前保存的百分比来调整当前生命值，确保百分比保持不变
        CurrentHealth = Mathf.Clamp(Mathf.RoundToInt(prevHpPercent * MaxHealth), 0, MaxHealth);

        OnValueChange();
    }

    private void ApplyPropertyToCharacter(EquipmentProperty property)
    {
        if (property == null) return;
        switch (property.propertyType)
        {
            case PropertyType.攻击:
                _attackBeforeBuffs += Mathf.RoundToInt(property.actualValue);
                break;
            case PropertyType.防御:
                Defence += Mathf.RoundToInt(property.actualValue);
                break;
            case PropertyType.生命:
                // 仅修改 MaxHealth，这里不再直接更改 CurrentHealth，以便在 UpdateCharacterStats 中按百分比刷新
                MaxHealth += Mathf.RoundToInt(property.actualValue);
                break;
            case PropertyType.生命回复:
                HpRecoverySpeed += property.actualValue;
                break;
            case PropertyType.物理增伤:
                PhysicalDamage += property.actualValue;
                break;
            case PropertyType.魔法增伤:
                MagicDamage += property.actualValue;
                break;
            default:
                Debug.LogWarning($"未处理的装备属性类型: {property.propertyType}");
                break;
        }
    }
    #endregion
}
