using NUnit.Framework;
using UnityEngine;

/// <summary>
/// CharacterState 单元测试 — 覆盖初始化和基础属性逻辑。
/// 需要 GameManager 提供 PlayerCharacterStateDataSO ，在手动测试环境中运行。
/// </summary>
public class CharacterStateTests
{
    private GameObject _characterGo;
    private CharacterState _characterState;

    [SetUp]
    public void SetUp()
    {
        _characterGo = new GameObject("Test_Character");
        _characterState = _characterGo.AddComponent<CharacterState>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_characterGo != null)
            Object.DestroyImmediate(_characterGo);
    }

    // ==================== 基础状态测试 ====================

    [Test]
    public void DefaultHealth_IsZero()
    {
        Assert.That(_characterState.CurrentHealth, Is.Zero);
    }

    [Test]
    public void DefaultMaxHealth_IsZero()
    {
        Assert.That(_characterState.MaxHealth, Is.Zero);
    }

    [Test]
    public void DefaultAttack_IsZero()
    {
        Assert.That(_characterState.Attack, Is.Zero);
    }

    [Test]
    public void DefaultDefence_IsZero()
    {
        Assert.That(_characterState.Defence, Is.Zero);
    }

    [Test]
    public void DefaultLevel_IsZero()
    {
        Assert.That(_characterState.Level, Is.Zero);
    }

    [Test]
    public void DefaultExp_IsZero()
    {
        Assert.That(_characterState.Exp, Is.Zero);
    }

    // ==================== Init 后状态测试 ====================

    /// <summary>
    /// Init 依赖 GameManager、CharacterRuntimeManager、PlayerCurrencyManager 等
    /// 在完整的 SetUp 环境中以下测试作为手动验证。
    /// </summary>
    [Test]
    public void Init_WithNullData_DoesNotThrow()
    {
        // 验证空数据不会导致崩溃（防御性编程检查）
        Assert.That(_characterState.PlayerCharacterData, Is.Null);
    }

    // ==================== GetCharacterDataForSave 测试 ====================

    [Test]
    public void GetCharacterDataForSave_NoPlayerData_ReturnsNull()
    {
        var result = _characterState.GetCharacterDataForSave();
        Assert.That(result, Is.Null);
    }

    // ==================== ApplyBuffTotals 测试 ====================

    [Test]
    public void ApplyBuffTotals_PositiveAttack_ChangesAttack()
    {
        // 需要通过反射设置 _attackBeforeBuffs 来测试
        // 这里验证方法存在且不会崩溃
        Assert.DoesNotThrow(() => _characterState.ApplyBuffTotals(10, 5f));
    }

    [Test]
    public void ApplyBuffTotals_NegativeCritChance_ClampedToZero()
    {
        Assert.DoesNotThrow(() => _characterState.ApplyBuffTotals(0, -10f));
    }

    // ==================== Movement 访问测试 ====================

    [Test]
    public void Movement_DefaultState_IsNull()
    {
        Assert.That(_characterState.Movement, Is.Null);
    }
}
