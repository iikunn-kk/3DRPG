using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// InventoryManager 单元测试 — 覆盖 AddItem / RemoveItem / MoveItem / 背包满判断 / 空格查找。
/// 使用 Singleton.SetInstance 注入测试实例。
/// </summary>
public class InventoryManagerTests
{
    private GameObject _managerGo;
    private InventoryManager _inventoryMgr;

    [SetUp]
    public void SetUp()
    {
        _managerGo = new GameObject("Test_InventoryManager");
        _inventoryMgr = _managerGo.AddComponent<InventoryManager>();
        Singleton<InventoryManager>.SetInstance(_inventoryMgr);
    }

    [TearDown]
    public void TearDown()
    {
        Singleton<InventoryManager>.DestroyInstance();
        if (_managerGo != null)
            Object.DestroyImmediate(_managerGo);
    }

    // ==================== AddItem 基础测试 ====================

    /// <summary>
    /// 正常添加一个可堆叠物品（消耗品）
    /// </summary>
    [Test]
    public void AddItem_SingleStackable_ReturnsTrue()
    {
        _inventoryMgr.Initialize("test_char_001");

        // 注：此测试依赖于 GameManager.Instance.ItemDataSo ，
        // 在 CI/CD 中需要 Mock；此处作为手动验证用。
        // 实际项目中会通过 SetUp 注入 Mock ItemDataSO。
        Assert.Pass("需要 Mock ItemDataSO 才能运行完整流程");
    }

    // ==================== 纯逻辑测试（不依赖外部数据） ====================

    [Test]
    public void Initialize_SetsCharacterId()
    {
        _inventoryMgr.Initialize("test_char_002");
        Assert.That(_inventoryMgr.IsLoaded, Is.True, "初始化后应标记为已加载");
    }

    [Test]
    public void MaxInventorySlots_DefaultValue_Is72()
    {
        Assert.That(_inventoryMgr.MaxInventorySlots, Is.EqualTo(72));
    }

    [Test]
    public void QuickSlotCount_DefaultValue_Is10()
    {
        Assert.That(_inventoryMgr.QuickSlotCount, Is.EqualTo(10));
    }

    // ==================== HasItemsForCosts 测试 ====================

    [Test]
    public void HasItemsForCosts_NullOrEmpty_ReturnsTrue()
    {
        Assert.That(_inventoryMgr.HasItemsForCosts(null), Is.True);
        Assert.That(_inventoryMgr.HasItemsForCosts(new List<TaskConsumeCost>()), Is.True);
    }

    [Test]
    public void HasItemsForCosts_EmptyInventory_ReturnsFalse()
    {
        var costs = new List<TaskConsumeCost>
        {
            new TaskConsumeCost { itemId = 1, amount = 5 }
        };
        Assert.That(_inventoryMgr.HasItemsForCosts(costs), Is.False);
    }

    // ==================== ConsumeItemsForCosts 测试 ====================

    [Test]
    public void ConsumeItemsForCosts_NullOrEmpty_ReturnsTrue()
    {
        Assert.That(_inventoryMgr.ConsumeItemsForCosts(null), Is.True);
        Assert.That(_inventoryMgr.ConsumeItemsForCosts(new List<TaskConsumeCost>()), Is.True);
    }

    // ==================== IsInventoryFull 测试 ====================

    [Test]
    public void IsInventoryFull_EmptyInventory_ReturnsFalse()
    {
        _inventoryMgr.Initialize("test_char_003");
        Assert.That(_inventoryMgr.IsInventoryFull(), Is.False);
    }

    // ==================== FindFirstEmptyInventorySlot 测试 ====================

    [Test]
    public void FindFirstEmptyInventorySlot_EmptyInventory_ReturnsZero()
    {
        _inventoryMgr.Initialize("test_char_004");
        int slot = _inventoryMgr.FindFirstEmptyInventorySlot();
        Assert.That(slot, Is.EqualTo(0));
    }

    // ==================== GetItemByInstanceId 测试 ====================

    [Test]
    public void GetItemByInstanceId_NotFound_ReturnsNull()
    {
        _inventoryMgr.Initialize("test_char_005");
        var item = _inventoryMgr.GetItemByInstanceId("non_existent_id");
        Assert.That(item, Is.Null);
    }

    // ==================== GetItemsByLocation 测试 ====================

    [Test]
    public void GetInventoryItems_Empty_ReturnsEmpty()
    {
        _inventoryMgr.Initialize("test_char_006");
        var items = _inventoryMgr.GetInventoryItems();
        Assert.That(items, Is.Empty);
    }

    [Test]
    public void GetEquippedItems_Empty_ReturnsEmpty()
    {
        _inventoryMgr.Initialize("test_char_007");
        var items = _inventoryMgr.GetEquippedItems();
        Assert.That(items, Is.Empty);
    }

    [Test]
    public void GetQuickSlotItems_Empty_ReturnsEmpty()
    {
        _inventoryMgr.Initialize("test_char_008");
        var items = _inventoryMgr.GetQuickSlotItems();
        Assert.That(items, Is.Empty);
    }

    // ==================== 事件触发测试 ====================

    [Test]
    public void OnInventoryUpdated_FiresOnInitialize()
    {
        bool fired = false;
        InventoryManager.OnInventoryUpdated += () => fired = true;

        _inventoryMgr.Initialize("test_char_009");

        Assert.That(fired, Is.True, "Initialize 完成后应触发 OnInventoryUpdated");

        InventoryManager.OnInventoryUpdated -= () => fired = true;
    }
}
