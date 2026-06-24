using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [已废弃] 请使用 CharacterService 替代。
/// 保留仅用于向后兼容，将在后续版本删除。
/// </summary>
[System.Obsolete("Use CharacterService instead.", false)]
public class CharacterRuntimeManager : Singleton<CharacterRuntimeManager>
{
    // ==================== 角色实例引用 ====================

    /// <summary>
    /// 当前玩家角色实例的引用
    /// </summary>
    private CharacterState _currentCharacterState;

    /// <summary>
    /// 获取当前玩家角色实例
    /// </summary>
    public CharacterState CurrentPlayerCharacter()
    {
        if (_currentCharacterState)
        {
            return _currentCharacterState;
        }
        else
        {
            Debug.LogWarning("[CharacterRuntimeManager] CurrentPlayerCharacter 回退到 FindGameObjectWithTag，请检查 SetPlayerCharacter 是否未调用");
            var data = GameObject.FindGameObjectWithTag("Player")?.GetComponent<CharacterState>();
            _currentCharacterState = data;
            return _currentCharacterState;
        }
    }

    /// <summary>
    /// 设置当前玩家角色实例
    /// </summary>
    public void SetPlayerCharacter(CharacterState characterState)
    {
        _currentCharacterState = characterState;
    }

    /// <summary>
    /// 清除玩家实例引用
    /// </summary>
    public void UnsetPlayerInstance()
    {
        _currentCharacterState = null;
    }

    // ==================== 地图管理 ====================

    /// <summary>
    /// 当前地图管理器引用
    /// </summary>
    public MapManager currentMapManager { get; private set; }

    /// <summary>
    /// 设置当前地图管理器
    /// </summary>
    public void SetMapManager(MapManager mapManager)
    {
        currentMapManager = mapManager;
    }

    // ==================== 临时状态快照（跨场景保留） ====================

    /// <summary>
    /// 临时状态快照列表 - 跨场景保留，不持久化存档
    /// </summary>
    private List<CharacterBuffs.BuffSnapshot> _savedBuffs;

    /// <summary>
    /// Buff视觉效果快照列表
    /// </summary>
    private List<CharacterBuffs.VisualSnapshot> _savedBuffVisuals;

    /// <summary>
    /// 保存场景切换时的角色状态
    /// </summary>
    public void SaveSceneTransitionPlayerState(CharacterState player)
    {
        if (player == null) { _savedBuffs = null; _savedBuffVisuals = null; return; }

        var buffs = player.GetComponent<CharacterBuffs>();
        if (buffs != null)
        {
            _savedBuffs = buffs.GetBuffSnapshots();
            _savedBuffVisuals = buffs.GetVisualSnapshots();
        }
        else
        {
            _savedBuffs = null;
            _savedBuffVisuals = null;
        }
    }

    /// <summary>
    /// 恢复角色的临时状态
    /// </summary>
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

    /// <summary>
    /// 是否有已保存的Buff快照
    /// </summary>
    public bool HasSavedBuffState => _savedBuffs != null;
}
