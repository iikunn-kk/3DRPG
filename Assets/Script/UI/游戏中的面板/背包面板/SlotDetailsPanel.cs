using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SlotDetailsPanel : MonoBehaviour
{
    // 改为 protected 以便子类访问
    [SerializeField] protected TMP_Text nameText;
    [SerializeField] protected TMP_Text descriptionText;
    [SerializeField] protected TMP_Text sellPriceText;
    [SerializeField] protected Image icon;
    [SerializeField] protected Image background;
    [SerializeField] protected bool isFollowCursor = true;
  [Header("跟随光标的偏移和边界")]
    [SerializeField] private float cursorMargin = 16f; // 鼠标与面板之间的最小间距
    [SerializeField] private bool smartQuadrantPosition = true; // 根据屏幕象限自动摆放
    protected RectTransform _rectTransform;
    protected Canvas _parentCanvas;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        // 获取父级Canvas组件
        _parentCanvas = GetComponentInParent<Canvas>();
    }

    private void LateUpdate()
    {
        if (gameObject.activeSelf && isFollowCursor)
        {
            FollowCursor();
        }
    }
    
    /// <summary>
    /// 显示物品详细信息（模板数据）
    /// </summary>
    /// <param name="itemData">物品数据</param>
    public void ShowDetails(ItemData itemData)
    {
        if (itemData == null) return;

        // 如果传入的是 EquipmentData，尝试使用 EquipmentData 的重载显示（保留兼容性）
        if (itemData is EquipmentData equipmentTemplate)
        {
            // 当只有模板数据时，使用模板上的属性（但优先显示实例属性如果可用）
            ShowDetails(equipmentTemplate);
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

        // 根据物品品质设置背景颜色
        if (background != null)
        {
            background.color = ItemQualityUtility.GetQualityColor(itemData.quantity);
        }

        // 显示面板
        gameObject.SetActive(true);
        
        // 立即更新位置
        FollowCursor();
    }

    /// <summary>
    /// 保持对 EquipmentData 模板的兼容显示（仅模板属性）
    /// </summary>
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
            descriptionText.text = description + "\n" + effect;
        
        // 显示物品图标
        if (icon != null)
            icon.sprite = data.itemSprite;

   
        if (sellPriceText != null)
            sellPriceText.text = data.GetMySellPrice().ToString();
        
        // 根据物品品质设置背景颜色
        if (background != null)
        {
            background.color = ItemQualityUtility.GetQualityColor(data.quantity);
        }
        
        // 显示面板
        gameObject.SetActive(true);
        
        // 立即更新位置
        FollowCursor();
    }

    /// <summary>
    /// 新增：使用 InventoryItem 显示（优先显示该实例生成的属性）
    /// </summary>
    public virtual void ShowDetails(InventoryItem item)
    {
        if (item == null) return;

        var template = GameManager.Instance.ItemDataSo.GetItemDataById(item.itemId);
        if (template == null)
        {
            // 如果找不到模板，仍然尝试显示基础数据
            if (nameText != null) nameText.text = "Unknown Item";
            if (descriptionText != null) descriptionText.text = string.Empty;
            if (sellPriceText != null) sellPriceText.text = string.Empty;
            if (icon != null) icon.enabled = false;
            // 设置默认背景颜色
            if (background != null)
                background.color = Color.white;
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

        // 根据物品品质设置背景颜色
        if (background != null)
        {
            background.color = ItemQualityUtility.GetQualityColor(item.quantity);
        }
        // 显示面板
        gameObject.SetActive(true);
        FollowCursor();
    }
    
    /// <summary>
    /// 跟随光标位置
    /// </summary>
    private void FollowCursor()
    {
        if (_rectTransform == null || _parentCanvas == null) return;

        // 屏幕鼠标位置
        Vector2 mousePosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        // 将屏幕坐标转为 Canvas 内局部坐标
        Vector2 cursorLocal;
        RectTransform canvasRect = _parentCanvas.transform as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            mousePosition,
            _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _parentCanvas.worldCamera,
            out cursorLocal
        );

        // 默认直接跟随（中心对中心）
        Vector2 target = cursorLocal;

        if (smartQuadrantPosition)
        {
            _rectTransform.ForceUpdateRectTransforms();
            Vector2 size = _rectTransform.rect.size;
            bool topHalf = mousePosition.y > (Screen.height * 0.5f);
            bool leftHalf = mousePosition.x < (Screen.width * 0.5f);

            // 考虑 Canvas 缩放因子，将 cursorMargin 转换为 Canvas 本地单位
            float margin = cursorMargin;
            if (_parentCanvas != null)
            {
                margin = cursorMargin / Mathf.Max(0.0001f, _parentCanvas.scaleFactor);
            }

            // 计算使面板的最近边缘贴近鼠标的 anchoredPosition
            // 左侧象限：把面板放在鼠标右侧，使左边缘 = cursor + margin
            // 右侧象限：把面板放在鼠标左侧，使右边缘 = cursor - margin
            float x;
            if (leftHalf)
            {
                // 左侧：左边缘 = cursorLocal.x + margin
                x = cursorLocal.x + margin + size.x * _rectTransform.pivot.x;
            }
            else
            {
                // 右侧：右边缘 = cursorLocal.x - margin
                x = cursorLocal.x - margin - size.x * (1f - _rectTransform.pivot.x);
            }

            float y;
            if (topHalf)
            {
                // 鼠标在上半区，面板放在下方：上边缘 = cursorLocal.y - margin
                y = cursorLocal.y - margin - size.y * (1f - _rectTransform.pivot.y);
            }
            else
            {
                // 鼠标在下半区，面板放在上方：下边缘 = cursorLocal.y + margin
                y = cursorLocal.y + margin + size.y * _rectTransform.pivot.y;
            }

            target = new Vector2(x, y);

            // 使用 pivot 计算允许的 anchoredPosition 范围，保证面板不超出 Canvas
            float minX = canvasRect.rect.xMin + size.x * _rectTransform.pivot.x;
            float maxX = canvasRect.rect.xMax - size.x * (1f - _rectTransform.pivot.x);
            float minY = canvasRect.rect.yMin + size.y * _rectTransform.pivot.y;
            float maxY = canvasRect.rect.yMax - size.y * (1f - _rectTransform.pivot.y);

            target.x = Mathf.Clamp(target.x, minX, maxX);
            target.y = Mathf.Clamp(target.y, minY, maxY);
        }

        _rectTransform.anchoredPosition = target;
    }

    /// <summary>
    /// 隐藏详细信息面板
    /// </summary>
    public void HideDetails()
    {
        gameObject.SetActive(false);
    }
}

