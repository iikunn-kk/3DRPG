using UnityEngine;
using System;

/// <summary>
/// [已废弃] 货币管理已被 CharacterService 替代，此类无任何调用方。
/// 保留仅用于向后兼容，将在后续版本删除。
/// </summary>
[System.Obsolete("Use CharacterService instead. This class has no callers.", false)]
public class CharacterDataManager : Singleton<CharacterDataManager>
{
    #region 字段和属性
    /// <summary>
    /// 金币数量
    /// </summary>
    public int Coins { get; private set; }
    
    /// <summary>
    /// 钻石数量
    /// </summary>
    public int Diamonds { get; private set; }
    
    /// <summary>
    /// 金币数量变化事件
    /// </summary>
    public Action<int> OnCoinsChanged;
    
    /// <summary>
    /// 钻石数量变化事件
    /// </summary>
    public Action<int> OnDiamondsChanged;
    #endregion
    
    #region 生命周期方法
    protected override void Awake()
    {
        base.Awake();
        // 初始化默认值
        Coins = 0;
        Diamonds = 0;
    }
    #endregion
    
    #region 数据操作方法
    /// <summary>
    /// 增加金币
    /// </summary>
    /// <param name="amount">增加的金币数量</param>
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        Coins += amount;
        OnCoinsChanged?.Invoke(Coins);
    }
    
    /// <summary>
    /// 消费金币
    /// </summary>
    /// <param name="amount">消费的金币数量</param>
    /// <returns>是否消费成功</returns>
    public bool SpendCoins(int amount)
    {
        if (amount <= 0 || Coins < amount) return false;
        Coins -= amount;
        OnCoinsChanged?.Invoke(Coins);
        return true;
    }
    
    /// <summary>
    /// 增加钻石
    /// </summary>
    /// <param name="amount">增加的钻石数量</param>
    public void AddDiamonds(int amount)
    {
        if (amount <= 0) return;
        Diamonds += amount;
        OnDiamondsChanged?.Invoke(Diamonds);
    }
    
    /// <summary>
    /// 消费钻石
    /// </summary>
    /// <param name="amount">消费的钻石数量</param>
    /// <returns>是否消费成功</returns>
    public bool SpendDiamonds(int amount)
    {
        if (amount <= 0 || Diamonds < amount) return false;
        Diamonds -= amount;
        OnDiamondsChanged?.Invoke(Diamonds);
        return true;
    }
    
    /// <summary>
    /// 设置金币数量（仅用于初始化或调试）
    /// </summary>
    /// <param name="amount">金币数量</param>
    public void SetCoins(int amount)
    {
        Coins = amount;
        OnCoinsChanged?.Invoke(Coins);
    }
    
    /// <summary>
    /// 设置钻石数量（仅用于初始化或调试）
    /// </summary>
    /// <param name="amount">钻石数量</param>
    public void SetDiamonds(int amount)
    {
        Diamonds = amount;
        OnDiamondsChanged?.Invoke(Diamonds);
    }
    #endregion
}