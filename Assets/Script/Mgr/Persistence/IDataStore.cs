using System.Collections.Generic;
using System.Threading.Tasks;
using GameDemo.Models;

/// <summary>
/// 数据持久化统一接口。
/// 定义所有 CRUD 方法签名，MongoDataStore 和 LocalJsonDataStore 均实现此接口。
/// </summary>
public interface IDataStore
{
    /// <summary>当前后端是否可用</summary>
    bool IsAvailable { get; }

    /// <summary>测试连接</summary>
    Task<bool> TestConnectionAsync();

    // ===== 玩家账户 =====

    Task<RegistrationResult> CreatePlayerAccountAsync(string username, string password);
    Task<PlayerLoginData> AuthenticatePlayerAsync(string username, string password);
    Task<bool> IsUsernameExistsAsync(string username);
    Task<bool> ChangePlayerPasswordAsync(string username, string oldPassword, string newPassword);

    // ===== 角色数据 =====

    Task<bool> CreateAndSaveCharacterData(CharacterData characterData);
    Task<List<CharacterData>> GetCharactersByPlayerUID(string playerUid);
    Task<List<CharacterData>> GetCharactersByPlayerUIDAndServer(string playerUid, int serverId);
    Task<CharacterData> GetCharacterData(string characterId);
    Task<bool> DeleteCharacterData(string characterId);
    Task<bool> IsCharacterNameExistsOnServer(string characterName, int serverId);

    // ===== 公会数据 =====

    Task<bool> SaveGuildDataAsync(GuildData guildData);
    Task<GuildData> GetGuildData(string guildId);
    Task<GuildData> GetGuildDataWithName(string guildName);
    Task<List<GuildData>> GetAllGuilds();
    Task<List<GuildData>> GetGuildsByServerId(int serverId);
    Task<bool> IsGuildNameExistsOnServer(string guildName, int serverId);
    Task<bool> RemoveMemberFromGuild(string guildId, string characterName);

    // ===== 背包数据 =====

    Task<bool> SavePlayerInventoryDataAsync(PlayerInventoryData inventoryData);
    Task<PlayerInventoryData> GetPlayerInventoryDataAsync(string characterId);
    Task<PlayerInventoryData> CreatePlayerInventoryDataAsync(string characterId);

    // ===== 任务进度 =====

    Task<bool> SaveTaskProgressDataAsync(string characterId, string taskDataJson);
    Task<TaskProgressData> GetTaskProgressDataAsync(string characterId);
    Task<bool> DeleteTaskProgressDataAsync(string characterId);
}
