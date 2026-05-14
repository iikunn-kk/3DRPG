using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 装备属性计算（从 EquipmentController 读取装备数据，重算 CharacterState 属性）
/// </summary>
public partial class CharacterState
{
    private EquipmentController _equipmentController;

    #region EquipmentController 引用
    private EquipmentController GetEquipmentController()
    {
        if (_equipmentController == null)
            _equipmentController = GetComponent<EquipmentController>();
        return _equipmentController;
    }
    #endregion

    #region 属性重算
    /// <summary>
    /// 根据 EquipmentController 中的已装备物品重新计算角色属性
    /// </summary>
    public void UpdateCharacterStats()
    {
        var equipCtrl = GetEquipmentController();
        var baseData = GameManager.Instance.playerCharacterStateDataSo.GetPlayerCharacterStateBaseData(Profession);

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

        if (equipCtrl != null)
        {
            foreach (var equipment in equipCtrl.GetAllEquippedItems())
            {
                var equipmentData = GameManager.Instance.ItemDataSo.GetEquipmentDataById(equipment.itemId);
                if (equipmentData == null) continue;
                foreach (var property in equipment.generatedProperties)
                {
                    ApplyPropertyToCharacter(property);
                }
            }
        }

        Attack = _attackBeforeBuffs;
        CurrentHealth = Mathf.Clamp(Mathf.RoundToInt(prevHpPercent * MaxHealth), 0, MaxHealth);
        OnValueChange();
    }
    #endregion

    #region 属性应用
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
