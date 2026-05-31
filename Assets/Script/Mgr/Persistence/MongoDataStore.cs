using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameDemo.Models;
using MongoDB.Driver;
using UnityEngine;

/// <summary>
/// MongoDB 数据存储实现。
/// 从原 MongoDBManager.cs 提取，实现 IDataStore 接口。
/// 2秒超时快速探测，不阻塞启动流程。
/// </summary>
public class MongoDataStore : IDataStore
{
    #region 字段

    private MongoClient _client;
    private IMongoDatabase _database;

    private IMongoCollection<PlayerLoginData> _playerCollection;
    private IMongoCollection<CharacterData> _characterCollection;
    private IMongoCollection<GuildData> _guildCollection;
    private IMongoCollection<PlayerInventoryData> _inventoryCollection;
    private IMongoCollection<TaskProgressData> _taskProgressCollection;

    public bool IsAvailable { get; private set; }

    #endregion

    #region 常量

    private const string ConnectionString = "mongodb://127.0.0.1:27017";
    private const string DatabaseName = "GameDemo";
    private const string PlayersCollectionName = "players";
    private const string CharactersCollectionName = "characters";
    private const string GuildsCollectionName = "guilds";
    private const string InventoryCollectionName = "inventories";
    private const string TaskProgressCollectionName = "taskProgress";

    #endregion

    #region 连接探测

    /// <summary>
    /// 快速探测 MongoDB 是否可达。2秒超时。
    /// </summary>
    public async Task<bool> TryConnectAsync()
    {
        try
        {
            var settings = MongoClientSettings.FromConnectionString(ConnectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
            settings.ConnectTimeout = TimeSpan.FromSeconds(2);

            _client = new MongoClient(settings);
            _database = _client.GetDatabase(DatabaseName);

            _playerCollection = _database.GetCollection<PlayerLoginData>(PlayersCollectionName);
            _characterCollection = _database.GetCollection<CharacterData>(CharactersCollectionName);
            _guildCollection = _database.GetCollection<GuildData>(GuildsCollectionName);
            _inventoryCollection = _database.GetCollection<PlayerInventoryData>(InventoryCollectionName);
            _taskProgressCollection = _database.GetCollection<TaskProgressData>(TaskProgressCollectionName);

            await _client.ListDatabaseNamesAsync(); // 真正发起连接
            await CreateIndexesAsync();

            IsAvailable = true;
            Debug.Log("[MongoDataStore] 连接成功，索引已就绪");
            return true;
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            Debug.Log($"[MongoDataStore] 连接失败 ({ex.Message})，将使用本地存储");
            return false;
        }
    }

    private async Task CreateIndexesAsync()
    {
        var playerUsernameIndex = new CreateIndexModel<PlayerLoginData>(
            Builders<PlayerLoginData>.IndexKeys.Ascending(p => p.username),
            new CreateIndexOptions { Unique = true });
        await _playerCollection.Indexes.CreateOneAsync(playerUsernameIndex);

        await _characterCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<CharacterData>(Builders<CharacterData>.IndexKeys.Ascending(c => c.playerUid)));

        var charNameServerIndex = new CreateIndexModel<CharacterData>(
            Builders<CharacterData>.IndexKeys.Combine(
                Builders<CharacterData>.IndexKeys.Ascending(c => c.characterName),
                Builders<CharacterData>.IndexKeys.Ascending(c => c.serverId)),
            new CreateIndexOptions { Unique = true });
        await _characterCollection.Indexes.CreateOneAsync(charNameServerIndex);

        await _inventoryCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<PlayerInventoryData>(Builders<PlayerInventoryData>.IndexKeys.Ascending(i => i.characterId)));

        var taskProgressCharacterIdIndex = new CreateIndexModel<TaskProgressData>(
            Builders<TaskProgressData>.IndexKeys.Ascending(t => t.characterId),
            new CreateIndexOptions { Unique = true });
        await _taskProgressCollection.Indexes.CreateOneAsync(taskProgressCharacterIdIndex);
    }

    #endregion

    #region 泛型辅助

    private async Task<T> FindOneAsync<T>(IMongoCollection<T> collection,
        FilterDefinition<T> filter, string operationName) where T : class
    {
        try { return await collection.Find(filter).FirstOrDefaultAsync(); }
        catch (Exception ex) { Debug.LogError($"{operationName}失败: {ex}"); return null; }
    }

    private async Task<List<T>> FindListAsync<T>(IMongoCollection<T> collection,
        FilterDefinition<T> filter, string operationName)
    {
        try { return await collection.Find(filter).ToListAsync(); }
        catch (Exception ex) { Debug.LogError($"{operationName}失败: {ex}"); return new List<T>(); }
    }

    private async Task<bool> UpsertAsync<T>(IMongoCollection<T> collection,
        FilterDefinition<T> filter, T document, string operationName)
    {
        try { await collection.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true }); return true; }
        catch (Exception ex) { Debug.LogError($"{operationName}失败: {ex}"); return false; }
    }

    private async Task<bool> DeleteOneAsync<T>(IMongoCollection<T> collection,
        FilterDefinition<T> filter, string operationName)
    {
        try { var result = await collection.DeleteOneAsync(filter); return result.DeletedCount > 0; }
        catch (Exception ex) { Debug.LogError($"{operationName}失败: {ex}"); return false; }
    }

    private async Task<bool> ExistsAsync<T>(IMongoCollection<T> collection,
        FilterDefinition<T> filter, string operationName) where T : class
    {
        try { var existing = await collection.Find(filter).FirstOrDefaultAsync(); return existing != null; }
        catch (Exception ex) { Debug.LogError($"{operationName}失败: {ex}"); return false; }
    }

    #endregion

    #region 玩家账户

    public async Task<RegistrationResult> CreatePlayerAccountAsync(string username, string password)
    {
        try
        {
            if (await IsUsernameExistsAsync(username))
            {
                Debug.LogWarning($"用户名 {username} 已存在。");
                return RegistrationResult.UsernameExists;
            }
            PasswordHelper.CreatePasswordHash(password, out var passwordHash, out var passwordSalt);
            var newPlayer = new PlayerLoginData(username, passwordHash, passwordSalt);
            await _playerCollection.InsertOneAsync(newPlayer);
            return RegistrationResult.Success;
        }
        catch (Exception ex)
        {
            Debug.LogError($"创建玩家账户失败: {ex}");
            return RegistrationResult.DatabaseError;
        }
    }

    public async Task<PlayerLoginData> AuthenticatePlayerAsync(string username, string password)
    {
        var player = await FindOneAsync(_playerCollection,
            Builders<PlayerLoginData>.Filter.Eq(p => p.username, username), "验证玩家");
        if (player != null && PasswordHelper.VerifyPasswordHash(password, player.passwordHash, player.passwordSalt))
            return player;
        return null;
    }

    public async Task<bool> IsUsernameExistsAsync(string username)
    {
        try
        {
            var filter = Builders<PlayerLoginData>.Filter.Eq(p => p.username, username);
            long count = await _playerCollection.CountDocumentsAsync(filter);
            return count > 0;
        }
        catch (Exception ex)
        {
            Debug.LogError($"检查用户名失败: {ex}");
            return true;
        }
    }

    public async Task<bool> ChangePlayerPasswordAsync(string username, string oldPassword, string newPassword)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword))
        {
            Debug.LogWarning("用户名、旧密码和新密码均不能为空。");
            return false;
        }
        try
        {
            var filter = Builders<PlayerLoginData>.Filter.Eq(p => p.username, username);
            var player = await _playerCollection.Find(filter).FirstOrDefaultAsync();
            if (player == null || !PasswordHelper.VerifyPasswordHash(oldPassword, player.passwordHash, player.passwordSalt))
            {
                Debug.LogWarning($"用户'{username}'的身份验证失败，无法修改密码。");
                return false;
            }
            PasswordHelper.CreatePasswordHash(newPassword, out var newHash, out var newSalt);
            var update = Builders<PlayerLoginData>.Update
                .Set(p => p.passwordHash, newHash)
                .Set(p => p.passwordSalt, newSalt);
            var result = await _playerCollection.UpdateOneAsync(filter, update);
            return result.IsAcknowledged && result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            Debug.LogError($"修改密码时发生异常: {ex}");
            return false;
        }
    }

    #endregion

    #region 角色数据

    public async Task<bool> CreateAndSaveCharacterData(CharacterData characterData)
    {
        return await UpsertAsync(_characterCollection,
            Builders<CharacterData>.Filter.Eq(c => c.Id, characterData.Id), characterData, "保存角色数据");
    }

    public async Task<List<CharacterData>> GetCharactersByPlayerUID(string playerUid)
    {
        return await FindListAsync(_characterCollection,
            Builders<CharacterData>.Filter.Eq(c => c.playerUid, playerUid), "获取角色列表");
    }

    public async Task<List<CharacterData>> GetCharactersByPlayerUIDAndServer(string playerUid, int serverId)
    {
        var filter = Builders<CharacterData>.Filter.And(
            Builders<CharacterData>.Filter.Eq(c => c.playerUid, playerUid),
            Builders<CharacterData>.Filter.Eq(c => c.serverId, serverId));
        return await FindListAsync(_characterCollection, filter, "获取角色列表");
    }

    public async Task<CharacterData> GetCharacterData(string characterId)
    {
        return await FindOneAsync(_characterCollection,
            Builders<CharacterData>.Filter.Eq(c => c.Id, characterId), "获取角色数据");
    }

    public async Task<bool> DeleteCharacterData(string characterId)
    {
        var result = await DeleteOneAsync(_characterCollection,
            Builders<CharacterData>.Filter.Eq(c => c.Id, characterId), "删除角色数据");
        if (result)
            Debug.Log($"成功删除角色: {characterId}");
        else
            Debug.LogWarning($"未找到要删除的角色: {characterId}");
        return result;
    }

    public async Task<bool> IsCharacterNameExistsOnServer(string characterName, int serverId)
    {
        var filter = Builders<CharacterData>.Filter.And(
            Builders<CharacterData>.Filter.Eq(c => c.characterName, characterName),
            Builders<CharacterData>.Filter.Eq(c => c.serverId, serverId));
        return await ExistsAsync(_characterCollection, filter, "检查角色名");
    }

    #endregion

    #region 公会数据

    public async Task<bool> SaveGuildDataAsync(GuildData guildData)
    {
        return await UpsertAsync(_guildCollection,
            Builders<GuildData>.Filter.Eq(g => g.guildId, guildData.guildId), guildData, "保存公会数据");
    }

    public async Task<GuildData> GetGuildData(string guildId)
    {
        return await FindOneAsync(_guildCollection,
            Builders<GuildData>.Filter.Eq(g => g.guildId, guildId), "获取公会数据");
    }

    public async Task<GuildData> GetGuildDataWithName(string guildName)
    {
        return await FindOneAsync(_guildCollection,
            Builders<GuildData>.Filter.Eq(g => g.guildName, guildName), "根据名称获取公会数据");
    }

    public async Task<List<GuildData>> GetAllGuilds()
    {
        return await FindListAsync(_guildCollection, Builders<GuildData>.Filter.Empty, "获取所有公会数据");
    }

    public async Task<List<GuildData>> GetGuildsByServerId(int serverId)
    {
        return await FindListAsync(_guildCollection,
            Builders<GuildData>.Filter.Eq(g => g.serverId, serverId), "获取服务器公会数据");
    }

    public async Task<bool> IsGuildNameExistsOnServer(string guildName, int serverId)
    {
        var filter = Builders<GuildData>.Filter.And(
            Builders<GuildData>.Filter.Eq(g => g.guildName, guildName),
            Builders<GuildData>.Filter.Eq(g => g.serverId, serverId));
        return await ExistsAsync(_guildCollection, filter, "检查公会名");
    }

    public async Task<bool> RemoveMemberFromGuild(string guildId, string characterName)
    {
        try
        {
            var filter = Builders<GuildData>.Filter.Eq(g => g.guildId, guildId);
            var guild = await _guildCollection.Find(filter).FirstOrDefaultAsync();
            if (guild == null)
            {
                Debug.LogWarning($"未找到ID为 {guildId} 的公会");
                return false;
            }
            var member = guild.members.Find(m => m.characterName == characterName);
            if (member == null)
            {
                Debug.LogWarning($"角色 {characterName} 不在公会中");
                return false;
            }
            guild.members.RemoveAll(m => m.characterName == characterName);
            if (guild.leaderCharacterName == characterName)
            {
                if (guild.members.Count > 0)
                {
                    guild.leaderCharacterName = guild.members[0].characterName;
                    guild.members[0].rank = GuildMemberRank.Leader;
                    Debug.Log($"公会会长已转移给 {guild.members[0].characterName}");
                }
                else
                {
                    await _guildCollection.DeleteOneAsync(filter);
                    Debug.Log("公会已解散，因为没有成员了");
                    return true;
                }
            }
            await _guildCollection.ReplaceOneAsync(filter, guild);
            Debug.Log($"角色 {characterName} 已从公会 {guild.guildName} 中移除");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"从公会移除成员失败: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region 背包数据

    public async Task<bool> SavePlayerInventoryDataAsync(PlayerInventoryData inventoryData)
    {
        return await UpsertAsync(_inventoryCollection,
            Builders<PlayerInventoryData>.Filter.Eq(i => i.characterId, inventoryData.characterId),
            inventoryData, "保存玩家物品数据");
    }

    public async Task<PlayerInventoryData> GetPlayerInventoryDataAsync(string characterId)
    {
        return await FindOneAsync(_inventoryCollection,
            Builders<PlayerInventoryData>.Filter.Eq(i => i.characterId, characterId), "获取玩家物品数据");
    }

    public async Task<PlayerInventoryData> CreatePlayerInventoryDataAsync(string characterId)
    {
        try
        {
            var newInventory = new PlayerInventoryData(characterId);
            await _inventoryCollection.InsertOneAsync(newInventory);
            return newInventory;
        }
        catch (Exception ex)
        {
            Debug.LogError($"创建玩家物品数据失败: {ex}");
            return null;
        }
    }

    #endregion

    #region 任务进度

    public async Task<bool> SaveTaskProgressDataAsync(string characterId, string taskDataJson)
    {
        var taskProgressData = new TaskProgressData(characterId, taskDataJson);
        return await UpsertAsync(_taskProgressCollection,
            Builders<TaskProgressData>.Filter.Eq(t => t.characterId, characterId),
            taskProgressData, "保存角色任务进度数据");
    }

    public async Task<TaskProgressData> GetTaskProgressDataAsync(string characterId)
    {
        return await FindOneAsync(_taskProgressCollection,
            Builders<TaskProgressData>.Filter.Eq(t => t.characterId, characterId), "获取角色任务进度数据");
    }

    public async Task<bool> DeleteTaskProgressDataAsync(string characterId)
    {
        return await DeleteOneAsync(_taskProgressCollection,
            Builders<TaskProgressData>.Filter.Eq(t => t.characterId, characterId), "删除角色任务进度数据");
    }

    #endregion

    #region 连接测试

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var databases = await _client.ListDatabaseNamesAsync();
            await databases.ToListAsync();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"MongoDB连接测试失败: {ex.Message}");
            return false;
        }
    }

    #endregion
}
