using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// NPC商店面板类，用于显示商店物品和玩家背包
/// </summary>
public class NpcShopPanel : UIPopPanelBase
{
    [Header("商店UI引用")]
    [SerializeField] private Transform shopItemParent;           // 商店物品父对象
    [SerializeField] private Transform playerInventoryParent;    // 玩玩家背包物品父对象
    [SerializeField] private GameObject shopItemPrefab;          // 商店物品预制件
    [SerializeField] private GameObject inventoryItemPrefab;     // 背包物品预制件
    [SerializeField] private TMP_Text npcNameText;               // NPC名称文本
    [SerializeField] private TMP_Text playerGoldText;            // 玩家金币文本

    [Header("购买/出售数量面板")]
    [SerializeField] private NpcShopQuantityPanel quantityPanel; // 数量选择面板
    
    [Header("详细信息面板")]
    [SerializeField] private SlotDetailsPanel slotDetailsPanel; // 普通详情面板
    [SerializeField] private EquipSlotDetailsPanel equipSlotDetailsPanel; // 装备专用详情面板（用于玩家背包区）

    private NpcBase _currentNpc;                                  // 当前交互的NPC
    private List<NpcShopItemUI> _shopItemUIs = new List<NpcShopItemUI>();       // 商店物品UI列表
    private List<NpcInventoryItemUI> _inventoryItemUIs = new List<NpcInventoryItemUI>(); // 背包物品UI列表
    
    [SerializeField] private NpcShopManager shopManager;                            // 商店管理器引用

    // 防重入：防止出售回调在极端情况下被触发两次导致加钱两次
    private bool _isProcessingSell;

    private void OnEnable()
    {
        // 监听背包更新事件
        InventoryManager.OnInventoryUpdated += RefreshInventoryDisplay;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        // 取消监听背包更新事件
        InventoryManager.OnInventoryUpdated -= RefreshInventoryDisplay;
        // 隐藏详情
        HideDetails();
    }

    /// <summary>
    /// 初始化商店面板
    /// </summary>
    /// <param name="npc">交互的NPC</param>
    public void Init(NpcBase npc)
    {
        if (npc == null || npc.NpcData == null)
        {
            Debug.LogError("NPC或NPC数据为空");
            return;
        }

        _currentNpc = npc;
        _isProcessingSell = false; // 重置防重入
        
        // 设置NPC名称
        if (npcNameText != null)
        {
            npcNameText.text = npc.NpcData.NpcName+"的商店";
        }
        
        // 更新玩家金币显示
        UpdatePlayerGoldDisplay();
        
        // 初始化商店物品显示
        InitShopItems();
        
        // 初始化玩家背包显示
        InitInventoryItems();
        Show();
    }

    /// <summary>
    /// 初始化商店物品显示
    /// </summary>
    private void InitShopItems()
    {
        // 清空现有商店物品UI
        ClearShopItems();

        if (_currentNpc == null || _currentNpc.NpcData == null || _currentNpc.NpcData.npcShopData == null ||
            _currentNpc.NpcData.npcShopData.shopItems == null || _currentNpc.NpcData.npcShopData.shopItems.Count == 0)
        {
            Debug.Log("该NPC没有商店配置或商店中没有物品");
            return;
        }

        int npcId = _currentNpc.NpcData.NpcID;
        // 将ShopItemInfo转换为NpcShopItem列表并根据持久化记录计算剩余
        List<NpcShopItem> shopItems = new List<NpcShopItem>();
        foreach (var shopItemInfo in _currentNpc.NpcData.npcShopData.shopItems)
        {
            ItemData itemData = GameDataConfig.Instance?.ItemDataSo?.GetItemDataById(shopItemInfo.itemId);
            if (itemData != null)
            {
                var npcItem = new NpcShopItem
                {
                    itemId = shopItemInfo.itemId,
                    price = shopItemInfo.price,
                    purchaseLimit = shopItemInfo.purchaseLimit,
                    itemName = itemData.itemName,
                    itemIcon = itemData.itemSprite,
                    originalPurchaseLimit = shopItemInfo.purchaseLimit > 0 ? shopItemInfo.purchaseLimit : 0
                };
                if (shopItemInfo.purchaseLimit >= 0 && shopItemInfo.purchaseLimit > 0)
                {
                    int purchased = ShopPurchaseHelper.GetNpcShopPurchasedCount(npcId, shopItemInfo.itemId);
                    npcItem.purchaseLimit = ShopPurchaseHelper.GetRemaining(shopItemInfo.purchaseLimit, purchased);
                }
                shopItems.Add(npcItem);
            }
        }

        var sortedShopItems = SortShopItems(shopItems);
        foreach (var shopItem in sortedShopItems)
        {
            if (shopItemPrefab != null && shopItemParent != null)
            {
                GameObject itemGo = Instantiate(shopItemPrefab, shopItemParent);
                NpcShopItemUI itemUI = itemGo.GetComponent<NpcShopItemUI>();
                if (itemUI != null)
                {
                    itemUI.Init(shopItem, this);
                    _shopItemUIs.Add(itemUI);
                }
                else
                {
                    Debug.LogError("商店物品预制件缺少NpcShopItemUI组件");
                    Destroy(itemGo);
                }
            }
        }
    }

    /// <summary>
    /// 初始化玩家背包物品显示
    /// </summary>
    private void InitInventoryItems()
    {
        // 清空现有背包物品UI
        ClearInventoryItems();

        // 获取玩家背包中的物品
        var inventoryItems = InventoryManager.Instance.GetInventoryItems();
        if (inventoryItems == null) return;

        // 为每个背包物品创建UI
        foreach (var inventoryItem in inventoryItems)
        {
            if (inventoryItemPrefab != null && playerInventoryParent != null)
            {
                GameObject itemGo = Instantiate(inventoryItemPrefab, playerInventoryParent);
                NpcInventoryItemUI itemUI = itemGo.GetComponent<NpcInventoryItemUI>();
                
                if (itemUI != null)
                {
                    // 使用 InventoryItem 级别的回调以便显示实例属性（比如随机词条）
                    itemUI.Init(inventoryItem, (Action<InventoryItem>)ShowDetails, HideDetails, OnInventoryItemRightClicked);
                    _inventoryItemUIs.Add(itemUI);
                }
                else
                {
                    Debug.LogError("背包物品预制件缺少NpcInventoryItemUI组件");
                    Destroy(itemGo);
                }
            }
        }
    }
    
    /// <summary>
    /// 当背包物品被右键点击时的回调
    /// </summary>
    /// <param name="inventoryItem">被点击的物品</param>
    private void OnInventoryItemRightClicked(InventoryItem inventoryItem)
    {
        // 显示出售数量选择面板
        ShowSellQuantityPanel(inventoryItem);
    }
    
    /// <summary>
    /// 清空商店物品UI
    /// </summary>
    private void ClearShopItems()
    {
        foreach (var itemUI in _shopItemUIs)
        {
            if (itemUI != null && itemUI.gameObject != null)
            {
                Destroy(itemUI.gameObject);
            }
        }
        _shopItemUIs.Clear();
    }

    /// <summary>
    /// 清空背包物品UI
    /// </summary>
    private void ClearInventoryItems()
    {
        foreach (var itemUI in _inventoryItemUIs)
        {
            if (itemUI != null && itemUI.gameObject != null)
            {
                Destroy(itemUI.gameObject);
            }
        }
        _inventoryItemUIs.Clear();
    }

    /// <summary>
    /// 对商店物品进行排序
    /// </summary>
    /// <param name="shopItems">待排序的商店物品列表</param>
    /// <returns>排序后的商店物品列表</returns>
    private List<NpcShopItem> SortShopItems(List<NpcShopItem> shopItems)
    {
        shopItems.Sort((a, b) => 
        {
            // 可购买的排在前面
            bool aPurchasable = a.purchaseLimit != 0;
            bool bPurchasable = b.purchaseLimit != 0;
            
            if (aPurchasable != bPurchasable)
            {
                return bPurchasable.CompareTo(aPurchasable);
            }
            
            // 按物品类型排序
            var aItemData = GameDataConfig.Instance?.ItemDataSo?.GetItemDataById(a.itemId);
            var bItemData = GameDataConfig.Instance?.ItemDataSo?.GetItemDataById(b.itemId);
            
            int aTypePriority = GetItemTypePriority(aItemData);
            int bTypePriority = GetItemTypePriority(bItemData);
            
            return aTypePriority.CompareTo(bTypePriority);
        });
        
        return shopItems;
    }
    
    /// <summary>
    /// 获取物品类型的优先级（用于排序）
    /// </summary>
    /// <param name="itemData">物品数据</param>
    /// <returns>物品类型优先级</returns>
    private int GetItemTypePriority(ItemData itemData)
    {
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
    public void ShowPurchaseQuantityPanel(NpcShopItem shopItem)
    {
        if (quantityPanel != null && shopItem != null)
        {
            print("召唤显示购买面板,名字是"+shopItem.itemName+" 数量是"+shopItem.purchaseLimit+" 价格是"+shopItem.price);
            quantityPanel.Init(shopItem, NpcShopQuantityPanel.PanelMode.Purchase, OnPurchaseConfirm);
        }
    }

    /// <summary>
    /// 显示出售数量选择面板
    /// </summary>
    /// <param name="inventoryItem">要出售的背包物品</param>
    public void ShowSellQuantityPanel(InventoryItem inventoryItem)
    {
        if (quantityPanel != null && inventoryItem != null)
        {
            quantityPanel.Init(inventoryItem, NpcShopQuantityPanel.PanelMode.Sell, OnSellConfirm);
        }
    }

    /// <summary>
    /// 确认购买回调
    /// </summary>
    /// <param name="shopItem">商店物品</param>
    /// <param name="quantity">购买数量</param>
    private void OnPurchaseConfirm(NpcShopItem shopItem, int quantity)
    {
        if (shopManager == null || _currentNpc == null) return;
        int npcId = _currentNpc.NpcData.NpcID;
        bool success = shopManager.PurchaseItem(npcId, shopItem, quantity);
        if (success)
        {
            Debug.Log($"成功购买 {quantity} 个 {shopItem.itemName}");
            RefreshInventoryDisplay();
            // 重新生成商店物品以更新剩余次数显示
            InitShopItems();
        }
        else
        {
            Debug.Log("购买失败");
        }
    }

    /// <summary>
    /// 确认出售回调
    /// </summary>
    /// <param name="inventoryItem">背包物品</param>
    /// <param name="quantity">出售数量</param>
    private void OnSellConfirm(InventoryItem inventoryItem, int quantity)
    {
        if (shopManager == null) return;
        if (_isProcessingSell) return; // 防止重复提交
        _isProcessingSell = true;
        try
        {
            bool success = shopManager.SellItem(inventoryItem, quantity);
            if (success)
            {
                Debug.Log($"成功出售 {quantity} 个物品ID {inventoryItem.itemId}");
                // 刷新显示
                RefreshInventoryDisplay();
            }
            else
            {
                Debug.Log("出售失败");
            }
        }
        finally
        {
            _isProcessingSell = false;
        }
    }

    /// <summary>
    /// 刷新背包显示
    /// </summary>
    private void RefreshInventoryDisplay()
    {
        InitInventoryItems();
        UpdatePlayerGoldDisplay();
    }

    /// <summary>
    /// 更新玩家金币显示
    /// </summary>
    private void UpdatePlayerGoldDisplay()
    {
        // 获取玩家实际金币数量
        int playerGold = CharacterService.Instance?.Money ?? 0;
        if (playerGoldText != null)
        {
            playerGoldText.text = $"金币: {playerGold}";
        }
    }
    
    /// <summary>
    /// 显示物品详细信息（用于商店物品模板显示）
    /// </summary>
    public void ShowDetails(ItemData itemData)
    {
        if (itemData == null) return;
        if (equipSlotDetailsPanel != null) equipSlotDetailsPanel.HideDetails();
        if (slotDetailsPanel != null)
        {
            slotDetailsPanel.ShowDetails(itemData);
        }
    }

    /// <summary>
    /// 显示物品详细信息（用于玩家背包区，带实例属性）
    /// </summary>
    public void ShowDetails(InventoryItem item)
    {
        if (item == null) return;
        var data = GameDataConfig.Instance.ItemDataSo.GetItemDataById(item.itemId);
        bool isEquipment = data is EquipmentData;
        if (isEquipment)
        {
            if (slotDetailsPanel != null) slotDetailsPanel.HideDetails();
            if (equipSlotDetailsPanel != null)
            {
                equipSlotDetailsPanel.ShowDetails(item);
            }
            else if (slotDetailsPanel != null)
            {
                // 兜底：未配置装备面板仍然使用普通面板
                slotDetailsPanel.ShowDetails(item);
            }
        }
        else
        {
            if (equipSlotDetailsPanel != null) equipSlotDetailsPanel.HideDetails();
            if (slotDetailsPanel != null)
            {
                // 使用NpcShopSlotDetailsPanel的InventoryItem版本（若实现），否则退回到模板展示
                slotDetailsPanel.ShowDetails(item);
            }
        }
    }
    
    /// <summary>
    /// 隐藏物品详细信息
    /// </summary>
    public void HideDetails()
    {
        if (slotDetailsPanel != null)
        {
            slotDetailsPanel.HideDetails();
        }
        if (equipSlotDetailsPanel != null)
        {
            equipSlotDetailsPanel.HideDetails();
        }
    }

    /// <summary>
    /// 关闭按钮点击回调
    /// </summary>
    public void OnCloseButtonClicked()
    {
        UIManager.Instance.ClosePanel<NpcShopPanel>();
        Hide();
    }
    public void OnMoneyChange(int value)
    {
        UpdatePlayerGoldDisplay();
    }
}
