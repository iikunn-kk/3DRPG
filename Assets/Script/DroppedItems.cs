using System.Collections.Generic;
using UnityEngine;

public class DroppedItems : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform uiParent;
    private List<Vector2Int> _itemIDs = new();

    public void Init(List<Vector2Int> itemIDs)
    {
        // 存储传入的物品ID和数量列表，供拾取时使用
        if (itemIDs == null) return;
        _itemIDs = new List<Vector2Int>(itemIDs);

        // 可选：为场景中的掉落物添加一个简单的浮动文本或图标
        // 这里尝试在物体上方显示第一个物品的名称（如果存在）
        var first = _itemIDs.Count > 0 ? _itemIDs[0] : (Vector2Int?)null;
        if (first != null)
        {
            var itemData = GameDataConfig.Instance.ItemDataSo.GetItemDataById(first.Value.x);
            if (itemData != null)
            {
                // 中央化提示将根据 GetInteractionPrompt 返回的字符串显示
            }
        }
    }

    // InteractionPrompt 现在由 PlayerInteraction 的中央实例负责显示/隐藏。
    // 通过下面的接口方法，PlayerInteraction 将获取提示文本和锚点。

    public string GetInteractionPrompt()
    {
        var first = _itemIDs.Count > 0 ? _itemIDs[0] : (Vector2Int?)null;
        if (first != null)
        {
            var itemData = GameDataConfig.Instance.ItemDataSo.GetItemDataById(first.Value.x);
            if (itemData != null)
                return $"拾取 {itemData.itemName} x{first.Value.y}";
        }
        return "F"; // 回退提示
    }

    public Transform GetPromptAnchor()
    {
        return uiParent != null ? uiParent : transform;
    }

    /// <summary>
    /// 执行交互逻辑
    /// </summary>
    /// <param name="playerInteraction">玩家交互组件</param>
    public void Interact(PlayerInteraction playerInteraction)
    {
        // 中央提示由 PlayerInteraction 负责隐藏/显示，所以这里不再处理提示的销毁
        // 实现拾取物品的逻辑
        Debug.Log("拾取物品: " + gameObject.name);
        // 捡起物品并加入背包
        InventoryManager.Instance.PickupItems(_itemIDs);
        Destroy(gameObject);
    }
}