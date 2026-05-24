using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldShopItemPrefab : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text itemName;
    [SerializeField] private TMP_Text price;
    [SerializeField] private TMP_Text canBuyNumber;
    [SerializeField] private Button itemButton;
   
    private int itemId;
    private int itemPrice;
    private int purchaseLimit;
    private WorldShopPanel shopPanel;
    private WorldShopItem shopItem;
    
    public void Init(WorldShopItem item, WorldShopPanel panel)
    {
        // Ensure price is set from the shop config even if item data lookup fails
        itemPrice = item.price;
        if (price != null) price.text = itemPrice.ToString();

        // 获取物品数据（使用通用的 GetItemDataById，支持所有物品类型）
        var itemData = GameDataConfig.Instance?.ItemDataSo?.GetItemDataById(item.itemId);
        if (itemData != null)
        {
            // 设置物品显示信息
            if (icon != null) icon.sprite = itemData.itemSprite;
            if (itemName != null) itemName.text = itemData.itemName;
        }
        else
        {
            // 当未找到 ItemData 时使用合理的回退显示，防止 UI 一片空白或显示错误数据
            if (icon != null) icon.sprite = null;
            if (itemName != null) itemName.text = $"Item_{item.itemId}";
            Debug.LogWarning($"WorldShopItemPrefab: 未找到 itemId={item.itemId} 的 ItemData，使用回退显示。");
        }

        // 保存购买限制 (此时 purchaseLimit 已表示剩余次数)
        purchaseLimit = item.purchaseLimit;

        // 设置可购买数量显示
        UpdatePurchaseLimitDisplay();

        // 保存引用
        this.shopItem = item;
        itemId = item.itemId;
        shopPanel = panel;

        // 添加按钮点击事件
        if (itemButton != null)
        {
            itemButton.onClick.AddListener(OnItemClicked);
            // 根据购买限制设置按钮状态
            UpdateButtonState();
        }
  
    }
    
    // 更新购买限制显示
    private void UpdatePurchaseLimitDisplay()
    {
        if (canBuyNumber != null)
        {
            if (purchaseLimit <= 0 && purchaseLimit != -1) // 已达到购买限制且不是无限购买
            {
                canBuyNumber.text = "已到购买上限";
            }
            else if (purchaseLimit == -1) // 无限购买
            {
                canBuyNumber.text = "无限";
            }
            else // 显示剩余购买次数
            {
                canBuyNumber.text = purchaseLimit.ToString();
            }
        }
    }
    
    // 更新按钮状态
    private void UpdateButtonState()
    {
        if (itemButton != null)
        {
            // 如果购买限制为0（已达到购买上限），禁用按钮
            itemButton.interactable = purchaseLimit != 0;
            print("按钮的状态是"+ itemButton.interactable);
        }
    }
    
    // 当物品被点击时调用
    public void OnItemClicked()
    {
        // 再次检查是否可以购买
        if (purchaseLimit == 0)
        {
            Debug.Log("该物品已达到购买限制，无法继续购买");
            return;
        }
        
        shopPanel.ShowPurchasePanel(shopItem, itemPrice, OnPurchaseConfirmed);
    }
    
    // 当点击购买按钮时调用
    public void OnPurchaseButtonClick()
    {
        // 检查是否可以购买
        if (purchaseLimit == 0)
        {
            Debug.Log("该物品已达到购买限制，无法继续购买");
            return;
        }
        
        // 这里应该实现购买逻辑
        // 例如：检查玩家是否有足够的货币，减少货币，增加物品到背包等
        Debug.Log($"尝试购买物品 ID: {itemId}");
        
        // 如果不是无限购买，减少购买限制
        if (purchaseLimit > 0)
        {
            purchaseLimit--;
            UpdatePurchaseLimitDisplay();
            UpdateButtonState();
            
            // 检查是否达到购买限制
            if (purchaseLimit == 0)
            {
                Debug.Log("该物品已达到购买限制");
            }
        }
    }
    
    // 当购买确认时调用
    private void OnPurchaseConfirmed(int quantity)
    {
        if (quantity <= 0) return;
        // 重新校验剩余次数
        if (purchaseLimit == 0)
        {
            UIManager.Instance?.ShowToast("已达上限");
            return;
        }
        if (purchaseLimit > 0 && quantity > purchaseLimit)
        {
            quantity = purchaseLimit; // 裁剪到剩余
        }
        int totalCost = itemPrice * quantity;
        // 检查钻石
        if (!PlayerCurrencyManager.Instance.RemoveDiamonds(totalCost))
        {
            UIManager.Instance?.ShowToast("钻石不足");
            return;
        }
        // 添加物品
        bool addOk = InventoryManager.Instance.AddItem(itemId, quantity);
        if (!addOk)
        {
            // 回退钻石
            PlayerCurrencyManager.Instance.AddDiamonds(totalCost);
            UIManager.Instance?.ShowToast("背包已满");
            return;
        }
        // 持久化记录
        if (shopItem.originalPurchaseLimit == 0)
        {
            // 若未初始化原始限制且当前有剩余限制，则设定
            shopItem.originalPurchaseLimit = (purchaseLimit > 0) ? purchaseLimit : shopItem.purchaseLimit;
        }
        if (purchaseLimit > 0)
        {
            // 记录购买次数
            ShopPurchaseHelper.AddWorldShopPurchasedCount(itemId, quantity);
            purchaseLimit -= quantity;
            shopItem.purchaseLimit = purchaseLimit; // 同步剩余
            UpdatePurchaseLimitDisplay();
            UpdateButtonState();
        }
        UIManager.Instance?.ShowToast($"购买成功 x{quantity}");
    }
}