using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC商店管理器，处理商店的购买和出售逻辑
/// 该组件应与NpcShopPanel挂载在同一GameObject上
/// </summary>
public class NpcShopManager : MonoBehaviour
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
        if (PlayerCurrencyManager.Instance.Money < totalPrice)
        {
            Debug.Log("金币不足");
            return false;
        }

        // 扣除玩家金币
        if (!PlayerCurrencyManager.Instance.RemoveMoney(totalPrice))
        {
            Debug.LogError("扣除玩家金币失败");
            return false;
        }

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
            // 如果添加物品失败，退还金币
            PlayerCurrencyManager.Instance.AddMoney(totalPrice);
            return false;
        }

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

        // 优先根据传入实例ID定位当前有效物品（避免异步加载后引用失效）
        var liveItem = InventoryManager.Instance.GetItemByInstanceId(inventoryItem.instanceId);
        // 获取物品数据
        ItemData itemData = GetItemDataById(inventoryItem.itemId);
        if (itemData == null)
        {
            Debug.LogError($"未找到ID为 {inventoryItem.itemId} 的物品数据");
            return false;
        }
        // 如果实例ID已失效，使用位置+格子+模板ID 进行回退匹配（仅限背包区）
        if (liveItem == null)
        {
            foreach (var it in InventoryManager.Instance.GetInventoryItems())
            {
                if (it.itemId == inventoryItem.itemId && it.slotIndex == inventoryItem.slotIndex)
                {
                    liveItem = it;
                    break;
                }
            }
            // 对于堆叠类，若位置匹配失败，再做一次宽松匹配（同ID且数量足够）。装备不做宽松匹配，避免误卖其它同ID装备。
            if (liveItem == null && (itemData.itemType == ItemType.消耗品 || itemData.itemType == ItemType.材料))
            {
                foreach (var it in InventoryManager.Instance.GetInventoryItems())
                {
                    if (it.itemId == inventoryItem.itemId && it.count >= quantity)
                    {
                        liveItem = it;
                        break;
                    }
                }
            }
        }
        // 如果还是找不到，提示并退出
        if (liveItem == null)
        {
            Debug.LogWarning("待出售的物品已发生变化，请重新打开商店后再试");
            UIManager.Instance?.ShowToast("物品状态已变化，请重试");
            return false;
        }

        bool removed = false;

        if (itemData.itemType == ItemType.消耗品 || itemData.itemType == ItemType.材料)
        {
            // 检查玩家是否有足够物品（堆叠类）
            if (liveItem.count < quantity)
            {
                Debug.Log("物品数量不足");
                return false;
            }

            // 从玩家背包中移除对应数量
            removed = InventoryManager.Instance.ReduceItemCount(liveItem.instanceId, quantity);
        }
        else if (itemData.itemType == ItemType.装备)
        {
            // 装备为非堆叠物品，一次只能卖掉一个实例
            if (quantity != 1)
            {
                quantity = 1;
            }
            removed = InventoryManager.Instance.RemoveItem(liveItem.instanceId);
        }
        else
        {
            // 其他类型，默认按堆叠处理
            if (liveItem.count < quantity)
            {
                Debug.Log("物品数量不足");
                return false;
            }
            removed = InventoryManager.Instance.ReduceItemCount(liveItem.instanceId, quantity);
        }

        if (!removed)
        {
            Debug.LogError($"无法从背包中移除物品: {itemData.itemName}");
            return false;
        }

        int unitPrice = itemData.GetMySellPrice();
        if (unitPrice <= 0)
        {
            unitPrice = GetDefaultSellPrice(itemData.itemType);
        }
        int totalPrice = unitPrice * quantity;
        if (!PlayerCurrencyManager.Instance.AddMoney(totalPrice))
        {
            Debug.LogError("增加玩家金币失败");
            InventoryManager.Instance.AddItem(inventoryItem.itemId, quantity);
            return false;
        }

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

    /// <summary>
    /// 购买物品（带NPC ID）
    /// </summary>
    /// <param name="npcId">NPC ID</param>
    /// <param name="shopItem">要购买的商店物品</param>
    /// <param name="quantity">购买数量</param>
    /// <returns>购买是否成功</returns>
    public bool PurchaseItem(int npcId, NpcShopItem shopItem, int quantity = 1)
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

        // 初始化原始限制（一次）
        if (shopItem.originalPurchaseLimit == 0 && shopItem.purchaseLimit > 0)
        {
            shopItem.originalPurchaseLimit = shopItem.purchaseLimit;
        }

        // 读取持久化已购买次数
        if (shopItem.purchaseLimit >= 0 && shopItem.originalPurchaseLimit > 0)
        {
            int purchased = ShopPurchaseHelper.GetNpcShopPurchasedCount(npcId, shopItem.itemId);
            int remaining = ShopPurchaseHelper.GetRemaining(shopItem.originalPurchaseLimit, purchased);
            shopItem.purchaseLimit = remaining; // 同步为当前剩余
        }

        // 检查商店库存是否足够（无限库存除外）
        if (shopItem.purchaseLimit >= 0 && shopItem.purchaseLimit < quantity)
        {
            Debug.Log("商店库存不足");
            return false;
        }

        int totalPrice = shopItem.price * quantity;
        if (PlayerCurrencyManager.Instance.Money < totalPrice)
        {
            Debug.Log("金币不足");
            return false;
        }

        if (!PlayerCurrencyManager.Instance.RemoveMoney(totalPrice))
        {
            Debug.LogError("扣除玩家金币失败");
            return false;
        }

        bool success = InventoryManager.Instance.AddItem(shopItem.itemId, quantity);
        if (!success)
        {
            PlayerCurrencyManager.Instance.AddMoney(totalPrice);
            Debug.LogError($"无法将物品添加到背包: {itemData.itemName}");
            return false;
        }

        // 更新持久化购买次数
        if (shopItem.purchaseLimit >= 0 && quantity > 0)
        {
            ShopPurchaseHelper.AddNpcShopPurchasedCount(npcId, shopItem.itemId, quantity);
            int purchased = ShopPurchaseHelper.GetNpcShopPurchasedCount(npcId, shopItem.itemId);
            if (shopItem.originalPurchaseLimit > 0)
            {
                shopItem.purchaseLimit = ShopPurchaseHelper.GetRemaining(shopItem.originalPurchaseLimit, purchased);
            }
        }

        Debug.Log($"购买了 {quantity} 个 {itemData.itemName}，花费 {totalPrice} 金币");
        return true;
    }
}