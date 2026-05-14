using UnityEngine;

/// <summary>
/// 消耗品事件集成 — 监听 InventoryManager 的消耗品使用事件
/// （装备相关事件已迁移到 EquipmentController）
/// </summary>
public partial class CharacterState
{
    private void OnEnable()
    {
        InventoryManager.OnItemConsumed += HandleItemConsumed;
    }

    private void OnDisable()
    {
        InventoryManager.OnItemConsumed -= HandleItemConsumed;
    }

    private void HandleItemConsumed(ConsumablesData consumablesData)
    {
        if (consumablesData == null) return;
        ApplyConsumableEffect(consumablesData);
    }
}
