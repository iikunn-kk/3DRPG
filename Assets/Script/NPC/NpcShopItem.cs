using System;
using UnityEngine;

/// <summary>
/// NPC商店物品类，用于表示商店中的物品信息
/// </summary>
[System.Serializable]
public class NpcShopItem
{
    [Header("物品ID")]
    public int itemId;
    
    [Header("可购买数量限制,负数就是无限")]
    public int purchaseLimit;
    
    [Header("物品价格")]
    public int price;
    
    [Header("物品名称")]
    public string itemName;
    
    [Header("物品图标")]
    public Sprite itemIcon;

    // 原始配置限制（运行时赋值，不序列化）
    [NonSerialized] public int originalPurchaseLimit;
}