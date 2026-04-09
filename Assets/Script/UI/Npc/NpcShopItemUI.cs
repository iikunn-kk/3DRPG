using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// NPC商店物品UI类，用于显示商店中的单个物品
/// </summary>
public class NpcShopItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI组件")]
    [SerializeField] private Image itemIcon;          // 物品图标
    [SerializeField] private TMP_Text itemNameText;   // 物品名称文本
    [SerializeField] private TMP_Text itemPriceText;  // 物品价格文本
    [SerializeField] private TMP_Text itemQuantityText; // 物品数量文本
    [SerializeField] private Button itemButton;       // 物品按钮
    [SerializeField] private Image highlightImage;    // 悬停高亮（可选）

    private NpcShopItem _shopItem;                     // 商店物品数据
    private NpcShopPanel _shopPanel;                   // 商店面板引用
    private ItemData _itemData;                        // 物品模板数据（用于详情）


    /// <summary>
    /// 初始化商店物品UI
    /// </summary>
    /// <param name="shopItem">商店物品数据</param>
    /// <param name="panel">商店面板引用</param>
    public void Init(NpcShopItem shopItem, NpcShopPanel panel)
    {
        _shopItem = shopItem;
        _shopPanel = panel;
        RefreshUI();
        if (highlightImage != null) highlightImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// 刷新UI显示
    /// </summary>
    private void RefreshUI()
    {
        if (_shopItem == null) return;

        // 获取物品数据
        _itemData = GameManager.Instance.ItemDataSo.GetItemDataById(_shopItem.itemId);

        // 设置物品图标
        if (itemIcon != null && _itemData != null && _itemData.itemSprite != null)
        {
            itemIcon.sprite = _itemData.itemSprite;
            itemIcon.enabled = true;
        }
        else if (itemIcon != null)
        {
            itemIcon.enabled = false;
        }

        // 设置物品名称
        if (itemNameText != null)
        {
            itemNameText.text = _itemData != null ? _itemData.itemName : "未知物品";
        }

        // 设置物品价格
        if (itemPriceText != null)
        {
            itemPriceText.text = _shopItem.price+"金币";
        }

        // 设置物品数量
        if (itemQuantityText != null)
        {
            if (_shopItem.purchaseLimit < 0)
            {
                itemQuantityText.text = "∞"; // 无限库存显示为∞
            }
            else
            {
                itemQuantityText.text = _shopItem.purchaseLimit.ToString();
            }
        }

        // 检查物品是否可购买
        bool isPurchasable = (_shopItem.purchaseLimit < 0 || _shopItem.purchaseLimit > 0) && PlayerCurrencyManager.Instance.Money >= _shopItem.price;
        SetItemInteractable(isPurchasable);
    }

    public void OnMoneyChanged(int newMoney)
    {
        bool isPurchasable = (_shopItem.purchaseLimit < 0 || _shopItem.purchaseLimit > 0) && newMoney >= _shopItem.price;
        SetItemInteractable(isPurchasable);
    }

    /// <summary>
    /// 设置物品是否可交互
    /// </summary>
    /// <param name="interactable">是否可交互</param>
    private void SetItemInteractable(bool interactable)
    {
        if (itemButton != null)
        {
            itemButton.interactable = interactable;
        }
    }

    /// <summary>
    /// 物品按钮点击回调
    /// </summary>
    public void OnItemClicked()
    {
        // 通知商店面板显示购买数量选择面板
        if (_shopPanel != null && _shopItem != null)
        {
            _shopPanel.ShowPurchaseQuantityPanel(_shopItem);
            AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
        }
    }

    // 悬停进入：显示详情 + 高亮
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_shopPanel != null && _itemData != null)
        {
            _shopPanel.ShowDetails(_itemData);
        }
        if (highlightImage != null)
        {
            highlightImage.gameObject.SetActive(true);
        }
    }

    // 悬停离开：隐藏详情 + 取消高亮
    public void OnPointerExit(PointerEventData eventData)
    {
        if (_shopPanel != null)
        {
            _shopPanel.HideDetails();
        }
        if (highlightImage != null)
        {
            highlightImage.gameObject.SetActive(false);
        }
    }
}