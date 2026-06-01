using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 添加对新输入系统的引用

public class QuickInventoryBar : MonoBehaviour
{
    [Header("UI 组件")]
    [SerializeField] private List<QuickSlot> quickSlots; // 在Inspector中拖入10个QuickSlot（可选）
    [SerializeField] private SlotDetailsPanel slotDetailsPanel;
    
    // 新增：定义数字键的键位数组，用于新输入系统
    private Key[] _numberKeys = new Key[]
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
        Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0
    };

    // 启动时订阅事件
    private void Start()
    {
        InitializeSlots();
        InventoryManager.OnInventoryUpdated += RefreshQuickBarUI;
        RefreshQuickBarUI(); // 游戏开始时立即刷新一次
    }

    // 对象销毁时取消订阅
    private void OnDestroy()
    {
        InventoryManager.OnInventoryUpdated -= RefreshQuickBarUI;
    }

    /// <summary>
    /// [新增] 监听键盘输入以使用快捷物品
    /// </summary>
    private void Update()
    {
        // 防御性检查
        if (quickSlots == null || quickSlots.Count == 0) return;
        if (Keyboard.current == null) return;

        // 检查数字键 1-9 和 0
        for (int i = 0; i < quickSlots.Count && i < _numberKeys.Length; i++)
        {
            // 使用新输入系统检查按键
            if (Keyboard.current[_numberKeys[i]].wasPressedThisFrame)
            {
                // 找到了对应的快捷键
                QuickSlot slot = quickSlots[i];
                if (slot != null && slot.Item != null)
                {
                    var item = slot.Item;
                    var itemData = GameDataConfig.Instance.ItemDataSo.GetItemDataById(item.itemId);
                    if (itemData == null) continue;

                    switch (itemData.itemType)
                    {
                        case ItemType.消耗品:
                            InventoryManager.Instance.UseItem(item.instanceId);
                            break;
                        case ItemType.装备:
                            var equipmentData = itemData as EquipmentData;
                            if (equipmentData != null)
                            {
                                InventoryManager.Instance.EquipItem(item.instanceId, (int)equipmentData.equipmentType);
                            }
                            break;
                        default:
                            // 兜底：其他类型按使用处理
                            InventoryManager.Instance.UseItem(item.instanceId);
                            break;
                    }
                }

                break; // 避免一帧内响应多个按键
            }
        }
    }

    /// <summary>
    /// [新增] 初始化或自动创建所有快捷栏格子，设置它们的快捷键显示和回调
    /// </summary>
    private void InitializeSlots()
    {
        // 确保 list 初始化
        if (quickSlots == null)
            quickSlots = new List<QuickSlot>();

        // 最终确保 quickSlots 数量至少为 slotCount（如果可能）
        // 并初始化显示（null 表示空格子）
        int initCount = quickSlots.Count;
        for (int i = 0; i < initCount; i++)
        {
            string keyDisplay = (i == 9) ? "0" : (i + 1).ToString();
            if (quickSlots[i] != null)
            {
                quickSlots[i].SetQuickSlotIndex(i);
                quickSlots[i].Init(null, keyDisplay, OnSlotPointerEnter, OnSlotPointerExit);
            }
        }
    }

    /// <summary>
    /// [核心] 刷新整个快捷栏的UI显示
    /// </summary>
    private void RefreshQuickBarUI()
    {
        if (InventoryManager.Instance == null) return;

        // 1. 先清空所有格子的现有显示
        if (quickSlots != null)
        {
            foreach (var slot in quickSlots)
            {
                if (slot != null) slot.ClearSlot();
            }
        }

        // 2. 从InventoryManager获取最新的快捷栏物品数据
        var quickSlotItems = InventoryManager.Instance.GetQuickSlotItems();
        if (quickSlotItems == null) return;

        // 3. 遍历数据，更新对应的UI格子
        foreach (var item in quickSlotItems)
        {
            if (item == null) continue;
            if (item.slotIndex >= 0 && quickSlots != null && item.slotIndex < quickSlots.Count)
            {
                // 使用已经设置好的快捷键显示来重新初始化格子
                string keyDisplay = (item.slotIndex == 9) ? "0" : (item.slotIndex + 1).ToString();
                var slot = quickSlots[item.slotIndex];
                if (slot != null)
                    slot.Init(item, keyDisplay, OnSlotPointerEnter, OnSlotPointerExit);
            }
        }
    }

    // [修改] 详情面板的回调方法，参数类型更新为 InventoryItem
    private void OnSlotPointerEnter(InventoryItem item)
    {
        if (slotDetailsPanel != null && item != null)
        {
            var data = GameDataConfig.Instance.ItemDataSo.GetItemDataById(item.itemId);
            slotDetailsPanel.ShowDetails(data);
        }
    }
    
    private void OnSlotPointerExit()
    {
        if (slotDetailsPanel != null)
        {
            slotDetailsPanel.HideDetails();
        }
    }

}

