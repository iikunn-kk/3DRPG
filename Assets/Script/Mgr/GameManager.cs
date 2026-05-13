using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System;

/// <summary>
/// 游戏管理器 - 全局核心管理类（逐步瘦身中）
/// 采用单例模式设计，负责管理游戏中的全局状态和系统协调
/// 当前职责：
/// 1. 持有需 Inspector 赋值的 ScriptableObject 引用（待迁移）
/// 2. 协调各子系统之间的通信
/// 3. 作为旧系统调用的转发 Facade
/// </summary>
public class GameManager : Singleton<GameManager>
{
    // ==================== 角色数据管理（已迁移至 SessionManager） ====================

    [System.Obsolete("请使用 SessionManager.Instance.CurrentCharacter")]
    public CharacterData CurrentCharacter => SessionManager.Instance.CurrentCharacter;

    /// <summary>
    /// 角色状态数据配置资源（保留，需要 Inspector 赋值）
    /// </summary>
    public PlayerCharacterStateDataSO playerCharacterStateDataSo;

    [System.Obsolete("请通过 GameManager.Instance.playerCharacterStateDataSo 访问")]
    [SerializeField] private CharacterSelectDataSO characterSelectDataSo;

    [System.Obsolete("请通过 GameManager.Instance.ItemDataSo 访问")]
    [SerializeField] private ItemDataSO itemDataSo;
    public ItemDataSO ItemDataSo => itemDataSo;

    [System.Obsolete("请使用 SessionManager.Instance.PlayerLoginData")]
    public PlayerLoginData PlayerLoginData => SessionManager.Instance.PlayerLoginData;

    // ==================== 地图管理（已迁移至 CharacterRuntimeManager） ====================

    [System.Obsolete("请使用 CharacterRuntimeManager.Instance.currentMapManager")]
    public MapManager currentMapManager => CharacterRuntimeManager.Instance.currentMapManager;

    // ==================== 角色实例引用（已迁移至 CharacterRuntimeManager） ====================

    [System.Obsolete("请使用 CharacterRuntimeManager.Instance.CurrentPlayerCharacter()")]
    public CharacterState CurrentPlayerCharacter() => CharacterRuntimeManager.Instance.CurrentPlayerCharacter();

    // ==================== 属性缩放配置 ====================

    [SerializeField] private PropertyScalingDataSO propertyScalingDataSo;
    public PropertyScalingDataSO PropertyScalingData => propertyScalingDataSo;

    // ==================== 临时状态快照（已迁移至 CharacterRuntimeManager） ====================

    [System.Obsolete("请使用 CharacterRuntimeManager.Instance.SaveSceneTransitionPlayerState")]
    public void SaveSceneTransitionPlayerState(CharacterState player) => CharacterRuntimeManager.Instance.SaveSceneTransitionPlayerState(player);

    [System.Obsolete("请使用 CharacterRuntimeManager.Instance.RestoreTransientPlayerState")]
    public void RestoreTransientPlayerState(CharacterState player) => CharacterRuntimeManager.Instance.RestoreTransientPlayerState(player);

    [System.Obsolete("请使用 CharacterRuntimeManager.Instance.UnsetPlayerInstance")]
    public void UnsetPlayerInstance() => CharacterRuntimeManager.Instance.UnsetPlayerInstance();

    private void Start()
    {
        Application.targetFrameRate = 60;
    }

    // ==================== 旧背包系统转发方法（即将废弃） ====================

    [System.Obsolete("请使用 LegacyPackageManager.Instance.DeletePackageItems")]
    public void DeletePackageItems(List<string> uids) => LegacyPackageManager.Instance.DeletePackageItems(uids);

    [System.Obsolete("请使用 LegacyPackageManager.Instance.DeletePackageItem")]
    public void DeletePackageItem(string uid, bool needSave = true) => LegacyPackageManager.Instance.DeletePackageItem(uid, needSave);

    [System.Obsolete("请使用 LegacyPackageManager.Instance.GetPackageTable")]
    public PackageTable GetPackageTable() => LegacyPackageManager.Instance.GetPackageTable();

    [System.Obsolete("请使用 LegacyPackageManager.Instance.GetPackageTableByType")]
    public List<PackageTableItem> GetPackageTableByType(int type) => LegacyPackageManager.Instance.GetPackageTableByType(type);

    [System.Obsolete("请使用 LegacyPackageManager.Instance.GetLotteryRandom1")]
    public InventoryItem GetLotteryRandom1() => LegacyPackageManager.Instance.GetLotteryRandom1();

    [System.Obsolete("请使用 LegacyPackageManager.Instance.GetLotteryRandom10")]
    public List<InventoryItem> GetLotteryRandom10(bool sort = false) => LegacyPackageManager.Instance.GetLotteryRandom10(sort);

    [System.Obsolete("请使用 LegacyPackageManager.Instance.CheckWeaponIsNew")]
    public bool CheckWeaponIsNew(int id) => LegacyPackageManager.Instance.CheckWeaponIsNew(id);

    [System.Obsolete("请使用 LegacyPackageManager.Instance.GetPackageLocalData")]
    public List<PackageLocalItem> GetPackageLocalData() => LegacyPackageManager.Instance.GetPackageLocalData();

    [System.Obsolete("请使用 LegacyPackageManager.Instance.GetPackageItemById")]
    public PackageTableItem GetPackageItemById(int id) => LegacyPackageManager.Instance.GetPackageItemById(id);

    [System.Obsolete("请使用 LegacyPackageManager.Instance.GetPackageLocalItemByUId")]
    public PackageLocalItem GetPackageLocalItemByUId(string uid) => LegacyPackageManager.Instance.GetPackageLocalItemByUId(uid);

    [System.Obsolete("请使用 LegacyPackageManager.Instance.GetSortPackageLocalData")]
    public List<PackageLocalItem> GetSortPackageLocalData() => LegacyPackageManager.Instance.GetSortPackageLocalData();

    // ==================== 核心数据接口（已迁移至 SessionManager / CharacterRuntimeManager） ====================

    [System.Obsolete("请使用 SessionManager.Instance.SetCurrentCharacterData")]
    public void SetCurrentCharacterData(CharacterData character) => SessionManager.Instance.SetCurrentCharacterData(character);

    [System.Obsolete("请使用 CharacterRuntimeManager.Instance.SetPlayerCharacter")]
    public void SetPlayerCharacter(CharacterState characterState) => CharacterRuntimeManager.Instance.SetPlayerCharacter(characterState);

    [System.Obsolete("请使用 CharacterRuntimeManager.Instance.SetMapManager")]
    public void SetMapManager(MapManager mapManager) => CharacterRuntimeManager.Instance.SetMapManager(mapManager);

    // ==================== 公会系统接口（即将废弃） ====================

    [System.Obsolete("请直接使用 GuildManager.Instance.CreateGuild")]
    public async Task<bool> CreateGuild(string guildName, string guildDescription)
        => await GuildManager.Instance.CreateGuild(guildName, guildDescription);

    [System.Obsolete("请直接使用 GuildManager.Instance.JoinGuild")]
    public async Task<bool> JoinGuild(string guildId)
        => await GuildManager.Instance.JoinGuild(guildId);

    [System.Obsolete("请直接使用 GuildManager.Instance.QuitGuild")]
    public async Task<bool> QuitGuild()
        => await GuildManager.Instance.QuitGuild();

    // ==================== 数据保存系统（已迁移至 SaveCoordinator） ====================

    [System.Obsolete("请使用 SaveCoordinator.Instance.SaveCurrentCharacterData")]
    public async void SaveCurrentCharacterData() => SaveCoordinator.Instance.SaveCurrentCharacterData();
}
