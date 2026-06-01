using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// [修改] QuickSlot 现在继承自我们重构后的 InventorySlot
public class QuickSlot : InventorySlot
{
    [Header("快捷键文本")]
    [SerializeField] private TMP_Text keyText;

    [SerializeField] private int _quickSlotIndex;

    // 覆盖基类的 SlotIndex，返回自己的快捷键槽位索引
    public override int SlotIndex => _quickSlotIndex;

    public void SetQuickSlotIndex(int index) => _quickSlotIndex = index;
    
    /// <summary>
    /// [修改] 初始化快捷栏格子
    /// </summary>
    /// <param name="item">要显示的物品实例</param>
    /// <param name="keyDisplay">要显示的快捷键字符，例如 "1"</param>
    /// <param name="onHoverEnter">鼠标悬停进入时的回调</param>
    /// <param name="onHoverExit">鼠标悬停离开时的回调</param>
    public void Init(InventoryItem item, string keyDisplay, Action<InventoryItem> onHoverEnter, Action onHoverExit)
    {
        // [修改] 调用基类的Init方法来处理图标、数量和悬停事件
        base.Init(item, onHoverEnter, onHoverExit);
      
        // 设置自己的快捷键显示
        if (keyText != null)
        {
            keyText.text = keyDisplay;
        }
    }

    // 允许左键直接使用/装备快捷栏中的物品，右键仍然由基类处理
    public override void OnPointerClick(PointerEventData eventData)
    {
        if (Item == null)
        {
            base.OnPointerClick(eventData);
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            var itemData = GameDataConfig.Instance.ItemDataSo.GetItemDataById(Item.itemId);
            if (itemData == null) return;

            switch (itemData.itemType)
            {
                case ItemType.消耗品:
                    InventoryManager.Instance.UseItem(Item.instanceId);
                    break;
                case ItemType.装备:
                    var equipmentData = itemData as EquipmentData;
                    if (equipmentData != null)
                    {
                        InventoryManager.Instance.EquipItem(Item.instanceId, (int)equipmentData.equipmentType);
                    }
                    break;
                default:
                    InventoryManager.Instance.UseItem(Item.instanceId);
                    break;
            }
        }
        else
        {
            // 其他按钮（例如右键）沿用基类行为
            base.OnPointerClick(eventData);
        }
    }
}