// 文件名: IDropTarget.cs
using UnityEngine;

/// <summary>
/// 一个接口，用于标识一个UI组件可以作为物品拖放的目标。
/// </summary>
public interface IDropTarget
{
    /// <summary>
    /// 获取此目标在物品系统中的位置类型（背包、装备栏、快捷栏）。
    /// </summary>
    ItemLocation Location { get; }

    /// <summary>
    /// 获取此目标在对应位置中的格子索引。
    /// </summary>
    int SlotIndex { get; }
    
    // 可以在此添加更多方法，例如 CanDropItem(InventoryItem item) 来进行更复杂的放置规则判断
}