using System.Collections.Generic;
using UnityEngine;

public class WorldDroppedItems : MonoBehaviour,IInteractable
{
    [SerializeField] private Transform uiParent;
    [Header("捡起来的时候会获得的物品id")]
    [SerializeField] private int itemID;
    [SerializeField] private int amount;
    /// <summary>
    /// 显示交互提示
    /// </summary>
    /// <param name="promptText">提示文本</param>
    // 由 PlayerInteraction 的中央提示负责显示/隐藏。
    public string GetInteractionPrompt()
    {
        var itemData = GameDataConfig.Instance.ItemDataSo.GetItemDataById(itemID);
        if (itemData != null)
            return $"拾取 {itemData.itemName} x{amount}";
        return "F";
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
        // 中央提示由 PlayerInteraction 负责隐藏/显示
        // 实现拾取物品的逻辑
        Debug.Log("拾取物品: " + gameObject.name);
        // 捡起物品并加入背包
        var temp = new Vector2Int(itemID, amount); 
        var itemIDs = new List<Vector2Int>() { temp };
        InventoryManager.Instance.PickupItems(itemIDs);
        Destroy(gameObject);
    }
}
