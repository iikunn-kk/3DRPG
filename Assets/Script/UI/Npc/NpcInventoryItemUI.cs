using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// NPC商店中玩家背包物品UI类，用于显示玩家背包中的单个物品
/// </summary>
public class NpcInventoryItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI组件")]
    [SerializeField] private Image itemIcon;          // 物品图标
    [SerializeField] private Image highlightImage;
    [SerializeField] private Image bkImage;
    [SerializeField] private TMP_Text itemCountText;  // 物品数量文本

    private InventoryItem inventoryItem;              // 背包物品数据
    private ItemData itemData;                        // 物品数据
    
    // 详情显示回调（两种形态，优先使用 InventoryItem 版本）
    private Action<InventoryItem> showDetailsInventoryCallback;
    private Action<ItemData> showDetailsCallback;
    private Action hideDetailsCallback;
    
    // 右键点击回调函数
    private Action<InventoryItem> onRightClickCallback;



    /// <summary>
    /// 初始化（兼容旧签名）：使用 ItemData 级别的详情回调
    /// </summary>
    /// <param name="inventoryItem">背包物品数据</param>
    /// <param name="showDetailsCallback">显示详细信息的回调函数</param>
    /// <param name="hideDetailsCallback">隐藏详细信息的回调函数</param>
    /// <param name="onRightClickCallback">右键点击的回调函数</param>
    public void Init(InventoryItem inventoryItem, Action<ItemData> showDetailsCallback, Action hideDetailsCallback, Action<InventoryItem> onRightClickCallback)
    {
        this.inventoryItem = inventoryItem;
        this.showDetailsCallback = showDetailsCallback;
        this.hideDetailsCallback = hideDetailsCallback;
        this.onRightClickCallback = onRightClickCallback;
        this.showDetailsInventoryCallback = null;

        CommonInit();
    }

    /// <summary>
    /// 初始化（推荐）：使用 InventoryItem 级别的详情回调，能显示实例属性
    /// </summary>
    /// <param name="inventoryItem">背包物品数据</param>
    /// <param name="showDetailsInventoryCallback">显示详细信息的回调函数</param>
    /// <param name="hideDetailsCallback">隐藏详细信息的回调函数</param>
    /// <param name="onRightClickCallback">右键点击的回调函数</param>
    public void Init(InventoryItem inventoryItem, Action<InventoryItem> showDetailsInventoryCallback, Action hideDetailsCallback, Action<InventoryItem> onRightClickCallback)
    {
        this.inventoryItem = inventoryItem;
        this.showDetailsInventoryCallback = showDetailsInventoryCallback;
        this.hideDetailsCallback = hideDetailsCallback;
        this.onRightClickCallback = onRightClickCallback;
        this.showDetailsCallback = null;

        CommonInit();
    }

    private void CommonInit()
    {
        // 获取物品数据
        itemData = GameDataConfig.Instance.ItemDataSo.GetItemDataById(inventoryItem.itemId);
        
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

        // 设置物品数量
        if (itemCountText != null)
        {
            // 对于可堆叠物品显示数量，装备类物品显示1
            if (itemData != null && (itemData.itemType == ItemType.消耗品 || itemData.itemType == ItemType.材料))
            {
                itemCountText.text = inventoryItem.count.ToString();
            }
            else
            {
                itemCountText.text = "1";
            }
        }

        if (itemData != null) bkImage.color = ItemQualityUtility.GetQualityColor(itemData.quantity);
        highlightImage.gameObject.SetActive(false);
    }


    
    /// <summary>
    /// 鼠标指针进入物体范围
    /// </summary>
    /// <param name="eventData">事件数据</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (showDetailsInventoryCallback != null)
        {
            showDetailsInventoryCallback(inventoryItem);
        }
        else if (itemData != null && showDetailsCallback != null)
        {
            showDetailsCallback(itemData);
        }
        highlightImage.gameObject.SetActive(true);
    }
    
    /// <summary>
    /// 鼠标指针退出物体范围
    /// </summary>
    /// <param name="eventData">事件数据</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (hideDetailsCallback != null)
        {
            hideDetailsCallback();
        }
        highlightImage.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// 鼠标指针点击物体
    /// </summary>
    /// <param name="eventData">事件数据</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        // 只处理右键点击
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            // 触发右键点击回调
            if (onRightClickCallback != null && inventoryItem != null)
            {
                onRightClickCallback(inventoryItem);
            }
        }
    }
}