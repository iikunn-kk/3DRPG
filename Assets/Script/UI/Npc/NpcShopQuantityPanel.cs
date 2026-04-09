using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// NPC商店购买/出售数量选择面板
/// </summary>
public class NpcShopQuantityPanel : UIPopPanelBase
{
    public enum PanelMode
    {
        Purchase,  // 购买模式
        Sell       // 出售模式
    }

    [Header("UI组件")]
    [SerializeField] private Image itemIcon;              // 物品图标
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text itemInfoText;       // 物品信息文本
    [SerializeField] private TMP_Text quantityText;       // 数量文本
    [SerializeField] private TMP_Text totalPriceText;     // 总价文本
    [SerializeField] private Slider quantitySlider;       // 数量滑动条
    [SerializeField] private Button confirmButton;        // 确认按钮
    [SerializeField] private Button closeButton;          // 关闭按钮
    [SerializeField] private Button maxButton;            // 最大数量按钮
    [SerializeField] private TMP_Text sellOrBuyText;     // 出售或购买文本
    [SerializeField] private TMP_Text sellAllOrBuyText;  // 出售全部或购买全部文本
    private int currentQuantity = 1;                      // 当前选择的数量
    private int maxQuantity = 1;                          // 最大可操作数量
    private int itemPrice = 0;                            // 物品单价
    private PanelMode currentMode;                        // 当前面板模式
    
    // 数据引用
    private NpcShopItem shopItem;                         // 商店物品（购买时使用）
    private InventoryItem inventoryItem;                  // 背包物品（出售时使用）
    
    // 回调
    private Action<NpcShopItem, int> purchaseCallback;    // 购买回调
    private Action<InventoryItem, int> sellCallback;      // 出售回调


    /// <summary>
    /// 初始化面板（购买模式）
    /// </summary>
    /// <param name="shopItemParam">商店物品</param>
    /// <param name="mode">面板模式</param>
    /// <param name="onPurchase">购买确认回调</param>
    public void Init(NpcShopItem shopItemParam, PanelMode mode, Action<NpcShopItem, int> onPurchase)
    {
        this.shopItem = shopItemParam;
        this.currentMode = mode;
        this.purchaseCallback = onPurchase;
        this.inventoryItem = null;
        this.sellCallback = null;
        titleText.text = currentMode == PanelMode.Purchase ? "购买" : "出售";
        SetupPanel();
        Show();
    }

    /// <summary>
    /// 初始化面板（出售模式）
    /// </summary>
    /// <param name="inventoryItemParam">背包物品</param>
    /// <param name="mode">面板模式</param>
    /// <param name="onSell">出售确认回调</param>
    public void Init(InventoryItem inventoryItemParam, PanelMode mode, Action<InventoryItem, int> onSell)
    {
        this.inventoryItem = inventoryItemParam;
        this.currentMode = mode;
        this.sellCallback = onSell;
        this.shopItem = null;
        this.purchaseCallback = null;
        titleText.text = currentMode == PanelMode.Purchase ? "购买" : "出售";
        SetupPanel();
        Show();
    }

    /// <summary>
    /// 设置面板显示内容
    /// </summary>
    private void SetupPanel()
    {
        // 获取物品数据
        ItemData itemData = null;
        string itemName = "未知物品";
        
        switch (currentMode)
        {
            case PanelMode.Purchase when shopItem != null:
                itemData = GameManager.Instance.ItemDataSo.GetItemDataById(shopItem.itemId);
                itemName = itemData != null ? itemData.itemName : "未知物品";
                itemPrice = shopItem.price;
                
                // 设置最大购买数量
                if (shopItem.purchaseLimit < 0)
                {
                    // 无库存限制，设置一个合理上限（如999）
                    maxQuantity = 999;
                }
                else
                {
                    maxQuantity = shopItem.purchaseLimit;
                }
                
                // 确保最大数量至少为1
                maxQuantity = Mathf.Max(1, maxQuantity);
                break;
                
            case PanelMode.Sell when inventoryItem != null:
                itemData = GameManager.Instance.ItemDataSo.GetItemDataById(inventoryItem.itemId);
                itemName = itemData != null ? itemData.itemName : "未知物品";
                
                // 设定出售价格
                itemPrice = itemData != null ? itemData.GetMySellPrice() : 0;
                // 若未设置售价，使用与管理器一致的默认价格
                if (itemPrice == 0 && itemData != null)
                {
                    itemPrice = NpcShopManager.GetDefaultSellPrice(itemData.itemType);
                }
                
                // 设置最大出售数量
                if (itemData != null && (itemData.itemType == ItemType.消耗品 || itemData.itemType == ItemType.材料))
                {
                    maxQuantity = inventoryItem.count;
                }
                else
                {
                    maxQuantity = 1;
                }
                break;
                
            default:
                // 处理无效情况
                maxQuantity = 1;
                itemPrice = 0;
                break;
        }

        // 设置物品图标
        if (itemIcon != null && itemData != null && itemData.itemSprite != null)
        {
            itemIcon.sprite = itemData.itemSprite;
            itemIcon.enabled = true;
        }
        else if (itemIcon != null)
        {
            itemIcon.enabled = false;
        }

        // 设置物品信息文本
        if (itemInfoText != null)
        {
            string modeText = currentMode == PanelMode.Purchase ? "购买" : "出售";
            itemInfoText.text = $"{modeText} {itemName}";
        }

        // 同步更新购买/出售相关文案（按钮/提示）
        string actionText = currentMode == PanelMode.Purchase ? "购买" : "出售";
        if (sellOrBuyText != null)
        {
            sellOrBuyText.text = actionText;
        }
        if (sellAllOrBuyText != null)
        {
            sellAllOrBuyText.text = actionText + "全部";
            if (sellAllOrBuyText.gameObject != null)
            {
                maxButton.gameObject.SetActive(maxQuantity > 1);
                sellAllOrBuyText.gameObject.SetActive(maxQuantity > 1);
            }
        }

        // 重置数量
        if (currentMode == PanelMode.Purchase)
        {
            currentQuantity = 1;
        }
        else
        {
            currentQuantity = maxQuantity;
        }
        currentQuantity = Mathf.Clamp(currentQuantity, 1, maxQuantity);
        
        // 设置滑动条范围
        if (quantitySlider != null)
        {
            quantitySlider.minValue = 1;
            quantitySlider.maxValue = maxQuantity; // 滑动条最大值应为maxQuantity
            quantitySlider.value = currentQuantity;
            quantitySlider.wholeNumbers = true;
            // 如果是在出售装备模式，禁用滑动条交互（装备只能一次出售一个实例）
            if (currentMode == PanelMode.Sell && itemData != null && itemData.itemType == ItemType.装备)
            {
                quantitySlider.interactable = false;
            }
            else
            {
                quantitySlider.interactable = true;
            }
        }

        // 更新显示
        UpdateDisplay();
    }

    /// <summary>
    /// 更新面板显示
    /// </summary>
    private void UpdateDisplay()
    {
        if (quantityText != null)
        {
            quantityText.text = currentQuantity.ToString();
        }

        if (totalPriceText != null)
        {
            int totalPrice = currentQuantity * itemPrice;
            string modeText = currentMode == PanelMode.Purchase ? "花费" : "获得";
            totalPriceText.text = $"{modeText}金币: {totalPrice}";
        }

        // 更新确认按钮的可交互性
        UpdateConfirmButtonInteractable();
    }

    /// <summary>
    /// 滑动条值变化回调
    /// </summary>
    /// <param name="value">滑动条值</param>
    public void OnSliderValueChanged(float value)
    {
        currentQuantity = Mathf.RoundToInt(value);
        currentQuantity = Mathf.Clamp(currentQuantity, 1, maxQuantity);
        UpdateDisplay();
    }

    /// <summary>
    /// 最大数量按钮点击回调
    /// </summary>
    public void OnMaxButtonClicked()
    {
        if (currentMode == PanelMode.Purchase)
        {
            if (itemPrice <= 0)
            {
                currentQuantity = maxQuantity;
            }
            else
            {
                int playerMoney = PlayerCurrencyManager.Instance?.Money ?? 0;
                int affordable = playerMoney / itemPrice;
                currentQuantity = Mathf.Clamp(affordable, 1, maxQuantity);
            }
        }
        else
        {
            currentQuantity = maxQuantity;
        }
        
        if (quantitySlider != null)
        {
            quantitySlider.value = currentQuantity;
        }
        
        UpdateDisplay();
    }

    /// <summary>
    /// 确认按钮点击回调
    /// </summary>
    public void OnConfirmButtonClicked()
    {
        if (currentMode == PanelMode.Purchase && shopItem != null && purchaseCallback != null)
        {
            purchaseCallback(shopItem, currentQuantity);
        }
        else if (currentMode == PanelMode.Sell && inventoryItem != null && sellCallback != null)
        {
            sellCallback(inventoryItem, currentQuantity);
        }
        
        Hide(false);
    }

    /// <summary>
    /// 关闭按钮点击回调
    /// </summary>
    public void OnCloseButtonClicked()
    {
        Hide(false);
    }

    /// <summary>
    /// 更新确认按钮可交互状态
    /// </summary>
    private void UpdateConfirmButtonInteractable()
    {
        if (confirmButton == null)
            return;

        if (currentMode == PanelMode.Purchase)
        {
            if (itemPrice <= 0)
            {
                confirmButton.interactable = true;
            }
            else
            {
                int playerMoney = PlayerCurrencyManager.Instance?.Money ?? 0;
                confirmButton.interactable = (long)playerMoney >= (long)currentQuantity * itemPrice;
            }
        }
        else
        {
            confirmButton.interactable = true;
        }
    }
}
