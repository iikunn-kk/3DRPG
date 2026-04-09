using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 公会测试辅助类，用于向公会添加随机生成的成员进行测试
/// </summary>
public class GuildTestHelper : MonoBehaviour
{
    [Header("测试设置")]
    [SerializeField] private int minLevel = 1;
    [SerializeField] private int maxLevel = 100;
    [SerializeField] private string[] testPlayerNames = {
        "嘉然的骑士", "妮可的信徒", "流萤的伙伴", "浮波柚叶的朋友",
        "勇敢的冒险者", "无畏的战士", "智慧的法师", "敏捷的游侠",
        "神圣的牧师", "暗影的刺客", "钢铁的守护者", "元素的掌控者",
        "幸运的寻宝者", "技艺精湛的工匠", "经验丰富的猎人", "博学的学者"
    };

    /// <summary>
    /// 向指定公会添加随机生成的成员
    /// </summary>
    /// <param name="guildId">公会ID</param>
    /// <param name="count">要添加的成员数量</param>
    /// <returns>添加成功的成员数量</returns>
    public async Task<int> AddRandomMembersToGuild(string guildId, int count)
    {
        if (string.IsNullOrEmpty(guildId))
        {
            Debug.LogError("公会ID不能为空");
            return 0;
        }

        if (count <= 0)
        {
            Debug.LogWarning("添加成员数量必须大于0");
            return 0;
        }

        GuildData guildData = await MongoDBManager.Instance.GetGuildData(guildId);
        if (guildData == null)
        {
            Debug.LogError($"找不到ID为 {guildId} 的公会");
            return 0;
        }

        int successCount = 0;
        List<string> addedCharacters = new List<string>();

        try
        {
            for (int i = 0; i < count; i++)
            {
                // 生成随机角色数据
                CharacterData randomCharacter = GenerateRandomCharacterData(guildData.serverId);
                
                // 确保角色名不重复
                int attempt = 0;
                while (addedCharacters.Contains(randomCharacter.characterName) || 
                       await MongoDBManager.Instance.IsCharacterNameExistsOnServer(randomCharacter.characterName, guildData.serverId))
                {
                    randomCharacter.characterName = GenerateRandomCharacterName() + "_" + UnityEngine.Random.Range(100, 999);
                    attempt++;
                    
                    // 防止无限循环
                    if (attempt > 50)
                    {
                        Debug.LogWarning("无法生成唯一角色名，跳过当前成员");
                        break;
                    }
                }
                
                if (attempt > 50) continue;
                
                addedCharacters.Add(randomCharacter.characterName);

                // 保存角色数据到数据库
                bool characterSaveSuccess = await MongoDBManager.Instance.CreateAndSaveCharacterData(randomCharacter);
                if (!characterSaveSuccess)
                {
                    Debug.LogError($"保存角色 {randomCharacter.characterName} 数据失败");
                    continue;
                }

                // 创建公会成员信息
                GuildMemberInfo newMember = new GuildMemberInfo
                {
                    Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
                    playerUid = randomCharacter.playerUid,
                    characterName = randomCharacter.characterName,
                    characterId = randomCharacter.Id,
                    level = randomCharacter.level,
                    iconID = randomCharacter.iconID,
                    profession = randomCharacter.profession,
                    rank = GuildMemberRank.Member, // 默认为普通成员
                    joinTime = DateTime.Now.Ticks,
                    lastOnlineTime = DateTime.Now.Ticks
                };

                // 检查成员是否已在公会中
                bool isMember = guildData.members.Exists(m => m.characterName == randomCharacter.characterName);
                if (isMember)
                {
                    Debug.LogWarning($"角色 {randomCharacter.characterName} 已经是该公会成员");
                    continue;
                }

                // 添加到公会成员列表
                guildData.members.Add(newMember);
                successCount++;
                Debug.Log($"成功生成测试成员: {randomCharacter.characterName}, 职业: {randomCharacter.profession}, 等级: {randomCharacter.level}");
            }

            // 保存更新后的公会数据
            bool guildSaveSuccess = await MongoDBManager.Instance.SaveGuildDataAsync(guildData);
            if (guildSaveSuccess)
            {
                Debug.Log($"成功向公会 {guildData.guildName} 添加了 {successCount} 个随机成员");
            }
            else
            {
                Debug.LogError("保存公会数据失败");
                return 0;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"添加随机成员时发生异常: {ex.Message}");
            return 0;
        }

        return successCount;
    }

    /// <summary>
    /// 生成随机角色数据
    /// </summary>
    /// <param name="serverId">服务器ID</param>
    /// <returns>随机生成的角色数据</returns>
    private CharacterData GenerateRandomCharacterData(int serverId)
    {
        // 生成随机玩家UID（测试用）
        string playerUid = "TestPlayer_" + UnityEngine.Random.Range(10000, 99999);
        
        // 生成随机角色名
        string characterName = GenerateRandomCharacterName();
        
        // 随机职业
        CharacterProfession profession = (CharacterProfession)UnityEngine.Random.Range(0, Enum.GetValues(typeof(CharacterProfession)).Length);
        
        // 随机等级
        int level = UnityEngine.Random.Range(minLevel, maxLevel + 1);
        
        // 创建角色数据
        CharacterData characterData = new CharacterData(playerUid, serverId, characterName, profession)
        {
            level = level,
            exp = level * 100, // 简单的经验值计算
            gold = UnityEngine.Random.Range(100, 10000),
            gem = UnityEngine.Random.Range(0, 500),
            iconID = UnityEngine.Random.Range(0, 10),
            currentScene = "TestScene"
        };

        return characterData;
    }

    /// <summary>
    /// 生成随机角色名
    /// </summary>
    /// <returns>随机角色名</returns>
    private string GenerateRandomCharacterName()
    {
        string prefix = testPlayerNames[UnityEngine.Random.Range(0, testPlayerNames.Length)];
        string suffix = UnityEngine.Random.Range(1000, 9999).ToString();
        return prefix + "_" + suffix;
    }
}