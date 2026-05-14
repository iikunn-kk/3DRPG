using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 玩家货币管理器，用于管理玩家的金钱和钻石等游戏货币
/// </summary>
public class PlayerCurrencyManager : Singleton<PlayerCurrencyManager>
{
    // 移除 C# 静态事件，使用 ScriptableObject 事件在 Inspector 绑定

    #region 字段和属性

    /// <summary>
    /// 玩家当前拥有的金钱数量
    /// </summary>
    public int Money { get; private set; }

    /// <summary>
    /// 玩家当前拥有的钻石数量
    /// </summary>
    public int Diamonds { get; private set; }

    [Header("事件 (SO 绑定)")]
    [SerializeField] private IntEventSO moneyChangedEvent;
    [SerializeField] private IntEventSO diamondsChangedEvent;
    [SerializeField] private IntEventSO moneyChangedNumberEvent;
    [SerializeField] private IntEventSO diamondsChangedNumberEvent;
    #endregion

    [SerializeField] private Sprite iconImage;
    [SerializeField] private Sprite gemImage;
    #region 初始化方法

    /// <summary>
    /// 从角色数据初始化玩家数据
    /// </summary>
    /// <param name="characterData">角色数据</param>
    public void InitializeFromCharacterData(CharacterData characterData)
    {
        Money = characterData.gold;
        Diamonds = characterData.gem;
        moneyChangedEvent?.RaiseEvent(Money, this);
    }

    #endregion

    #region 数据操作方法

    /// <summary>
    /// 增加金钱
    /// </summary>
    /// <param name="amount">增加的金钱数量</param>
    /// <returns>操作是否成功</returns>
    public bool AddMoney(int amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning("尝试增加负数金钱，应使用RemoveMoney方法");
            return false;
        }
        Money += amount;
        // 同步更新PlayerCharacter中的金币数量
        var playerCharacter = CharacterRuntimeManager.Instance.CurrentPlayerCharacter();
        moneyChangedEvent?.RaiseEvent(Money, this);
        moneyChangedNumberEvent.RaiseEvent(amount, this);
        SaveCoordinator.Instance.SaveCurrentCharacterData();
        UIManager.Instance.ShowToast("获得金币+" + amount, iconImage);
        return true;
    }

    /// <summary>
    /// 减少金钱
    /// </summary>
    /// <param name="amount">减少的金钱数量</param>
    /// <returns>操作是否成功</returns>
    public bool RemoveMoney(int amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning("尝试减少负数金钱，应使用AddMoney方法");
            return false;
        }

        if (Money < amount)
        {
            Debug.LogWarning("金钱不足，无法完成操作");
            return false;
        }
        Money -= amount;
        print("减少金钱" + amount + "剩余" + Money);
        moneyChangedEvent?.RaiseEvent(Money, this);
        UIManager.Instance.ShowToast("消耗了金币" + amount, iconImage);
        SaveCoordinator.Instance.SaveCurrentCharacterData().Forget();
        return true;
    }

    /// <summary>
    /// 增加钻石
    /// </summary>
    /// <param name="amount">增加的钻石数量</param>
    /// <returns>操作是否成功</returns>
    public bool AddDiamonds(int amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning("尝试增加负数钻石，应使用RemoveDiamonds方法");
            return false;
        }
        Diamonds += amount;
        diamondsChangedEvent.RaiseEvent(Diamonds, this);
        diamondsChangedNumberEvent.RaiseEvent(amount, this);
        UIManager.Instance.ShowToast("获得钻石+" + amount, gemImage);
        SaveCoordinator.Instance.SaveCurrentCharacterData();
        return true;
    }

    /// <summary>
    /// 减少钻石
    /// </summary>
    /// <param name="amount">减少的钻石数量</param>
    /// <returns>操作是否成功</returns>
    public bool RemoveDiamonds(int amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning("尝试减少负数钻石，应使用AddDiamonds方法");
            return false;
        }

        if (Diamonds < amount)
        {
            Debug.LogWarning("钻石不足，无法完成操作");
            return false;
        }

        Diamonds -= amount;
        diamondsChangedEvent.RaiseEvent(Diamonds, this);
        UIManager.Instance.ShowToast("消耗了钻石" + amount, gemImage);
        SaveCoordinator.Instance.SaveCurrentCharacterData().Forget();
        return true;
    }

    #endregion

}