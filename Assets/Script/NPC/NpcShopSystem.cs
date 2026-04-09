using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC商店系统类
/// </summary>
public class NpcShopSystem : MonoBehaviour
{
    /// <summary>
    /// 获取指定NPC商店配置数据中的物品列表
    /// </summary>
    /// <param name="shopData">NPC商店配置数据</param>
    /// <returns>商店物品列表</returns>
    public List<NpcShopDataSO.ShopItemInfo> GetShopItems(NpcShopDataSO shopData)
    {
        // 检查商店配置数据是否存在
        if (shopData != null && shopData.shopItems != null)
        {
            return shopData.shopItems;
        }
        
        // 如果没有商店配置，返回空列表
        return new List<NpcShopDataSO.ShopItemInfo>();
    }

    /// <summary>
    /// 购买物品
    /// </summary>
    /// <param name="shopItem">要购买的商店物品</param>
    /// <param name="quantity">购买数量</param>
    /// <returns>购买是否成功</returns>
    public bool PurchaseItem(NpcShopItem shopItem, int quantity = 1)
    {
        if (shopItem == null || quantity <= 0)
            return false;

        // 获取物品数据
        ItemData itemData = GetItemDataById(shopItem.itemId);
        if (itemData == null)
        {
            Debug.LogError($"未找到ID为 {shopItem.itemId} 的物品数据");
            return false;
        }

        // 检查商店库存是否足够（无限库存除外）
        if (shopItem.purchaseLimit >= 0 && shopItem.purchaseLimit < quantity)
        {
            Debug.Log("商店库存不足");
            return false;
        }

        // 检查玩家是否有足够的金币
        int totalPrice = shopItem.price * quantity;
        // TODO: 实际项目中需要实现金币系统并检查玩家金币
        // if (player.gold < totalPrice) 
        // {
        //     Debug.Log("金币不足");
        //     return false;
        // }

        // 扣除商店库存（无限库存除外）
        if (shopItem.purchaseLimit >= 0)
        {
            shopItem.purchaseLimit -= quantity;
        }

        // 给玩家添加物品
        bool success = InventoryManager.Instance.AddItem(shopItem.itemId, quantity);
        if (!success)
        {
            Debug.LogError($"无法将物品添加到背包: {itemData.itemName}");
            return false;
        }

        // TODO: 扣除玩家金币
        // player.gold -= totalPrice;

        Debug.Log($"购买了 {quantity} 个 {itemData.itemName}，花费 {totalPrice} 金币");
        return true;
    }

    /// <summary>
    /// 出售物品
    /// </summary>
    /// <param name="inventoryItem">要出售的背包物品</param>
    /// <param name="quantity">出售数量</param>
    /// <returns>出售是否成功</returns>
    public bool SellItem(InventoryItem inventoryItem, int quantity)
    {
        if (inventoryItem == null || quantity <= 0)
            return false;

        // 获取物品数据
        ItemData itemData = GetItemDataById(inventoryItem.itemId);
        if (itemData == null)
        {
            Debug.LogError($"未找到ID为 {inventoryItem.itemId} 的物品数据");
            return false;
        }

        // 计算出售单价（若未设置则使用默认）
        int unitPrice = itemData.GetMySellPrice();
        if (unitPrice <= 0)
        {
            unitPrice = GetDefaultSellPrice(itemData.itemType);
        }

        bool removed = false;

        if (itemData.itemType == ItemType.消耗品 || itemData.itemType == ItemType.材料)
        {
            if (inventoryItem.count < quantity)
            {
                Debug.Log("物品数量不足");
                return false;
            }
            removed = InventoryManager.Instance.ReduceItemCount(inventoryItem.instanceId, quantity);
        }
        else if (itemData.itemType == ItemType.装备)
        {
            if (quantity != 1)
            {
                quantity = 1;
            }
            removed = InventoryManager.Instance.RemoveItem(inventoryItem.instanceId);
        }
        else
        {
            if (inventoryItem.count < quantity)
            {
                Debug.Log("物品数量不足");
                return false;
            }
            removed = InventoryManager.Instance.ReduceItemCount(inventoryItem.instanceId, quantity);
        }

        if (!removed)
        {
            Debug.LogError($"无法从背包中移除物品: {itemData.itemName}");
            return false;
        }

        int totalPrice = unitPrice * quantity;
        // TODO: 增加玩家金币
        // player.gold += totalPrice;

        Debug.Log($"出售了 {quantity} 个 {itemData.itemName}，获得 {totalPrice} 金币");
        return true;
    }
    
    /// <summary>
    /// 根据物品ID获取物品数据
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <returns>物品数据</returns>
    private ItemData GetItemDataById(int itemId)
    {
        // 通过GameManager获取ItemDataSO引用
        if (GameManager.Instance != null && GameManager.Instance.ItemDataSo != null)
        {
            return GameManager.Instance.ItemDataSo.GetItemDataById(itemId);
        }
        
        Debug.LogError("无法获取物品数据，GameManager或ItemDataSO未正确初始化");
        return null;
    }
    
    /// <summary>
    /// 根据物品类型获取默认出售价格
    /// </summary>
    /// <param name="itemType">物品类型</param>
    /// <returns>默认出售价格</returns>
    public static int GetDefaultSellPrice(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.装备:
                return 100;
            case ItemType.消耗品:
                return 20;
            case ItemType.材料:
                return 10;
            default:
                return 0;
        }
    }
}