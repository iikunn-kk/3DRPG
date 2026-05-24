using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class NpcShopSlotDetailsPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text sellPriceText;
    [SerializeField] private Image icon;
    [SerializeField] private bool isFollowCursor = true;
    private RectTransform _rectTransform;
    private Canvas _parentCanvas;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        // 获取父级Canvas组件
        _parentCanvas = GetComponentInParent<Canvas>();
    }

    private void LateUpdate()
    {
        if (gameObject.activeSelf&& isFollowCursor)
        {
            FollowCursor();
        }
    }
    
    /// <summary>
    /// 显示物品详细信息（模板）
    /// </summary>
    public void ShowDetails(ItemData itemData)
    {
        if (itemData == null) return;

        // 若是装备，走装备专用展示
        if (itemData is EquipmentData eq)
        {
            ShowDetails(eq);
            return;
        }
        
        // 显示物品名称
        if (nameText != null)
            nameText.text = itemData.itemName;
            
        // 显示物品描述
        if (descriptionText != null)
            descriptionText.text = itemData.itemDescription;
            
        // 显示物品图标
        if (icon != null)
            icon.sprite = itemData.itemSprite;
            
        if (sellPriceText != null)
            sellPriceText.text = itemData.GetMySellPrice().ToString();
        // 显示面板
        gameObject.SetActive(true);
        
        // 立即更新位置
        FollowCursor();
    }

    /// <summary>
    /// 显示物品详细信息（玩家实例）
    /// </summary>
    public void ShowDetails(InventoryItem item)
    {
        if (item == null) return;
        var template = GameDataConfig.Instance.ItemDataSo.GetItemDataById(item.itemId);
        if (template == null)
        {
            if (nameText != null) nameText.text = "Unknown Item";
            if (descriptionText != null) descriptionText.text = string.Empty;
            if (sellPriceText != null) sellPriceText.text = string.Empty;
            if (icon != null) icon.enabled = false;
            gameObject.SetActive(true);
            FollowCursor();
            return;
        }

        // 基本信息
        if (nameText != null) nameText.text = template.itemName;
        if (descriptionText != null) descriptionText.text = template.itemDescription;
        if (icon != null)
        {
            icon.sprite = template.itemSprite;
            icon.enabled = template.itemSprite != null;
        }
        if (sellPriceText != null) sellPriceText.text = template.GetMySellPrice().ToString();

        // 显示面板
        gameObject.SetActive(true);
        FollowCursor();
    }

    public void ShowDetails(EquipmentData data)
    {
        if (data == null) return;
        // 显示物品名称
        if (nameText != null)
            nameText.text = data.itemName;
            
        string description = data.itemDescription;
        string effect = "";
        foreach (var property in data.GetAllProperties())
        {
            effect += property.GetDisplayText() + "\n";
        }
        // 显示物品描述
        if (descriptionText != null)
            descriptionText.text =description+"\n"+effect;
            
        // 显示物品图标
        if (icon != null)
            icon.sprite = data.itemSprite;
            
        // 显示面板
        gameObject.SetActive(true);
        
        // 立即更新位置
        FollowCursor();
    }
    
    /// <summary>
    /// 跟随光标位置
    /// </summary>
    private void FollowCursor()
    {
        if (_rectTransform == null || _parentCanvas == null) return;

        Vector2 cursorPos;
        // 使用Unity新输入系统获取鼠标位置
        Vector2 mousePosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parentCanvas.transform as RectTransform,
            mousePosition,
            _parentCanvas.worldCamera,
            out cursorPos
        );

        // 设置面板位置，稍微偏移光标位置以避免遮挡
        _rectTransform.anchoredPosition = cursorPos + new Vector2(10, -10);
    }

    /// <summary>
    /// 隐藏详细信息面板
    /// </summary>
    public void HideDetails()
    {
        gameObject.SetActive(false);
    }
}