using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WorldShopData", menuName = "Data/WorldShopData")]
public class WorldShopDataSO : ScriptableObject
{
    [Header("世界商店物品配置")]
    public List<WorldShopItem> shopItems;
}

[System.Serializable]
public class WorldShopItem
{
    [Header("物品ID")] public int itemId;
    [Header("可购买数量限制,负数就是无限")] public int purchaseLimit;
    [Header("物品价格")] public int price;
    // 原始配置购买限制（运行时赋值，用于计算剩余），不进行序列化保存
    [NonSerialized] public int originalPurchaseLimit;
}