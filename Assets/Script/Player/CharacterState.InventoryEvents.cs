using System.Linq;
using UnityEngine;

// 与 InventoryManager 的事件集成：装备同步 / 消耗品使用
public partial class CharacterState
{
    private bool _equipmentInitializedFromInventory;
    private bool _debugEquipmentInit = true; // 临时调试开关，验证后可设为 false

    // 对外公开：用于外部（例如 MapManager 补偿调用）强制尝试初始化一次
    public void EnsureEquipmentInitialized()
    {
        if (_equipmentInitializedFromInventory) return;
        if (_debugEquipmentInit) Debug.Log("[EquipInit] External EnsureEquipmentInitialized trigger.");
        TryInitializeEquipmentFromInventory();
    }

    private void OnEnable()
    {
        InventoryManager.OnItemEquipped += HandleItemEquipped;
        InventoryManager.OnItemUnequipped += HandleItemUnequipped;
        InventoryManager.OnItemConsumed += HandleItemConsumed;
        InventoryManager.OnInventoryUpdated += HandleInventoryUpdated;
    }

    private void OnDisable()
    {
        InventoryManager.OnItemEquipped -= HandleItemEquipped;
        InventoryManager.OnItemUnequipped -= HandleItemUnequipped;
        InventoryManager.OnItemConsumed -= HandleItemConsumed;
        InventoryManager.OnInventoryUpdated -= HandleInventoryUpdated;
    }

    private void HandleInventoryUpdated()
    {
        if (_debugEquipmentInit) Debug.Log("[EquipInit] OnInventoryUpdated received.");
        TryInitializeEquipmentFromInventory();
    }

    // 统一入口：只有在 Init 完成且背包加载完成后才执行一次
    private void TryInitializeEquipmentFromInventory()
    {
        if (_equipmentInitializedFromInventory)
        {
            if (_debugEquipmentInit) Debug.Log("[EquipInit] Already initialized, skip.");
            return;
        }
        if (!HasRunCoreInit)
        {
            if (_debugEquipmentInit) Debug.Log("[EquipInit] Core Init not finished yet, defer.");
            return; // 使用属性
        }
        var invMgr = InventoryManager.Instance;
        if (invMgr == null)
        {
            if (_debugEquipmentInit) Debug.Log("[EquipInit] InventoryManager null, defer.");
            return;
        }
        if (!invMgr.IsLoaded)
        {
            if (_debugEquipmentInit) Debug.Log("[EquipInit] Inventory not loaded, defer.");
            return;
        }
        var allItems = invMgr.AllItems;
        if (allItems == null)
        {
            if (_debugEquipmentInit) Debug.Log("[EquipInit] AllItems null -> mark initialized (empty backpack)." );
            _equipmentInitializedFromInventory = true;
            CurrentHealth = MaxHealth;
            OnValueChange();
            return;
        }
        int equipCount = 0;
        foreach (var invItem in allItems.Where(i => i.location == ItemLocation.Equipped))
        {
            var equipData = GameManager.Instance.ItemDataSo.GetEquipmentDataById(invItem.itemId);
            if (equipData == null) continue;
            var stored = BuildStoredEquipmentFromInventoryItem(invItem);
            EquipItem(stored);
            equipCount++;
        }
        _equipmentInitializedFromInventory = true;
        CurrentHealth = MaxHealth;
        OnValueChange();
        if (_debugEquipmentInit) Debug.Log($"[EquipInit] Initialized. Equipped items applied: {equipCount}. Final Attack={Attack}, MaxHP={MaxHealth}");
    }

    private void HandleItemEquipped(InventoryItem inventoryItem, EquipmentData equipmentData)
    {
        if (inventoryItem == null || equipmentData == null) return;
        var stored = BuildStoredEquipmentFromInventoryItem(inventoryItem);
        EquipItem(stored);
        if (CurrentHealth > MaxHealth)
        {
            CurrentHealth = MaxHealth;
            OnValueChange();
        }
    }

    private void HandleItemUnequipped(InventoryItem inventoryItem, EquipmentData equipmentData)
    {
        if (equipmentData == null) return;
        UnEquipItem(equipmentData.equipmentType);
        if (CurrentHealth > MaxHealth)
        {
            CurrentHealth = MaxHealth;
            OnValueChange();
        }
    }

    private void HandleItemConsumed(ConsumablesData consumablesData)
    {
        if (consumablesData == null) return;
        ApplyConsumableEffect(consumablesData);
    }

    private StoredEquipment BuildStoredEquipmentFromInventoryItem(InventoryItem invItem)
    {
        var se = new StoredEquipment
        {
            itemId = invItem.itemId,
            generatedProperties = invItem.generatedProperties != null
                ? invItem.generatedProperties.Select(p => p.DeepClone()).ToList()
                : new System.Collections.Generic.List<EquipmentProperty>(),
            quantity = invItem.quantity
        };

        // 补齐模板上的“固定/基础”属性（此处按设计为模板自定义属性 customProperties）
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
                    // 若模板未写入 actualValue，但提供 min/max，则生成一次实际值
                    if (Mathf.Approximately(clone.actualValue, 0f) && (!Mathf.Approximately(clone.minValue, 0f) || !Mathf.Approximately(clone.maxValue, 0f)))
                    {
                        clone.GenerateActualValue();
                        if (!clone.IsPercentage)
                        {
                            clone.actualValue = Mathf.RoundToInt(clone.actualValue);
                        }
                    }
                    se.generatedProperties.Add(clone);
                }
            }
        }

        return se;
    }
}
