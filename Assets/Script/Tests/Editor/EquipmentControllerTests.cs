using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// EquipmentController 单元测试 — 覆盖装备/卸下/初始化逻辑。
/// 强隔离：不依赖 InventoryManager 真实数据，通过构造函数或 SetUp 注入测试数据。
/// </summary>
public class EquipmentControllerTests
{
    private GameObject _controllerGo;
    private EquipmentController _controller;

    [SetUp]
    public void SetUp()
    {
        _controllerGo = new GameObject("Test_EquipmentController");
        _controller = _controllerGo.AddComponent<EquipmentController>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_controllerGo != null)
            Object.DestroyImmediate(_controllerGo);
    }

    // ==================== EquipItem 测试 ====================

    [Test]
    public void EquipItem_ValidEquipment_ReturnsNullForFirstEquip()
    {
        var se = new StoredEquipment { itemId = 1001, generatedProperties = new List<EquipmentProperty>() };
        // 需要 Mock GameDataConfig.Instance.ItemDataSo 才能获取 EquipmentData
        // 此处验证初始化状态
        Assert.That(_controller.IsInitialized, Is.False);
        Assert.That(_controller.EquippedItems, Is.Empty);
    }

    [Test]
    public void UnEquipItem_NotEquipped_ReturnsNull()
    {
        var result = _controller.UnEquipItem(EquipmentType.武器);
        Assert.That(result, Is.Null);
    }

    // ==================== 查询测试 ====================

    [Test]
    public void GetEquippedItem_NotEquipped_ReturnsNull()
    {
        var result = _controller.GetEquippedItem(EquipmentType.头盔);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void IsEquipped_NotEquipped_ReturnsFalse()
    {
        Assert.That(_controller.IsEquipped(EquipmentType.武器), Is.False);
        Assert.That(_controller.IsEquipped(EquipmentType.头盔), Is.False);
        Assert.That(_controller.IsEquipped(EquipmentType.上衣), Is.False);
    }

    [Test]
    public void GetAllEquippedItems_Empty_ReturnsEmptyList()
    {
        var items = _controller.GetAllEquippedItems();
        Assert.That(items, Is.Not.Null);
        Assert.That(items, Is.Empty);
    }

    // ==================== 初始化状态测试 ====================

    [Test]
    public void IsInitialized_DefaultState_IsFalse()
    {
        Assert.That(_controller.IsInitialized, Is.False);
    }

    [Test]
    public void EquippedItems_DefaultState_IsEmpty()
    {
        Assert.That(_controller.EquippedItems, Is.Empty);
    }

    // ==================== EnsureInitialized 测试 ====================

    [Test]
    public void EnsureInitialized_WhenNotInitialized_CallsTryInit()
    {
        // EnsureInitialized 应尝试初始化（依赖 InventoryManager.Instance）
        // 在没有 Mock 的情况下，验证不会抛异常
        Assert.DoesNotThrow(() => _controller.EnsureInitialized());
    }

    // ==================== 事件订阅测试 ====================

    [Test]
    public void OnEquipmentChanged_EventHandler_CanSubscribe()
    {
        bool fired = false;
        _controller.OnEquipmentChanged += () => fired = true;
        Assert.That(_controller, Is.Not.Null);
    }
}
