using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;

public class EquipmentSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDropHandler, IPointerClickHandler,IDropTarget, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image icon;

    [SerializeField] private Image bgmImage;
    [SerializeField] private Color defaultBgmColor = Color.grey; // 新增: 默认背景颜色（当无装备时）
    // [核心修改] 使用 InventoryItem 作为唯一的数据源
    public InventoryItem Item { get; private set; }
    
    // [修改] 回调函数的参数类型改为 InventoryItem
    private Action<InventoryItem> _onHoverEnter;
    private Action _onHoverExit;
    
    [Header("槽位配置")]
    [SerializeField] private EquipmentType slotType; // [新增] 在Inspector中设置此槽的类型

    // [修改] 新的 Init 方法
    public void Init(InventoryItem item, Action<InventoryItem> onHoverEnter, Action onHoverExit)
    {
        this.Item = item;
        this._onHoverEnter = onHoverEnter;
        this._onHoverExit = onHoverExit;
        // 这里不要直接用 Item.quantity（可能为 null），统一走更新函数
        UpdateSlotDisplay();
    }
    
    /// <summary>
    /// [修改] 鼠标点击事件，主要用于卸下装备
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // 必须有物品且是右键点击
        if (Item == null || eventData.button != PointerEventData.InputButton.Right)
        {
            return;
        }

        // [核心逻辑] 卸下装备
        // 1. 请求 InventoryManager 查找一个空格子
        int emptySlotIndex = InventoryManager.Instance.FindFirstEmptyInventorySlot();

        // 2. 如果找到了空格子
        if (emptySlotIndex != -1)
        {
            // 3. 通知 InventoryManager 将此物品卸下到该空格子
            InventoryManager.Instance.UnequipItem(Item.instanceId, emptySlotIndex);
        }
        else
        {
            // 如果背包满了，可以给玩家一个提示
            Debug.Log("背包已满，无法卸下装备！");
            // UIManager.Instance.ShowNotification("背包已满！");
        }
    }
    
    /// <summary>
    /// [修改] 物品拖放到此格子的逻辑
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log("一个物品被拖放到了装备槽上。");
    }

    #region UI更新与事件回调

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Item != null)
        {
            _onHoverEnter?.Invoke(Item);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _onHoverExit?.Invoke();
    }
    
    /// <summary>
    /// [修改] 更新UI显示
    /// </summary>
    public void UpdateSlotDisplay()
    {
        if (Item == null)
        {
            icon.enabled = false;
            if (bgmImage != null)
                bgmImage.color = defaultBgmColor; // 新增: 无装备时恢复默认颜色
            return;
        }

        var equipmentData = GameManager.Instance.ItemDataSo.GetEquipmentDataById(Item.itemId);
        if (equipmentData != null)
        {
            icon.enabled = true;
            icon.sprite = equipmentData.itemSprite;
            if (bgmImage != null)
                bgmImage.color = ItemQualityUtility.GetQualityColor(equipmentData.quantity);
        }
        else
        {
            ClearSlot(); // 如果找不到数据，也清空
        }
    }
    
    /// <summary>
    /// [新增] 清空格子，供 InventoryPanel 在刷新前调用
    /// </summary>
    public void ClearSlot()
    {
        this.Item = null;
        UpdateSlotDisplay();
    }
    
    #endregion

    // 新增：在拖拽时临时隐藏或显示视觉元素
    public void SetVisible(bool visible)
    {
        if (visible)
        {
            UpdateSlotDisplay();
        }
        else
        {
            if (icon != null) icon.enabled = false;
            if (bgmImage != null) bgmImage.color = defaultBgmColor; // 拖起时也可显示默认颜色
        }
    }

    // 拖拽支持：从装备槽开始拖拽到UI外或者其他槽
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Item != null)
        {
            DragAndDropPanel.Instance.StartDrag(this);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 可选：视觉效果由 DragVisualPrefab 管理
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (Item != null)
        {
            DragAndDropPanel.Instance.EndDrag(eventData);
        }
    }

    // [新增] 实现接口的 Location 属性
    public ItemLocation Location => ItemLocation.Equipped; // 这个格子属于装备栏

    // [新增] 实现接口的 SlotIndex 属性
    public int SlotIndex => (int)slotType; // 它的索引就是其装备类型的枚举值
}