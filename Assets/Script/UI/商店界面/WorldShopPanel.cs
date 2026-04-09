using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class WorldShopPanel : UIPopPanelBase
{
    [SerializeField] private WorldShopPurchaseQuantityPanel worldShopPurchaseQuantityPanel;
    [SerializeField] private Transform itemParent;
    [SerializeField] private TMP_Text gemText;
    [SerializeField] private GameObject itemPrefab;//WorldShopItemPrefab
    
    [Header("世界商店配置数据")]
    [SerializeField] private WorldShopDataSO worldShopData;
    
    private List<WorldShopItemPrefab> shopItemPrefabs = new List<WorldShopItemPrefab>();
    
    protected override void Awake()
    {
        base.Awake();
        InitShopItems();
    }
    
    private void InitShopItems()
    {
        foreach (var item in shopItemPrefabs) { if(item) Destroy(item.gameObject);} shopItemPrefabs.Clear();
        if (worldShopData == null || worldShopData.shopItems == null) return;
        var runtimeItems = new List<WorldShopItem>();
        foreach (var cfg in worldShopData.shopItems)
        {
            var originalLimit = cfg.purchaseLimit; // 配置中的原始限制（可能为-1）
            int purchased;
            int remain = originalLimit;
            if (originalLimit >= 0)
            {
                purchased = ShopPurchaseHelper.GetWorldShopPurchasedCount(cfg.itemId);
                remain = ShopPurchaseHelper.GetRemaining(originalLimit, purchased);
            }
            var runtime = new WorldShopItem
            {
                itemId = cfg.itemId,
                price = cfg.price,
                purchaseLimit = remain,
                originalPurchaseLimit = originalLimit
            };
            runtimeItems.Add(runtime);
        }
        var sorted = SortShopItems(runtimeItems);
        foreach (var rItem in sorted)
        {
            var go = Instantiate(itemPrefab, itemParent);
            var comp = go.GetComponent<WorldShopItemPrefab>();
            if (comp != null)
            {
                comp.Init(rItem, this);
                shopItemPrefabs.Add(comp);
            }
        }

        gemText.text = PlayerCurrencyManager.Instance?.Diamonds.ToString();
    }
    
    /// <summary>
    /// 对商店物品进行排序
    /// </summary>
    /// <param name="shopItems">待排序的商店物品列表</param>
    /// <returns>排序后的商店物品列表</returns>
    private List<WorldShopItem> SortShopItems(List<WorldShopItem> shopItems)
    {
        return shopItems.OrderBy(item => IsItemPurchasable(item) ? 0 : 1)  // 可购买的排在前面
                       .ThenBy(GetItemTypePriority)         // 按物品类型排序
                       .ToList();
    }
    
    /// <summary>
    /// 判断物品是否可以购买
    /// </summary>
    /// <param name="shopItem">商店物品</param>
    /// <returns>是否可以购买</returns>
    private bool IsItemPurchasable(WorldShopItem shopItem)
    {
        return shopItem.purchaseLimit != 0; // purchaseLimit 已代表剩余次数
    }
    
    /// <summary>
    /// 获取物品类型的优先级（用于排序）
    /// </summary>
    /// <param name="shopItem">商店物品</param>
    /// <returns>物品类型优先级，装备返回0，消耗品返回1</returns>
    private int GetItemTypePriority(WorldShopItem shopItem)
    {
        // 获取物品数据（使用通用的 GetItemDataById，支持所有物品类型）
        var itemData = GameManager.Instance?.ItemDataSo?.GetItemDataById(shopItem.itemId);
        if (itemData != null)
        {
            // 装备类型优先级为0（排在前面）
            if (itemData.itemType == ItemType.装备)
            {
                return 0;
            }
            // 消耗品类型优先级为1（排在装备后面）
            else if (itemData.itemType == ItemType.消耗品)
            {
                return 1;
            }
        }
        
        // 其他类型或未找到物品数据的优先级为2（排在最后）
        return 2;
    }
    
    /// <summary>
    /// 显示购买数量选择面板
    /// </summary>
    /// <param name="shopItem">要购买的商店物品</param>
    /// <param name="itemPrice">物品价格</param>
    /// <param name="onConfirm">确认购买的回调</param>
    public void ShowPurchasePanel(WorldShopItem shopItem, int itemPrice, System.Action<int> onConfirm)
    {
        if (worldShopPurchaseQuantityPanel != null)
        {
            worldShopPurchaseQuantityPanel.Init(shopItem, itemPrice, onConfirm);
        }
    }
    
    public void OnGemTextUpdate(int value)
    {
        gemText.text = value.ToString();
    }
    
    public void OnCloseButtonClick()
    {
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
        UIManager.Instance.ClosePanel<WorldShopPanel>();
        Hide();
    }

    public void AddGem()
    {
        PlayerCurrencyManager.Instance.AddDiamonds(50000);
    }
}