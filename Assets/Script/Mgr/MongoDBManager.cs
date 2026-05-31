using System.Collections.Generic;
using System.Threading.Tasks;
using GameDemo.Models;
using UnityEngine;

/// <summary>
/// 数据持久化统一入口（门面模式）。
/// 启动时自动探测：优先 MongoDB，不可用时降级为本地 JSON 文件存储。
/// 所有调用方无需任何修改，仍然通过 MongoDBManager.Instance.XXX() 调用。
/// </summary>
public class MongoDBManager : Singleton<MongoDBManager>
{
    private IDataStore _store;

    public bool IsAvailable => _store?.IsAvailable ?? false;

    protected override async void Awake()
    {
        base.Awake();
        _store = await DataStoreFactory.CreateAsync();
    }

    // ================================================================
    //  以下 23 个方法均为一行转发，调用方完全无感知底层切换
    // ================================================================

    public Task<bool> TestConnection()
        => _store.TestConnectionAsync();

    public Task<RegistrationResult> CreatePlayerAccountAsync(string username, string password)
        => _store.CreatePlayerAccountAsync(username, password);

    public Task<PlayerLoginData> AuthenticatePlayerAsync(string username, string password)
        => _store.AuthenticatePlayerAsync(username, password);

    public Task<bool> IsUsernameExistsAsync(string username)
        => _store.IsUsernameExistsAsync(username);

    public Task<bool> ChangePlayerPasswordAsync(string username, string oldPassword, string newPassword)
        => _store.ChangePlayerPasswordAsync(username, oldPassword, newPassword);

    public Task<bool> CreateAndSaveCharacterData(CharacterData characterData)
        => _store.CreateAndSaveCharacterData(characterData);

    public Task<List<CharacterData>> GetCharactersByPlayerUID(string playerUid)
        => _store.GetCharactersByPlayerUID(playerUid);

    public Task<List<CharacterData>> GetCharactersByPlayerUIDAndServer(string playerUid, int serverId)
        => _store.GetCharactersByPlayerUIDAndServer(playerUid, serverId);

    public Task<CharacterData> GetCharacterData(string characterId)
        => _store.GetCharacterData(characterId);

    public Task<bool> DeleteCharacterData(string characterId)
        => _store.DeleteCharacterData(characterId);

    public Task<bool> IsCharacterNameExistsOnServer(string characterName, int serverId)
        => _store.IsCharacterNameExistsOnServer(characterName, serverId);

    public Task<bool> SaveGuildDataAsync(GuildData guildData)
        => _store.SaveGuildDataAsync(guildData);

    public Task<GuildData> GetGuildData(string guildId)
        => _store.GetGuildData(guildId);

    public Task<GuildData> GetGuildDataWithName(string guildName)
        => _store.GetGuildDataWithName(guildName);

    public Task<List<GuildData>> GetAllGuilds()
        => _store.GetAllGuilds();

    public Task<List<GuildData>> GetGuildsByServerId(int serverId)
        => _store.GetGuildsByServerId(serverId);

    public Task<bool> IsGuildNameExistsOnServer(string guildName, int serverId)
        => _store.IsGuildNameExistsOnServer(guildName, serverId);

    public Task<bool> RemoveMemberFromGuild(string guildId, string characterName)
        => _store.RemoveMemberFromGuild(guildId, characterName);

    public Task<bool> SavePlayerInventoryDataAsync(PlayerInventoryData inventoryData)
        => _store.SavePlayerInventoryDataAsync(inventoryData);

    public Task<PlayerInventoryData> GetPlayerInventoryDataAsync(string characterId)
        => _store.GetPlayerInventoryDataAsync(characterId);

    public Task<PlayerInventoryData> CreatePlayerInventoryDataAsync(string characterId)
        => _store.CreatePlayerInventoryDataAsync(characterId);

    public Task<bool> SaveTaskProgressDataAsync(string characterId, string taskDataJson)
        => _store.SaveTaskProgressDataAsync(characterId, taskDataJson);

    public Task<TaskProgressData> GetTaskProgressDataAsync(string characterId)
        => _store.GetTaskProgressDataAsync(characterId);

    public Task<bool> DeleteTaskProgressDataAsync(string characterId)
        => _store.DeleteTaskProgressDataAsync(characterId);
}
