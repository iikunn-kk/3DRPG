using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 角色服务 — 合并 CharacterRuntimeManager + PlayerCurrencyManager。
/// 绞杀完成后将替代这两个旧类。
/// </summary>
public class CharacterService : Singleton<CharacterService>
{
    // ==================== 角色实例引用（来自 CharacterRuntimeManager） ====================

    private CharacterState _currentCharacterState;

    public CharacterState CurrentPlayerCharacter()
    {
        if (_currentCharacterState)
            return _currentCharacterState;

        Debug.LogWarning("[CharacterService] CurrentPlayerCharacter 回退到 FindGameObjectWithTag，请检查初始化");
        var data = GameObject.FindGameObjectWithTag("Player")?.GetComponent<CharacterState>();
        _currentCharacterState = data;
        return _currentCharacterState;
    }

    public void SetPlayerCharacter(CharacterState characterState) => _currentCharacterState = characterState;
    public void UnsetPlayerInstance() => _currentCharacterState = null;

    // ==================== 地图管理 ====================

    public MapManager currentMapManager { get; private set; }
    public void SetMapManager(MapManager mapManager) => currentMapManager = mapManager;

    // ==================== 场景切换 Buff 快照 ====================

    private List<CharacterBuffs.BuffSnapshot> _savedBuffs;
    private List<CharacterBuffs.VisualSnapshot> _savedBuffVisuals;
    public bool HasSavedBuffState => _savedBuffs != null;

    public void SaveSceneTransitionPlayerState(CharacterState player)
    {
        if (player == null) { _savedBuffs = null; _savedBuffVisuals = null; return; }

        var buffs = player.GetComponent<CharacterBuffs>();
        if (buffs != null)
        {
            _savedBuffs = buffs.GetBuffSnapshots();
            _savedBuffVisuals = buffs.GetVisualSnapshots();
        }
        else { _savedBuffs = null; _savedBuffVisuals = null; }
    }

    public void RestoreTransientPlayerState(CharacterState player)
    {
        if (player == null) return;
        var buffs = player.GetComponent<CharacterBuffs>();
        if (buffs != null)
        {
            if (_savedBuffs != null) buffs.ApplyBuffSnapshots(_savedBuffs);
            if (_savedBuffVisuals != null) buffs.RestoreVisualSnapshots(_savedBuffVisuals);
        }
        _savedBuffs = null;
        _savedBuffVisuals = null;
    }

    // ==================== 货币管理（来自 PlayerCurrencyManager） ====================

    public int Money { get; private set; }
    public int Diamonds { get; private set; }

    [Header("事件 (SO 绑定)")]
    [SerializeField] private IntEventSO moneyChangedEvent;
    [SerializeField] private IntEventSO diamondsChangedEvent;
    [SerializeField] private IntEventSO moneyChangedNumberEvent;
    [SerializeField] private IntEventSO diamondsChangedNumberEvent;

    [SerializeField] private Sprite iconImage;
    [SerializeField] private Sprite gemImage;

    public void InitializeFromCharacterData(CharacterData characterData)
    {
        Money = characterData.gold;
        Diamonds = characterData.gem;
        moneyChangedEvent?.RaiseEvent(Money, this);
    }

    public bool AddMoney(int amount)
    {
        if (amount < 0) { Debug.LogWarning("尝试增加负数金钱"); return false; }
        Money += amount;
        moneyChangedEvent?.RaiseEvent(Money, this);
        moneyChangedNumberEvent.RaiseEvent(amount, this);
        SaveCoordinator.Instance.SaveCurrentCharacterData();
        UIManager.Instance.ShowToast("获得金币+" + amount, iconImage);
        return true;
    }

    public bool RemoveMoney(int amount)
    {
        if (amount < 0) { Debug.LogWarning("尝试减少负数金钱"); return false; }
        if (Money < amount) { Debug.LogWarning("金钱不足"); return false; }
        Money -= amount;
        moneyChangedEvent?.RaiseEvent(Money, this);
        UIManager.Instance.ShowToast("消耗了金币" + amount, iconImage);
        SaveCoordinator.Instance.SaveCurrentCharacterData().Forget();
        return true;
    }

    public bool AddDiamonds(int amount)
    {
        if (amount < 0) { Debug.LogWarning("尝试增加负数钻石"); return false; }
        Diamonds += amount;
        diamondsChangedEvent.RaiseEvent(Diamonds, this);
        diamondsChangedNumberEvent.RaiseEvent(amount, this);
        UIManager.Instance.ShowToast("获得钻石+" + amount, gemImage);
        SaveCoordinator.Instance.SaveCurrentCharacterData();
        return true;
    }

    public bool RemoveDiamonds(int amount)
    {
        if (amount < 0) { Debug.LogWarning("尝试减少负数钻石"); return false; }
        if (Diamonds < amount) { Debug.LogWarning("钻石不足"); return false; }
        Diamonds -= amount;
        diamondsChangedEvent.RaiseEvent(Diamonds, this);
        UIManager.Instance.ShowToast("消耗了钻石" + amount, gemImage);
        SaveCoordinator.Instance.SaveCurrentCharacterData().Forget();
        return true;
    }
}
