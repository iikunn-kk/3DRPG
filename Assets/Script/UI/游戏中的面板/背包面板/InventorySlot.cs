using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;

public class InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,IDropTarget
{
    [SerializeField] protected Image icon;
    [SerializeField] protected TMP_Text countText; // 变量名建议更清晰
    [SerializeField] protected Image bkg;
    [SerializeField] protected ItemLocation location ;
    [SerializeField] protected Image highlightImage; 
    [SerializeField] protected Color defaultBkgColor = Color.grey; // 新增: 默认背景颜色
    // [核心修改] 使用 InventoryItem 作为唯一的数据源，代替 InventoryData
    public InventoryItem Item { get; private set; }

    // [修改] 回调函数的参数类型改为 InventoryItem
    protected Action<InventoryItem> _onHoverEnter;
    protected Action _onHoverExit;
    
    // [修改] 拖拽回调，让它传递 InventoryItem
    protected Action<PointerEventData, InventorySlot> _onBeginDrag;
    protected Action<PointerEventData> _onDrag;
    protected Action<PointerEventData, InventorySlot> _onEndDrag;

    /// <summary>
    /// [修改] 初始化方法，接收 InventoryItem
    /// </summary>
    public virtual void Init(InventoryItem item, Action<InventoryItem> onHoverEnter, Action onHoverExit)
    {
        this.Item = item;
        this._onHoverEnter = onHoverEnter;
        this._onHoverExit = onHoverExit;
        
        UpdateSlotDisplay();
    }
    
    /// <summary>
    /// [新增] 更新UI显示的核心方法
    /// </summary>
    public void UpdateSlotDisplay()
    {
        if (Item == null)
        {
            icon.enabled = false;
            countText.enabled = false;
            if (bkg != null)
                bkg.color = defaultBkgColor; // 使用可配置默认颜色
            return;
        }

        var itemData = GameDataConfig.Instance.ItemDataSo.GetItemDataById(Item.itemId);
        if (itemData == null)
        {
            ClearSlot();
            return;
        }
        
        // 更新图标和数量
        icon.enabled = true;
        icon.sprite = itemData.itemSprite;
        
        if (Item.count > 1)
        {
            countText.enabled = true;
            countText.text = Item.count.ToString();
        }
        else
        {
            countText.enabled = false;
        }
        
        // 根据物品品质设置背景颜色
        if (bkg != null)
        {
            bkg.color = ItemQualityUtility.GetQualityColor(Item.quantity);
        }
        icon.gameObject.SetActive(Item != null);
    }

    /// <summary>
    /// [新增] 清空格子，供 InventoryPanel 在刷新前调用
    /// </summary>
    public void ClearSlot()
    {
        this.Item = null;
        UpdateSlotDisplay();
    }

    /// <summary>
    /// LoopScrollRect 回调 —— 接收数据索引、物品、悬停回调
    /// </summary>
    public void ScrollCellIndexWithCallbacks(int idx, InventoryItem item,
        Action<InventoryItem> onHoverEnter, Action onHoverExit)
    {
        _dataIndex = idx;
        Init(item, onHoverEnter, onHoverExit);
    }

    /// <summary>
    /// 临时隐藏或显示该格子的可视元素（图标和数量），不会改变数据
    /// 在拖拽时可用于让源格子看起来是空的
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (visible)
        {
            // 恢复到当前数据的显示状态
            UpdateSlotDisplay();
        }
        else
        {
            // 隐藏可视元素，但不修改 Item
            if (icon != null) icon.enabled = false;
            if (countText != null) countText.enabled = false;
            if (bkg != null) bkg.color = defaultBkgColor; // 拖拽起时恢复默认背景颜色
        }
    }

    #region 事件处理 (Event Handlers)
    
    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        // 如果格子里有物品，才触发悬停事件
        if (Item != null)
        {
            _onHoverEnter?.Invoke(Item);
            AudioManager.Instance.PlayUISound(UISoundType.光标划过物品栏);
        }
        highlightImage.color=Color.yellow;
        
    }
    
    public virtual void OnPointerExit(PointerEventData eventData)
    {
        _onHoverExit?.Invoke();
        highlightImage.color=Color.white;
    }
   
    public virtual void OnPointerClick(PointerEventData eventData)
    {
        if (Item == null) return;

        // 右键点击：使用或装备
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            var itemData = GameDataConfig.Instance.ItemDataSo.GetItemDataById(Item.itemId);
            if (itemData == null) return;

            switch (itemData.itemType)
            {
                case ItemType.消耗品:
                    // [修改] 通知InventoryManager使用物品，传递的是"实例ID"
                    InventoryManager.Instance.UseItem(Item.instanceId);
                    break;
                case ItemType.装备:
                    // [修改] 通知InventoryManager装备物品
                    var equipmentData = itemData as EquipmentData;
                    if (equipmentData != null)
                    {
                        // 将装备类型转换为整数作为装备槽ID
                        InventoryManager.Instance.EquipItem(Item.instanceId, (int)equipmentData.equipmentType);
                    }
                    break;
            }
        }
    }
    
    #endregion
    
    #region 拖拽 (Drag & Drop)

    public void SetDragCallbacks(Action<PointerEventData, InventorySlot> onBeginDrag, Action<PointerEventData> onDrag, Action<PointerEventData, InventorySlot> onEndDrag)
    {
        _onBeginDrag = onBeginDrag;
        _onDrag = onDrag;
        _onEndDrag = onEndDrag;
    }
// 在 InventorySlot.cs 的 #region 拖拽 (Drag & Drop) 部分

    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        if (Item != null)
        {
            // [修改] 直接通知 DragAndDropPanel 开始拖拽
            DragAndDropPanel.Instance.StartDrag(this);
        }
    }

    public virtual void OnDrag(PointerEventData eventData)
    {
        // 这个方法可以保持为空，因为 DragVisualPrefab 会自己跟随鼠标
    }

    public virtual void OnEndDrag(PointerEventData eventData)
    {
        if (Item != null)
        {
            // [修改] 直接通知 DragAndDropPanel 结束拖拽
            DragAndDropPanel.Instance.EndDrag(eventData);
        }
    }
    #endregion

    private int _dataIndex;
    public ItemLocation Location => location;
    public virtual int SlotIndex => _dataIndex;

    public void SetSlotIndex(int index) => _dataIndex = index;

}