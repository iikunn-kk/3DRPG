using System;
using System.Threading.Tasks;
using UnityEngine;
using MongoDB.Bson;

/// <summary>
/// 公会管理器，专门负责处理公会相关操作
/// </summary>
public class GuildManager : Singleton<GuildManager>
{
    /// <summary>
    /// 创建公会
    /// </summary>
    /// <param name="guildName">公会名称</param>
    /// <param name="guildDescription">公会描述</param>
    /// <returns>创建公会操作是否成功</returns>
    public async Task<bool> CreateGuild(string guildName, string guildDescription)
    {
        CharacterData currentCharacter = SessionManager.Instance.CurrentCharacter;

        if (currentCharacter == null)
        {
            Debug.LogWarning("当前没有选择角色");
            return false;
        }

        if (string.IsNullOrEmpty(guildName))
        {
            Debug.LogWarning("公会名称不能为空");
            return false;
        }

        try
        {
            // 检查同名公会是否已存在（在同一个服务器上）
            bool guildExists = await MongoDBManager.Instance.IsGuildNameExistsOnServer(guildName, currentCharacter.serverId);
            if (guildExists)
            {
                Debug.LogWarning($"公会名称 '{guildName}' 已存在");
                return false;
            }

            // 创建新公会
            GuildData newGuild = new GuildData();
            newGuild.guildName = guildName;
            newGuild.guildDescription = guildDescription;
            newGuild.serverId = currentCharacter.serverId;
            newGuild.leaderUid = currentCharacter.playerUid;
            newGuild.leaderCharacterName = currentCharacter.characterName;

            // 保存公会数据
            bool saveSuccess = await MongoDBManager.Instance.SaveGuildDataAsync(newGuild);

            if (saveSuccess)
            {
                // 更新角色数据，设置公会ID
                CharacterData characterDataToSave = SessionManager.Instance.CurrentCharacter;

                if (characterDataToSave != null)
                {
                    characterDataToSave.guildId = newGuild.guildId;

                    // 保存角色数据到数据库
                    bool characterSaveSuccess = await MongoDBManager.Instance.CreateAndSaveCharacterData(characterDataToSave);

                    if (characterSaveSuccess)
                    {
                        // 添加创建者为会长
                        GuildMemberInfo memberInfo = new GuildMemberInfo
                        {
                            Id = ObjectId.GenerateNewId().ToString(),
                            playerUid = characterDataToSave.playerUid,
                            characterName = characterDataToSave.characterName,
                            characterId = characterDataToSave.Id,
                            level = characterDataToSave.level,
                            profession = characterDataToSave.profession,
                            rank = GuildMemberRank.Leader,
                            joinTime = DateTime.Now.Ticks,
                            lastOnlineTime = DateTime.Now.Ticks
                        };

                        newGuild.members.Add(memberInfo);

                        // 再次保存公会数据（添加成员后）
                        bool finalSaveSuccess = await MongoDBManager.Instance.SaveGuildDataAsync(newGuild);

                        if (finalSaveSuccess)
                        {
                            Debug.Log($"成功创建公会 '{guildName}'，角色 {currentCharacter.characterName} 成为会长");
                            // IMPORTANT: Ensure GameManager's CurrentCharacter is updated so UI logic
                            // that checks SessionManager.Instance.CurrentCharacter.guildId can see the new guild.
                            SessionManager.Instance.SetCurrentCharacterData(characterDataToSave);
                            return true;
                        }
                        else
                        {
                            Debug.LogError("保存公会成员信息失败");
                            return false;
                        }
                    }
                    else
                    {
                        Debug.LogError("保存角色数据失败");
                        return false;
                    }
                }
                else
                {
                    Debug.LogError("无法获取角色数据进行保存");
                    return false;
                }
            }
            else
            {
                Debug.LogError("保存公会数据失败");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"创建公会时发生异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 加入公会
    /// </summary>
    /// <param name="guildId">要加入的公会ID</param>
    /// <returns>加入公会操作是否成功</returns>
    public async Task<bool> JoinGuild(string guildId)
    {
        CharacterData currentCharacter = SessionManager.Instance.CurrentCharacter;

        if (currentCharacter == null)
        {
            Debug.LogWarning("当前没有选择角色");
            return false;
        }

        // 检查角色是否已经加入了公会
        if (!string.IsNullOrEmpty(currentCharacter.guildId))
        {
            Debug.LogWarning($"角色 {currentCharacter.characterName} 已经是公会成员");
            return false;
        }

        try
        {
            // 获取要加入的公会数据
            GuildData guildData = await MongoDBManager.Instance.GetGuildData(guildId);
            if (guildData == null)
            {
                Debug.LogWarning($"找不到ID为 {guildId} 的公会");
                return false;
            }

            // 检查角色是否已经在该公会中
            bool isMember = guildData.members.Exists(m => m.characterName == currentCharacter.characterName);
            if (isMember)
            {
                Debug.LogWarning($"角色 {currentCharacter.characterName} 已经是该公会成员");
                return false;
            }

            // 创建新成员信息
            GuildMemberInfo newMember = new GuildMemberInfo
            {
                Id = ObjectId.GenerateNewId().ToString(),
                playerUid = currentCharacter.playerUid,
                characterName = currentCharacter.characterName,
                characterId = currentCharacter.Id,
                level = currentCharacter.level,
                iconID = currentCharacter.iconID,
                profession = currentCharacter.profession,
                rank = GuildMemberRank.Member, // 默认普通成员
                joinTime = DateTime.Now.Ticks,
                lastOnlineTime = DateTime.Now.Ticks
            };

            // 添加到公会成员列表
            guildData.members.Add(newMember);

            // 保存更新后的公会数据
            bool guildSaveSuccess = await MongoDBManager.Instance.SaveGuildDataAsync(guildData);

            if (guildSaveSuccess)
            {
                // 更新角色数据，设置公会ID
                CharacterData characterDataToSave = SessionManager.Instance.CurrentCharacter;

                if (characterDataToSave != null)
                {
                    characterDataToSave.guildId = guildId;

                    // 保存角色数据到数据库
                    bool characterSaveSuccess = await MongoDBManager.Instance.CreateAndSaveCharacterData(characterDataToSave);

                    if (characterSaveSuccess)
                    {
                        // 同步更新当前角色数据
                        SessionManager.Instance.SetCurrentCharacterData(characterDataToSave);
                        Debug.Log($"角色 {currentCharacter.characterName} 成功加入公会 {guildData.guildName}");
                        return true;
                    }
                    else
                    {
                        Debug.LogError("保存角色数据失败");
                        return false;
                    }
                }
                else
                {
                    Debug.LogError("无法获取角色数据进行保存");
                    return false;
                }
            }
            else
            {
                Debug.LogError("保存公会数据失败");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"加入公会时发生异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 退出公会
    /// </summary>
    /// <returns>退出公会操作是否成功</returns>
    public async Task<bool> QuitGuild()
    {
        CharacterData currentCharacter = SessionManager.Instance.CurrentCharacter;

        if (currentCharacter == null)
        {
            Debug.LogWarning("当前没有选择角色");
            return false;
        }

        if (string.IsNullOrEmpty(currentCharacter.guildId))
        {
            Debug.LogWarning("当前角色未加入任何公会");
            return false;
        }

        try
        {
            bool success = await MongoDBManager.Instance.RemoveMemberFromGuild(currentCharacter.guildId, currentCharacter.characterName);

            if (success)
            {
                // 获取最新的角色数据
                CharacterData characterDataToSave = SessionManager.Instance.CurrentCharacter;

                // 确保公会ID被清空
                if (characterDataToSave != null)
                {
                    characterDataToSave.guildId = string.Empty;

                    // 保存角色数据到数据库
                    bool saveSuccess = await MongoDBManager.Instance.CreateAndSaveCharacterData(characterDataToSave);

                    if (saveSuccess)
                    {
                        // 同步更新当前角色数据
                        SessionManager.Instance.SetCurrentCharacterData(characterDataToSave);
                        Debug.Log($"角色 {currentCharacter.characterName} 已退出公会");
                        return true;
                    }
                    else
                    {
                        Debug.LogError("保存角色数据失败");
                        return false;
                    }
                }
                else
                {
                    Debug.LogError("无法获取角色数据进行保存");
                    return false;
                }
            }
            else
            {
                Debug.LogError("从公会移除成员失败");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"退出公会时发生异常: {ex.Message}");
            return false;
        }
    }
}