using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using UnityEngine;
using GameDemo.Models;

/// <summary>
/// MongoDB管理类，用于处理与MongoDB的连接和数据操作
/// 这是一个单例类，确保在整个应用程序中只有一个MongoDB连接实例
/// </summary>
public class MongoDBManager : Singleton<MongoDBManager>
{
    #region 字段和属性

    // MongoDB客户端，用于与MongoDB服务器建立连接
    private MongoClient _client;
    // MongoDB数据库实例，代表我们连接的具体数据库
    private IMongoDatabase _database;

    // 数据集合引用，每个集合对应数据库中的一个"表"
    // 玌玩家登录数据集合 - 存储用户账户信息
    private IMongoCollection<PlayerLoginData> _playerCollection;
    // 角色数据集合 - 存储游戏角色信息
    private IMongoCollection<CharacterData> _characterCollection;
    // 公会数据集合 - 存储游戏公会信息
    private IMongoCollection<GuildData> _guildCollection;
    // 物品数据集合 - 存储玩家物品信息
    private IMongoCollection<PlayerInventoryData> _inventoryCollection;
    // 任务进度数据集合 - 存储玩家任务进度信息
    private IMongoCollection<TaskProgressData> _taskProgressCollection;

    // 初始化状态
    public bool IsInitialized { get; private set; }
    public Exception InitializationException { get; private set; }
    public Task InitializationTask { get; private set; }

    #region 常量
    // 使用常量管理集合名称，避免硬编码和拼写错误
    private const string PlayersCollectionName = "players";
    private const string CharactersCollectionName = "characters";
    private const string GuildsCollectionName = "guilds";
    private const string InventoryCollectionName = "inventories";
    private const string TaskProgressCollectionName = "taskProgress";
    #endregion

    [Header("MongoDB配置")]
    // MongoDB连接字符串，指定服务器地址和端口
    // mongodb://127.0.0.1:27017 表示连接本地MongoDB服务的默认端口
    public string connectionString = "mongodb://127.0.0.1:27017";
    // 数据库名称，指定要连接的数据库
    public string databaseName = "GameDemo";

    #endregion

    #region Unity生命周期方法

    /// <summary>
    /// Unity生命周期函数，在对象启用时调用
    /// 这里用于初始化MongoDB连接
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        // 记录初始化任务，供其它方法等待
        InitializationTask = InitializeMongoDBAsync();
    }

    #endregion

    #region 初始化方法

    private async Task InitializeMongoDBAsync()
    {
        try
        {
            //创建Mongo客户端
            var mongoSettings = MongoClientSettings.FromConnectionString(connectionString);
            mongoSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
            mongoSettings.ConnectTimeout = TimeSpan.FromSeconds(10);

            _client = new MongoClient(mongoSettings);
            //获取数据库
            _database = _client.GetDatabase(databaseName);

            _playerCollection = _database.GetCollection<PlayerLoginData>(PlayersCollectionName);
            _characterCollection = _database.GetCollection<CharacterData>(CharactersCollectionName);
            _guildCollection = _database.GetCollection<GuildData>(GuildsCollectionName);
            _inventoryCollection = _database.GetCollection<PlayerInventoryData>(InventoryCollectionName);
            _taskProgressCollection = _database.GetCollection<TaskProgressData>(TaskProgressCollectionName);

            Debug.Log("MongoDB连接初始化成功。");

            // 初始化完成后，确保索引已创建
            await CreateIndexesAsync();

            IsInitialized = true;
            InitializationException = null;
        }
        catch (Exception ex)
        {
            InitializationException = ex;
            IsInitialized = false;
            Debug.LogError($"MongoDB连接失败: {ex}");
        }
    }
    #endregion

    private async Task EnsureInitialized()
    {
        // 已完成初始化
        if (IsInitialized)
            return;

        // 如果 Awake 还未执行，确保此处也能触发初始化
        if (InitializationTask == null)
        {
            InitializationTask = InitializeMongoDBAsync();
        }

        // 等待初始化任务结束（如果仍在进行）
        if (InitializationTask != null)
        {
            try { await InitializationTask; }
            catch { /* 已在 InitializeMongoDBAsync 捕获并记录 */ }
        }
    }

    /// <summary>
    /// 为常用查询字段创建索引，以保证查询性能。此操作是幂等的。
    /// </summary>
    private async Task CreateIndexesAsync()
    {
        // 为 players 集合的 username 字段创建唯一索引
        var playerUsernameIndex = new CreateIndexModel<PlayerLoginData>(
            Builders<PlayerLoginData>.IndexKeys.Ascending(p => p.username),
            new CreateIndexOptions { Unique = true }
        );
        await _playerCollection.Indexes.CreateOneAsync(playerUsernameIndex);

        // 为 characters 集合的 playerUid 字段创建索引
        await _characterCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<CharacterData>(Builders<CharacterData>.IndexKeys.Ascending(c => c.playerUid))
        );

        // 为 characters 集合的 (characterName, serverId) 创建复合唯一索引，防止同服重名
        var charNameServerIndex = new CreateIndexModel<CharacterData>(
            Builders<CharacterData>.IndexKeys.Combine(
                Builders<CharacterData>.IndexKeys.Ascending(c => c.characterName),
                Builders<CharacterData>.IndexKeys.Ascending(c => c.serverId)
            ),
            new CreateIndexOptions { Unique = true }
        );
        await _characterCollection.Indexes.CreateOneAsync(charNameServerIndex);

        // 为 inventories 集合的 characterId 字段创建索引
        await _inventoryCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<PlayerInventoryData>(Builders<PlayerInventoryData>.IndexKeys.Ascending(i => i.characterId))
        );

        // 为 taskProgress 集合的 characterId 字段创建唯一索引
        var taskProgressCharacterIdIndex = new CreateIndexModel<TaskProgressData>(
            Builders<TaskProgressData>.IndexKeys.Ascending(t => t.characterId),
            new CreateIndexOptions { Unique = true }
        );
        await _taskProgressCollection.Indexes.CreateOneAsync(taskProgressCharacterIdIndex);

        Debug.Log("数据库索引已确认。");
    }

    // ================================================================
    //  泛型辅助方法 —— 消除 EnsureInitialized + null检查 + try-catch 样板重复
    // ================================================================

    #region 泛型辅助方法

    /// <summary>初始化守卫 + 集合空检查。返回 false 表示未就绪。</summary>
    private async Task<bool> EnsureCollection(object collection, string operationName)
    {
        await EnsureInitialized();
        if (!IsInitialized || collection == null)
        {
            Debug.LogError($"MongoDB 尚未初始化，无法{operationName}。");
            return false;
        }
        return true;
    }

    /// <summary>通用单条查询：Find + FirstOrDefaultAsync。</summary>
    private async Task<T> FindOneAsync<T>(IMongoCollection<T> collection,
        FilterDefinition<T> filter, string operationName) where T : class
    {
        if (!await EnsureCollection(collection, operationName)) return null;
        try { return await collection.Find(filter).FirstOrDefaultAsync(); }
        catch (Exception ex) { Debug.LogError($"{operationName}失败: {ex}"); return null; }
    }

    /// <summary>通用列表查询：Find + ToListAsync。</summary>
    private async Task<List<T>> FindListAsync<T>(IMongoCollection<T> collection,
        FilterDefinition<T> filter, string operationName)
    {
        if (!await EnsureCollection(collection, operationName)) return new List<T>();
        try { return await collection.Find(filter).ToListAsync(); }
        catch (Exception ex) { Debug.LogError($"{operationName}失败: {ex}"); return new List<T>(); }
    }

    /// <summary>通用 Upsert：ReplaceOneAsync + IsUpsert。</summary>
    private async Task<bool> UpsertAsync<T>(IMongoCollection<T> collection,
        FilterDefinition<T> filter, T document, string operationName)
    {
        if (!await EnsureCollection(collection, operationName)) return false;
        try { await collection.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true }); return true; }
        catch (Exception ex) { Debug.LogError($"{operationName}失败: {ex}"); return false; }
    }

    /// <summary>通用删除：DeleteOneAsync。</summary>
    private async Task<bool> DeleteOneAsync<T>(IMongoCollection<T> collection,
        FilterDefinition<T> filter, string operationName)
    {
        if (!await EnsureCollection(collection, operationName)) return false;
        try { var result = await collection.DeleteOneAsync(filter); return result.DeletedCount > 0; }
        catch (Exception ex) { Debug.LogError($"{operationName}失败: {ex}"); return false; }
    }

    /// <summary>通用存在性检查：FirstOrDefaultAsync != null。</summary>
    private async Task<bool> ExistsAsync<T>(IMongoCollection<T> collection,
        FilterDefinition<T> filter, string operationName) where T : class
    {
        if (!await EnsureCollection(collection, operationName)) return false;
        try { var existing = await collection.Find(filter).FirstOrDefaultAsync(); return existing != null; }
        catch (Exception ex) { Debug.LogError($"{operationName}失败: {ex}"); return false; }
    }

    #endregion

    #region 玩家账户操作 (安全)

    /// <summary>
    /// 创建新玩家账户，使用加盐哈希存储密码。
    /// </summary>
    public async Task<RegistrationResult> CreatePlayerAccountAsync(string username, string password)
    {
        if (!await EnsureCollection(_playerCollection, "创建玩家账户"))
            return RegistrationResult.DatabaseError;

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

    /// <summary>
    /// 验证玩家登录。
    /// </summary>
    public async Task<PlayerLoginData> AuthenticatePlayerAsync(string username, string password)
    {
        var player = await FindOneAsync(_playerCollection,
            Builders<PlayerLoginData>.Filter.Eq(p => p.username, username),
            "验证玩家");

        if (player != null && PasswordHelper.VerifyPasswordHash(password, player.passwordHash, player.passwordSalt))
            return player;

        return null;
    }

    /// <summary>
    /// 检查用户名是否存在（高效版）。
    /// </summary>
    public async Task<bool> IsUsernameExistsAsync(string username)
    {
        if (!await EnsureCollection(_playerCollection, "检查用户名"))
            return true; // 保守处理，阻止创建

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
    /// <summary>
    /// 安全地修改玩家密码。
    /// 此方法会先验证旧密码是否正确，然后才会更新为新密码。
    /// </summary>
    /// <param name="username">玩家的用户名。</param>
    /// <param name="oldPassword">玩家当前的（旧的）密码。</param>
    /// <param name="newPassword">玩家想要设置的新密码。</param>
    /// <returns>如果密码修改成功，返回true；如果用户名或旧密码错误，或发生其他异常，返回false。</returns>
    public async Task<bool> ChangePlayerPasswordAsync(string username, string oldPassword, string newPassword)
    {
        // 1. 基本输入验证
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword))
        {
            Debug.LogWarning("用户名、旧密码和新密码均不能为空。");
            return false;
        }

        if (!await EnsureCollection(_playerCollection, "修改密码"))
            return false;

        try
        {
            // 2. 查找用户并验证旧密码 (身份验证)
            var filter = Builders<PlayerLoginData>.Filter.Eq(p => p.username, username);
            var player = await _playerCollection.Find(filter).FirstOrDefaultAsync();

            if (player == null || !PasswordHelper.VerifyPasswordHash(oldPassword, player.passwordHash, player.passwordSalt))
            {
                // 如果用户不存在，或旧密码验证失败，则直接返回失败
                Debug.LogWarning($"用户'{username}'的身份验证失败，无法修改密码。");
                return false;
            }

            // 验证通过，可以继续修改密码

            // 3. 为新密码生成新的哈希和盐
            PasswordHelper.CreatePasswordHash(newPassword, out var newPasswordHash, out var newPasswordSalt);

            // 4. 使用 $set 原子地更新密码哈希和盐，这是最高效的方式
            var update = Builders<PlayerLoginData>.Update
                .Set(p => p.passwordHash, newPasswordHash)
                .Set(p => p.passwordSalt, newPasswordSalt);

            var result = await _playerCollection.UpdateOneAsync(filter, update);

            // 5. 检查更新是否成功
            // result.ModifiedCount > 0 确保了确实有一条文档被修改了
            return result.IsAcknowledged && result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            Debug.LogError($"修改密码时发生异常: {ex}");
            return false;
        }
    }
    #endregion


    #region 角色数据操作

    /// <summary>
    /// 保存角色数据（优化版）
    /// 使用 Upsert 模式: 如果文档存在则替换，不存在则创建。操作是原子的，且只需一次数据库交互。
    /// </summary>
    /// <param name="characterData">要保存的角色数据</param>
    /// <returns>操作是否成功</returns>
    public async Task<bool> CreateAndSaveCharacterData(CharacterData characterData)
    {
        return await UpsertAsync(_characterCollection,
            Builders<CharacterData>.Filter.Eq(c => c.Id, characterData.Id),
            characterData, "保存角色数据");
    }
    /// <summary>
    /// 根据玩家UID获取角色列表
    /// 用于获取特定玩家拥有的所有角色
    /// </summary>
    /// <param name="playerUid">玩家的唯一ID</param>
    /// <returns>角色数据列表</returns>
    public async Task<List<CharacterData>> GetCharactersByPlayerUID(string playerUid)
    {
        return await FindListAsync(_characterCollection,
            Builders<CharacterData>.Filter.Eq(c => c.playerUid, playerUid),
            "获取角色列表");
    }

    /// <summary>
    /// 根据玩家UID和服务器ID获取角色列表
    /// 用于获取特定玩家在特定服务器上的所有角色
    /// </summary>
    /// <param name="playerUid">玩家的唯一ID</param>
    /// <param name="serverId">服务器ID</param>
    /// <returns>角色数据列表</returns>
    public async Task<List<CharacterData>> GetCharactersByPlayerUIDAndServer(string playerUid, int serverId)
    {
        var filter = Builders<CharacterData>.Filter.And(
            Builders<CharacterData>.Filter.Eq(c => c.playerUid, playerUid),
            Builders<CharacterData>.Filter.Eq(c => c.serverId, serverId)
        );
        return await FindListAsync(_characterCollection, filter, "获取角色列表");
    }

    /// <summary>
    /// 根据角色ID获取角色数据
    /// 用于获取特定角色的详细信息
    /// </summary>
    /// <param name="characterId">角色的唯一ID</param>
    /// <returns>角色数据</returns>
    public async Task<CharacterData> GetCharacterData(string characterId)
    {
        return await FindOneAsync(_characterCollection,
            Builders<CharacterData>.Filter.Eq(c => c.Id, characterId),
            "获取角色数据");
    }

    /// <summary>
    /// 删除角色数据
    /// </summary>
    /// <param name="characterId">要删除的角色ID</param>
    /// <returns>操作是否成功</returns>
    public async Task<bool> DeleteCharacterData(string characterId)
    {
        var result = await DeleteOneAsync(_characterCollection,
            Builders<CharacterData>.Filter.Eq(c => c.Id, characterId),
            "删除角色数据");
        if (result)
            Debug.Log($"成功删除角色: {characterId}");
        else
            Debug.LogWarning($"未找到要删除的角色: {characterId}");
        return result;
    }

    /// <summary>
    /// 检查指定服务器上是否有重复的角色名
    /// </summary>
    /// <param name="characterName">要检查的角色名</param>
    /// <param name="serverId">服务器ID</param>
    /// <returns>角色名是否已存在</returns>
    public async Task<bool> IsCharacterNameExistsOnServer(string characterName, int serverId)
    {
        var filter = Builders<CharacterData>.Filter.And(
            Builders<CharacterData>.Filter.Eq(c => c.characterName, characterName),
            Builders<CharacterData>.Filter.Eq(c => c.serverId, serverId)
        );
        return await ExistsAsync(_characterCollection, filter, "检查角色名");
    }

    #endregion

    #region 公会数据操作

    /// <summary>
    /// 保存公会数据 (采用Upsert模式，高性能且原子)。
    /// </summary>
    public async Task<bool> SaveGuildDataAsync(GuildData guildData)
    {
        return await UpsertAsync(_guildCollection,
            Builders<GuildData>.Filter.Eq(g => g.guildId, guildData.guildId),
            guildData, "保存公会数据");
    }

    /// <summary>
    /// 根据公会ID获取公会数据
    /// 用于获取特定公会的详细信息
    /// </summary>
    /// <param name="guildId">公会的ID</param>
    /// <returns>公会数据</returns>
    public async Task<GuildData> GetGuildData(string guildId)
    {
        return await FindOneAsync(_guildCollection,
            Builders<GuildData>.Filter.Eq(g => g.guildId, guildId),
            "获取公会数据");
    }

    /// <summary>
    /// 根据公会名称获取公会数据
    /// 用于通过公会名称查找公会信息
    /// </summary>
    /// <param name="guildName">公会名称</param>
    /// <returns>公会数据</returns>
    public async Task<GuildData> GetGuildDataWithName(string guildName)
    {
        return await FindOneAsync(_guildCollection,
            Builders<GuildData>.Filter.Eq(g => g.guildName, guildName),
            "根据名称获取公会数据");
    }

    /// <summary>
    /// 获取所有公会数据
    /// 用于显示公会列表或进行公会相关统计
    /// </summary>
    /// <returns>所有公会数据的列表</returns>
    public async Task<List<GuildData>> GetAllGuilds()
    {
        return await FindListAsync(_guildCollection,
            Builders<GuildData>.Filter.Empty,
            "获取所有公会数据");
    }

    /// <summary>
    /// 根据服务器ID获取公会数据
    /// 用于获取特定服务器上的所有公会
    /// </summary>
    /// <param name="serverId">服务器ID</param>
    /// <returns>指定服务器上的所有公会数据列表</returns>
    public async Task<List<GuildData>> GetGuildsByServerId(int serverId)
    {
        return await FindListAsync(_guildCollection,
            Builders<GuildData>.Filter.Eq(g => g.serverId, serverId),
            "获取服务器公会数据");
    }

    /// <summary>
    /// 检查指定服务器上是否有重复的公会名
    /// </summary>
    /// <param name="guildName">要检查的公会名</param>
    /// <param name="serverId">服务器ID</param>
    /// <returns>公会名是否已存在</returns>
    public async Task<bool> IsGuildNameExistsOnServer(string guildName, int serverId)
    {
        var filter = Builders<GuildData>.Filter.And(
            Builders<GuildData>.Filter.Eq(g => g.guildName, guildName),
            Builders<GuildData>.Filter.Eq(g => g.serverId, serverId)
        );
        return await ExistsAsync(_guildCollection, filter, "检查公会名");
    }

    /// <summary>
    /// 从公会中移除指定角色
    /// </summary>
    /// <param name="guildId">公会ID</param>
    /// <param name="characterName">角色名</param>
    /// <returns>操作是否成功</returns>
    public async Task<bool> RemoveMemberFromGuild(string guildId, string characterName)
    {
        if (!await EnsureCollection(_guildCollection, "移除成员"))
            return false;

        try
        {
            // 查找公会
            var filter = Builders<GuildData>.Filter.Eq(g => g.guildId, guildId);
            var guild = await _guildCollection.Find(filter).FirstOrDefaultAsync();

            if (guild == null)
            {
                Debug.LogWarning($"未找到ID为 {guildId} 的公会");
                return false;
            }

            // 检查角色是否在公会中
            var member = guild.members.Find(m => m.characterName == characterName);
            if (member == null)
            {
                Debug.LogWarning($"角色 {characterName} 不在公会中");
                return false;
            }

            // 从成员列表中移除指定角色
            guild.members.RemoveAll(m => m.characterName == characterName);

            // 更新公会会长信息（如果被移除的是会长）
            if (guild.leaderCharacterName == characterName)
            {
                if (guild.members.Count > 0)
                {
                    // 将会长职位转移给第一个成员
                    guild.leaderCharacterName = guild.members[0].characterName;
                    guild.members[0].rank = GuildMemberRank.Leader;
                    Debug.Log($"公会会长已转移给 {guild.members[0].characterName}");
                }
                else
                {
                    // 如果没有成员了，删除公会
                    await _guildCollection.DeleteOneAsync(filter);
                    Debug.Log($"公会已解散，因为没有成员了");
                    return true;
                }
            }

            // 保存更新后的公会数据
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

    #region 物品数据操作

    /// <summary>
    /// 保存玩家物品数据 (采用Upsert模式，高性能且原子)。
    /// </summary>
    public async Task<bool> SavePlayerInventoryDataAsync(PlayerInventoryData inventoryData)
    {
        return await UpsertAsync(_inventoryCollection,
            Builders<PlayerInventoryData>.Filter.Eq(i => i.characterId, inventoryData.characterId),
            inventoryData, "保存玩家物品数据");
    }

    /// <summary>
    /// 根据角色ID获取玩家物品数据
    /// </summary>
    /// <param name="characterId">角色的唯一ID</param>
    /// <returns>玩家物品数据</returns>
    public async Task<PlayerInventoryData> GetPlayerInventoryDataAsync(string characterId)
    {
        return await FindOneAsync(_inventoryCollection,
            Builders<PlayerInventoryData>.Filter.Eq(i => i.characterId, characterId),
            "获取玩家物品数据");
    }

    /// <summary>
    /// 创建新的玩家物品数据记录
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>新创建的玩家物品数据</returns>
    public async Task<PlayerInventoryData> CreatePlayerInventoryDataAsync(string characterId)
    {
        if (!await EnsureCollection(_inventoryCollection, "创建玩家物品数据"))
            return null;

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

    #region 任务进度数据操作

    /// <summary>
    /// 保存角色任务进度数据 (采用Upsert模式，高性能且原子)。
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="taskDataJson">任务进度数据的JSON字符串</param>
    /// <returns>操作是否成功</returns>
    public async Task<bool> SaveTaskProgressDataAsync(string characterId, string taskDataJson)
    {
        var taskProgressData = new TaskProgressData(characterId, taskDataJson);
        return await UpsertAsync(_taskProgressCollection,
            Builders<TaskProgressData>.Filter.Eq(t => t.characterId, characterId),
            taskProgressData, "保存角色任务进度数据");
    }

    /// <summary>
    /// 根据角色ID获取任务进度数据
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>任务进度数据</returns>
    public async Task<TaskProgressData> GetTaskProgressDataAsync(string characterId)
    {
        return await FindOneAsync(_taskProgressCollection,
            Builders<TaskProgressData>.Filter.Eq(t => t.characterId, characterId),
            "获取角色任务进度数据");
    }

    /// <summary>
    /// 删除角色任务进度数据
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>操作是否成功</returns>
    public async Task<bool> DeleteTaskProgressDataAsync(string characterId)
    {
        return await DeleteOneAsync(_taskProgressCollection,
            Builders<TaskProgressData>.Filter.Eq(t => t.characterId, characterId),
            "删除角色任务进度数据");
    }

    #endregion

    #region 连接测试方法

    /// <summary>
    /// 测试MongoDB连接
    /// 用于验证MongoDB服务器是否可以正常连接
    /// </summary>
    /// <returns>连接是否成功</returns>
    public async Task<bool> TestConnection()
    {
        if (!await EnsureCollection(_client, "测试连接"))
            return false;

        try
        {
            // 尝试列出所有数据库名称来测试连接
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
