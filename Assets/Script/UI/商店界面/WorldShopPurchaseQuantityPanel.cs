using System;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class WorldShopPurchaseQuantityPanel : UIPopPanelBase
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text numberText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Slider slider;
    private int _nowNumber;
    private int _maxNumber;
    private int _itemPrice;
    private Action<int> _onBuyButtonClick;
    private WorldShopItem _shopItem;
    
    public void Init(WorldShopItem shopItem, int itemPrice, Action<int> onBuyButtonClick)
    {
        _shopItem = shopItem;
        _itemPrice = shopItem.price; // 使用商店物品配置的价格
        _onBuyButtonClick = onBuyButtonClick;
        
        var itemData = GameManager.Instance.ItemDataSo.GetEquipmentDataById(shopItem.itemId);
        if (itemData != null && iconImage != null)
        {
            iconImage.sprite = itemData.itemSprite;
        }
        // 购买上限逻辑: -1 = 无限; 0 = 不可购买; >0 剩余次数
        if (shopItem.purchaseLimit == -1)
        {
            _maxNumber = 999; // 人为上限
        }
        else if (shopItem.purchaseLimit == 0)
        {
            _maxNumber = 0; // 不可购买
        }
        else
        {
            _maxNumber = shopItem.purchaseLimit;
        }
        if (slider != null)
        {
            slider.wholeNumbers = true;
            slider.minValue = _maxNumber > 0 ? 1 : 0;
            int gemCount = PlayerCurrencyManager.Instance.Diamonds;
            int maxCount=math.min(gemCount / _itemPrice, _maxNumber);
            slider.maxValue = maxCount;
            slider.value = maxCount;
            _nowNumber = maxCount;
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(OnSliderValueChange);
            slider.interactable = _maxNumber > 1;
        }
        if (buyButton != null)
        {
            buyButton.interactable = _maxNumber > 0;
        }
        UpdateDisplay();
        Show();
    }
    
    private void UpdateDisplay()
    {
        if (numberText != null)
        {
            numberText.text = "X" + _nowNumber;
        }
        
        if (priceText != null)
        {
            priceText.text = "总价: " + (_nowNumber * _itemPrice);
        }
    }

    public void OnSliderValueChange(float value)
    {
        _nowNumber = Mathf.Clamp((int)value, 1, _maxNumber);
        UpdateDisplay();
    }
    
    public void OnBuyButtonClick()
    {
        if (_maxNumber == 0)
        {
            UIManager.Instance?.ShowToast("无法购买");
            Hide(false);
            return;
        }
        _onBuyButtonClick?.Invoke(_nowNumber);
        Hide(false);
    }
    
    public void OnCloseButtonClick()
    {
        Hide(false);
    }
}