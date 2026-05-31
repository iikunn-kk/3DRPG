using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameDemo.Models;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// 本地 JSON 文件存储实现。
/// MongoDB 不可用时自动降级，数据保存在 persistentDataPath/LocalData/ 下。
/// 线程安全，密码使用与 MongoDB 模式一致的加盐哈希。
/// </summary>
public class LocalJsonDataStore : IDataStore
{
    private readonly string _basePath;
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    private readonly JsonSerializerSettings _jsonSettings;

    public bool IsAvailable => true; // 本地存储始终可用

    public LocalJsonDataStore()
    {
        _basePath = Path.Combine(Application.persistentDataPath, "LocalData");
        Directory.CreateDirectory(_basePath);
        _jsonSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };
        Debug.Log($"[LocalJsonDataStore] 数据目录: {_basePath}");
    }

    #region 通用辅助

    private string GetPath(string fileName) => Path.Combine(_basePath, fileName);

    private List<T> LoadList<T>(string fileName)
    {
        var path = GetPath(fileName);
        if (!File.Exists(path)) return new List<T>();
        try
        {
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<List<T>>(json, _jsonSettings) ?? new List<T>();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocalJson] 读取 {fileName} 失败: {ex.Message}");
            return new List<T>();
        }
    }

    private void SaveList<T>(string fileName, List<T> data)
    {
        var path = GetPath(fileName);
        try
        {
            var json = JsonConvert.SerializeObject(data, Formatting.Indented, _jsonSettings);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocalJson] 保存 {fileName} 失败: {ex.Message}");
        }
    }

    private Task<List<T>> LoadListAsync<T>(string fileName)
    {
        return Task.Run(() =>
        {
            _lock.Wait();
            try { return LoadList<T>(fileName); }
            finally { _lock.Release(); }
        });
    }

    private Task SaveListAsync<T>(string fileName, List<T> data)
    {
        return Task.Run(() =>
        {
            _lock.Wait();
            try { SaveList(fileName, data); }
            finally { _lock.Release(); }
        });
    }

    #endregion

    #region 连接

    public Task<bool> TestConnectionAsync() => Task.FromResult(true);

    #endregion

    #region 玩家账户

    public async Task<RegistrationResult> CreatePlayerAccountAsync(string username, string password)
    {
        var players = await LoadListAsync<PlayerLoginData>("players.json");
        if (players.Any(p => p.username == username))
            return RegistrationResult.UsernameExists;

        PasswordHelper.CreatePasswordHash(password, out var hash, out var salt);
        players.Add(new PlayerLoginData(username, hash, salt));
        await SaveListAsync("players.json", players);
        return RegistrationResult.Success;
    }

    public async Task<PlayerLoginData> AuthenticatePlayerAsync(string username, string password)
    {
        var players = await LoadListAsync<PlayerLoginData>("players.json");
        var player = players.FirstOrDefault(p => p.username == username);
        if (player != null && PasswordHelper.VerifyPasswordHash(password, player.passwordHash, player.passwordSalt))
            return player;
        return null;
    }

    public async Task<bool> IsUsernameExistsAsync(string username)
    {
        var players = await LoadListAsync<PlayerLoginData>("players.json");
        return players.Any(p => p.username == username);
    }

    public async Task<bool> ChangePlayerPasswordAsync(string username, string oldPassword, string newPassword)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword))
            return false;

        var players = await LoadListAsync<PlayerLoginData>("players.json");
        var player = players.FirstOrDefault(p => p.username == username);
        if (player == null || !PasswordHelper.VerifyPasswordHash(oldPassword, player.passwordHash, player.passwordSalt))
            return false;

        PasswordHelper.CreatePasswordHash(newPassword, out var newHash, out var newSalt);
        player.passwordHash = newHash;
        player.passwordSalt = newSalt;
        await SaveListAsync("players.json", players);
        return true;
    }

    #endregion

    #region 角色数据

    public async Task<bool> CreateAndSaveCharacterData(CharacterData characterData)
    {
        var chars = await LoadListAsync<CharacterData>("characters.json");
        var idx = chars.FindIndex(c => c.Id == characterData.Id);
        if (idx >= 0) chars[idx] = characterData;
        else chars.Add(characterData);
        await SaveListAsync("characters.json", chars);
        return true;
    }

    public async Task<List<CharacterData>> GetCharactersByPlayerUID(string playerUid)
    {
        var chars = await LoadListAsync<CharacterData>("characters.json");
        return chars.Where(c => c.playerUid == playerUid).ToList();
    }

    public async Task<List<CharacterData>> GetCharactersByPlayerUIDAndServer(string playerUid, int serverId)
    {
        var chars = await LoadListAsync<CharacterData>("characters.json");
        return chars.Where(c => c.playerUid == playerUid && c.serverId == serverId).ToList();
    }

    public async Task<CharacterData> GetCharacterData(string characterId)
    {
        var chars = await LoadListAsync<CharacterData>("characters.json");
        return chars.FirstOrDefault(c => c.Id == characterId);
    }

    public async Task<bool> DeleteCharacterData(string characterId)
    {
        var chars = await LoadListAsync<CharacterData>("characters.json");
        var removed = chars.RemoveAll(c => c.Id == characterId);
        if (removed > 0)
        {
            await SaveListAsync("characters.json", chars);
            return true;
        }
        return false;
    }

    public async Task<bool> IsCharacterNameExistsOnServer(string characterName, int serverId)
    {
        var chars = await LoadListAsync<CharacterData>("characters.json");
        return chars.Any(c => c.characterName == characterName && c.serverId == serverId);
    }

    #endregion

    #region 公会数据

    public async Task<bool> SaveGuildDataAsync(GuildData guildData)
    {
        var guilds = await LoadListAsync<GuildData>("guilds.json");
        var idx = guilds.FindIndex(g => g.guildId == guildData.guildId);
        if (idx >= 0) guilds[idx] = guildData;
        else guilds.Add(guildData);
        await SaveListAsync("guilds.json", guilds);
        return true;
    }

    public async Task<GuildData> GetGuildData(string guildId)
    {
        var guilds = await LoadListAsync<GuildData>("guilds.json");
        return guilds.FirstOrDefault(g => g.guildId == guildId);
    }

    public async Task<GuildData> GetGuildDataWithName(string guildName)
    {
        var guilds = await LoadListAsync<GuildData>("guilds.json");
        return guilds.FirstOrDefault(g => g.guildName == guildName);
    }

    public async Task<List<GuildData>> GetAllGuilds()
    {
        return await LoadListAsync<GuildData>("guilds.json");
    }

    public async Task<List<GuildData>> GetGuildsByServerId(int serverId)
    {
        var guilds = await LoadListAsync<GuildData>("guilds.json");
        return guilds.Where(g => g.serverId == serverId).ToList();
    }

    public async Task<bool> IsGuildNameExistsOnServer(string guildName, int serverId)
    {
        var guilds = await LoadListAsync<GuildData>("guilds.json");
        return guilds.Any(g => g.guildName == guildName && g.serverId == serverId);
    }

    public async Task<bool> RemoveMemberFromGuild(string guildId, string characterName)
    {
        var guilds = await LoadListAsync<GuildData>("guilds.json");
        var guild = guilds.FirstOrDefault(g => g.guildId == guildId);
        if (guild == null) return false;

        var member = guild.members.Find(m => m.characterName == characterName);
        if (member == null) return false;

        guild.members.RemoveAll(m => m.characterName == characterName);
        if (guild.leaderCharacterName == characterName)
        {
            if (guild.members.Count > 0)
            {
                guild.leaderCharacterName = guild.members[0].characterName;
                guild.members[0].rank = GuildMemberRank.Leader;
            }
            else
            {
                guilds.Remove(guild);
                await SaveListAsync("guilds.json", guilds);
                return true;
            }
        }
        await SaveListAsync("guilds.json", guilds);
        return true;
    }

    #endregion

    #region 背包数据

    public async Task<bool> SavePlayerInventoryDataAsync(PlayerInventoryData inventoryData)
    {
        var inventories = await LoadListAsync<PlayerInventoryData>("inventories.json");
        var idx = inventories.FindIndex(i => i.characterId == inventoryData.characterId);
        if (idx >= 0) inventories[idx] = inventoryData;
        else inventories.Add(inventoryData);
        await SaveListAsync("inventories.json", inventories);
        return true;
    }

    public async Task<PlayerInventoryData> GetPlayerInventoryDataAsync(string characterId)
    {
        var inventories = await LoadListAsync<PlayerInventoryData>("inventories.json");
        return inventories.FirstOrDefault(i => i.characterId == characterId);
    }

    public async Task<PlayerInventoryData> CreatePlayerInventoryDataAsync(string characterId)
    {
        var newInventory = new PlayerInventoryData(characterId);
        await SavePlayerInventoryDataAsync(newInventory);
        return newInventory;
    }

    #endregion

    #region 任务进度

    public async Task<bool> SaveTaskProgressDataAsync(string characterId, string taskDataJson)
    {
        var tasks = await LoadListAsync<TaskProgressData>("taskProgress.json");
        var idx = tasks.FindIndex(t => t.characterId == characterId);
        var data = new TaskProgressData(characterId, taskDataJson);
        if (idx >= 0) tasks[idx] = data;
        else tasks.Add(data);
        await SaveListAsync("taskProgress.json", tasks);
        return true;
    }

    public async Task<TaskProgressData> GetTaskProgressDataAsync(string characterId)
    {
        var tasks = await LoadListAsync<TaskProgressData>("taskProgress.json");
        return tasks.FirstOrDefault(t => t.characterId == characterId);
    }

    public async Task<bool> DeleteTaskProgressDataAsync(string characterId)
    {
        var tasks = await LoadListAsync<TaskProgressData>("taskProgress.json");
        var removed = tasks.RemoveAll(t => t.characterId == characterId);
        if (removed > 0)
        {
            await SaveListAsync("taskProgress.json", tasks);
            return true;
        }
        return false;
    }

    #endregion
}
