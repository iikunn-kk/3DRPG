using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 装备控制器 — 从 CharacterState 拆分出的独立组件。
/// 负责装备数据管理、InventoryManager 事件同步、从背包初始化装备。
/// 装备变化时通过 OnEquipmentChanged 事件通知 CharacterState 重算属性。
/// 通过 GetComponent&lt;EquipmentController&gt;() 访问。
/// </summary>
public class EquipmentController : MonoBehaviour
{
    #region 装备数据
    private readonly Dictionary<EquipmentType, StoredEquipment> _equippedItems = new();
    private bool _initializedFromInventory;
    #endregion

    #region 事件
    /// <summary>装备变化时触发，CharacterState 订阅后重算属性</summary>
    public event System.Action OnEquipmentChanged;
    #endregion

    #region 公共属性
    public bool IsInitialized => _initializedFromInventory;
    public IReadOnlyDictionary<EquipmentType, StoredEquipment> EquippedItems => _equippedItems;
    #endregion

    #region Unity 生命周期
    private void OnEnable()
    {
        InventoryManager.OnItemEquipped += HandleItemEquipped;
        InventoryManager.OnItemUnequipped += HandleItemUnequipped;
        InventoryManager.OnInventoryUpdated += HandleInventoryUpdated;
    }

    private void OnDisable()
    {
        InventoryManager.OnItemEquipped -= HandleItemEquipped;
        InventoryManager.OnItemUnequipped -= HandleItemUnequipped;
        InventoryManager.OnInventoryUpdated -= HandleInventoryUpdated;
    }
    #endregion

    #region 装备操作
    /// <summary>装备物品并触发属性重算</summary>
    public StoredEquipment EquipItem(StoredEquipment equipmentData)
    {
        var itemData = GameManager.Instance.ItemDataSo.GetEquipmentDataById(equipmentData.itemId);
        if (itemData == null)
        {
            Debug.LogError($"无法找到ID为 {equipmentData.itemId} 的装备数据");
            return null;
        }
        StoredEquipment oldEquipment = null;
        if (_equippedItems.TryGetValue(itemData.equipmentType, out var existing))
            oldEquipment = existing;
        _equippedItems[itemData.equipmentType] = equipmentData;
        OnEquipmentChanged?.Invoke();
        return oldEquipment;
    }

    /// <summary>卸下指定类型的装备并触发属性重算</summary>
    public StoredEquipment UnEquipItem(EquipmentType equipmentType)
    {
        if (!_equippedItems.TryGetValue(equipmentType, out var equipment)) return null;
        _equippedItems.Remove(equipmentType);
        OnEquipmentChanged?.Invoke();
        return equipment;
    }

    /// <summary>获取指定类型的已装备物品</summary>
    public StoredEquipment GetEquippedItem(EquipmentType equipmentType)
    {
        return _equippedItems.TryGetValue(equipmentType, out var eq) ? eq : null;
    }

    /// <summary>获取所有已装备物品</summary>
    public List<StoredEquipment> GetAllEquippedItems()
    {
        return _equippedItems.Values.ToList();
    }

    /// <summary>检查指定类型是否已装备</summary>
    public bool IsEquipped(EquipmentType type) => _equippedItems.ContainsKey(type);
    #endregion

    #region 背包初始化
    /// <summary>强制尝试初始化装备（由 MapManager 延迟调用）</summary>
    public void EnsureInitialized()
    {
        if (_initializedFromInventory) return;
        TryInitializeFromInventory();
    }

    /// <summary>从 InventoryManager 加载已装备物品并应用</summary>
    private void TryInitializeFromInventory()
    {
        if (_initializedFromInventory) return;
        var invMgr = InventoryManager.Instance;
        if (invMgr == null || !invMgr.IsLoaded) return;
        var allItems = invMgr.AllItems;
        if (allItems == null)
        {
            _initializedFromInventory = true;
            return;
        }
        int equipCount = 0;
        foreach (var invItem in allItems.Where(i => i.location == ItemLocation.Equipped))
        {
            var equipData = GameManager.Instance.ItemDataSo.GetEquipmentDataById(invItem.itemId);
            if (equipData == null) continue;
            var stored = BuildStoredEquipmentFromInventoryItem(invItem);
            _equippedItems[equipData.equipmentType] = stored;
            equipCount++;
        }
        _initializedFromInventory = true;
        if (equipCount > 0)
            OnEquipmentChanged?.Invoke();
        Debug.Log($"[EquipmentController] 初始化完成，已装备 {equipCount} 件");
    }
    #endregion

    #region InventoryManager 事件处理
    private void HandleItemEquipped(InventoryItem inventoryItem, EquipmentData equipmentData)
    {
        if (inventoryItem == null || equipmentData == null) return;
        _equippedItems[equipmentData.equipmentType] = BuildStoredEquipmentFromInventoryItem(inventoryItem);
        OnEquipmentChanged?.Invoke();
    }

    private void HandleItemUnequipped(InventoryItem inventoryItem, EquipmentData equipmentData)
    {
        if (equipmentData == null) return;
        _equippedItems.Remove(equipmentData.equipmentType);
        OnEquipmentChanged?.Invoke();
    }

    private void HandleInventoryUpdated()
    {
        TryInitializeFromInventory();
    }
    #endregion

    #region 工具方法
    private StoredEquipment BuildStoredEquipmentFromInventoryItem(InventoryItem invItem)
    {
        var se = new StoredEquipment
        {
            itemId = invItem.itemId,
            generatedProperties = invItem.generatedProperties != null
                ? invItem.generatedProperties.Select(p => p.DeepClone()).ToList()
                : new List<EquipmentProperty>(),
            quantity = invItem.quantity
        };

        var templateData = GameManager.Instance?.ItemDataSo?.GetEquipmentDataById(invItem.itemId);
        if (templateData != null && templateData.customProperties != null && templateData.customProperties.Count > 0)
        {
            foreach (var tProp in templateData.customProperties)
            {
                if (tProp == null) continue;
                bool identicalExists = se.generatedProperties != null && se.generatedProperties.Any(p =>
                    p != null &&
                    p.propertyType == tProp.propertyType &&
                    Mathf.Approximately(p.actualValue, tProp.actualValue) &&
                    Mathf.Approximately(p.minValue, tProp.minValue) &&
                    Mathf.Approximately(p.maxValue, tProp.maxValue)
                );
                if (!identicalExists)
                {
                    var clone = tProp.DeepClone();
                    if (Mathf.Approximately(clone.actualValue, 0f) && (!Mathf.Approximately(clone.minValue, 0f) || !Mathf.Approximately(clone.maxValue, 0f)))
                    {
                        clone.GenerateActualValue();
                        if (!clone.IsPercentage)
                            clone.actualValue = Mathf.RoundToInt(clone.actualValue);
                    }
                    se.generatedProperties.Add(clone);
                }
            }
        }
        return se;
    }
    #endregion
}
