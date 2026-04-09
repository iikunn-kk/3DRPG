using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC商店配置数据ScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "NpcShopData", menuName = "NPC/Shop Data", order = 1)]
public class NpcShopDataSO : ScriptableObject
{
    /// <summary>
    /// 商店物品信息类
    /// </summary>
    [System.Serializable]
    public class ShopItemInfo
    {
        [Header("物品设置")]
        public int itemId;          // 物品ID
        public int price;           // 价格
        [Tooltip("-1表示无限库存")]
        public int purchaseLimit = -1; // 购买限制（-1表示无限）
    }

    [Header("商店设置")]
    [Tooltip("商店中可购买的物品列表")]
    public List<ShopItemInfo> shopItems = new List<ShopItemInfo>();
}
