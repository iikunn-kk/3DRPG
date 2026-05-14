using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Random = UnityEngine.Random;

public class GuildMemberPanel : MonoBehaviour
{
    [Header("成员列表")]
    [SerializeField] private Transform prefabsParent;
    [SerializeField] private GameObject guildMemberPrefab;

    [Header("弹窗引用")]
    [SerializeField] private SetGuildFunctionPanel setGuildFunctionPanel;
    [SerializeField] private ConfirmOutGuildPanel confirmOutGuildPanel;

    private GuildData currentGuildData;
    private List<GuildMemberInfo> sortedMembers;

    // 父面板引用，用于在成员变更后通知刷新详情面板
    private AlreadyHaveGuildPanel alreadyHaveGuildPanel;

    // 测试功能相关字段
    [Header("测试功能设置")]
    [SerializeField] private int minLevel = 1;
    [SerializeField] private int maxLevel = 100;
    [SerializeField]
    private string[] testPlayerNames = {
        "嘉然的骑士", "妮可的信徒", "流萤的伙伴", "浮波柚叶的朋友",
        "勇敢的冒险者", "无畏的战士", "智慧的法师", "敏捷的游侠",
        "神圣的牧师", "暗影的刺客", "钢铁的守护者", "元素的掌控者",
        "幸运的寻宝者", "技艺精湛的工匠", "经验丰富的猎人", "博学的学者"
    };

    public void Init(GuildData data)
    {
        currentGuildData = data;
        DisplayMembers(data.members);
    }

    public void SetAlreadyHaveGuildPanel(AlreadyHaveGuildPanel panel)
    {
        alreadyHaveGuildPanel = panel;
    }

    /// <summary>
    /// 显示成员列表
    /// </summary>
    /// <param name="members">要显示的成员列表</param>
    private void DisplayMembers(List<GuildMemberInfo> members)
    {
        // 清除现有的成员显示
        if (prefabsParent != null)
        {
            foreach (Transform child in prefabsParent)
            {
                Destroy(child.gameObject);
            }
        }

        CharacterData currentCharacter = SessionManager.Instance.CurrentCharacter;
        string selfUid = currentCharacter != null ? currentCharacter.playerUid : null;
        string selfCharacterName = currentCharacter != null ? currentCharacter.characterName : null;
        GuildMemberInfo selfMemberInfo = null;
        if (currentGuildData != null && currentGuildData.members != null && currentCharacter != null)
            selfMemberInfo = currentGuildData.members.FirstOrDefault(m => m.playerUid == currentCharacter.playerUid);
        GuildMemberRank selfRank = selfMemberInfo != null ? selfMemberInfo.rank : GuildMemberRank.Member;

        // 创建成员列表
        if (members != null && guildMemberPrefab != null && prefabsParent != null)
        {
            foreach (var member in members)
            {
                GameObject memberObj = Instantiate(guildMemberPrefab, prefabsParent);
                GuildPlayerInformation playerInfo = memberObj.GetComponent<GuildPlayerInformation>();
                if (playerInfo != null)
                {
                    bool isSelf = (member.playerUid == selfUid);

                    // 设置按钮显示逻辑
                    bool showSetFunction = false;
                    bool showKick = false;

                    // 设置权限按钮：只有会长可以，且不能对会长本身显示
                    if (!isSelf && selfRank == GuildMemberRank.Leader && member.rank != GuildMemberRank.Leader)
                        showSetFunction = true;

                    // 踢人按钮逻辑
                    if (!isSelf)
                    {
                        if (selfRank == GuildMemberRank.Leader)
                        {
                            // 会长可以踢除除自己外所有人（不可能到这里是自己）
                            showKick = true;
                        }
                        else if (selfRank == GuildMemberRank.ViceLeader)
                        {
                            // 副会长只能踢普通成员
                            if (member.rank == GuildMemberRank.Member)
                                showKick = true;
                        }
                    }

                    playerInfo.Init(member,
                        OnClickSetPermission,
                        OnClickKickMember,
                        showSetGuildFunctionButton: showSetFunction,
                        showConfirmOutGuildButton: showKick);
                }
            }
        }
    }

    private void OnClickSetPermission(GuildMemberInfo target)
    {
        if (setGuildFunctionPanel == null) return;
        // 只有会长能设置且不能修改会长职位
        GuildMemberInfo self = currentGuildData.members.FirstOrDefault(m => m.playerUid == SessionManager.Instance.CurrentCharacter.playerUid);
        if (self == null || self.rank != GuildMemberRank.Leader) return;
        if (target.rank == GuildMemberRank.Leader) return;
        setGuildFunctionPanel.Init(target, newRank => { _ = ChangeMemberRankAsync(target, newRank); });
    }

    private void OnClickKickMember(GuildMemberInfo target)
    {
        if (confirmOutGuildPanel == null) return;
        confirmOutGuildPanel.Init(target, () => { _ = KickMemberAsync(target); });
    }

    private async Task ChangeMemberRankAsync(GuildMemberInfo target, GuildMemberRank newRank)
    {
        if (target == null) return;
        if (target.rank == newRank) return;
        // 再次权限验证
        GuildMemberInfo self = currentGuildData.members.FirstOrDefault(m => m.playerUid == SessionManager.Instance.CurrentCharacter.playerUid);
        if (self == null || self.rank != GuildMemberRank.Leader) return;
        if (target.rank == GuildMemberRank.Leader) return;

        target.rank = newRank;
        bool save = await MongoDBManager.Instance.SaveGuildDataAsync(currentGuildData);
        if (!save)
        {
            Debug.LogError("保存公会数据失败(修改职位)");
            return;
        }
        // 刷新显示
        SortByRank();
        // 通知父面板刷新详情（如成员数量/公告等）
        alreadyHaveGuildPanel?.UpdateGuildData(currentGuildData);
    }

    private async Task KickMemberAsync(GuildMemberInfo target)
    {
        if (target == null) return;
        CharacterData selfChar = SessionManager.Instance.CurrentCharacter;
        var self = currentGuildData.members.FirstOrDefault(m => m.playerUid == selfChar.playerUid);
        if (self == null) return;

        // 权限检查
        bool canKick = false;
        if (self.rank == GuildMemberRank.Leader && target.playerUid != self.playerUid)
            canKick = true;
        else if (self.rank == GuildMemberRank.ViceLeader && target.rank == GuildMemberRank.Member)
            canKick = true;

        if (!canKick) return;

        currentGuildData.members.RemoveAll(m => m.playerUid == target.playerUid);
        bool save = await MongoDBManager.Instance.SaveGuildDataAsync(currentGuildData);
        if (!save)
        {
            Debug.LogError("保存公会数据失败(踢出成员)");
            return;
        }
        // 清空被踢成员角色数据中的 guildId
        try
        {
            var targetCharacter = await MongoDBManager.Instance.GetCharacterData(target.characterId);
            if (targetCharacter != null)
            {
                targetCharacter.guildId = string.Empty;
                await MongoDBManager.Instance.CreateAndSaveCharacterData(targetCharacter);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("更新被踢角色数据失败: " + e.Message);
        }
        // 刷新
        SortByRank();
        // 通知父面板刷新详情
        alreadyHaveGuildPanel?.UpdateGuildData(currentGuildData);
    }

    /// <summary>
    /// 按公会职级排序（会长 > 副会长 > 干事 > 普通成员）
    /// </summary>
    public void SortByRank()
    {
        if (currentGuildData?.members == null) return;
        sortedMembers = new List<GuildMemberInfo>(currentGuildData.members);
        sortedMembers.Sort((a, b) => b.rank.CompareTo(a.rank));
        DisplayMembers(sortedMembers);
    }

    /// <summary>
    /// 按等级排序（从高到低）
    /// </summary>
    public void SortByLevel()
    {
        if (currentGuildData?.members == null) return;
        sortedMembers = new List<GuildMemberInfo>(currentGuildData.members);
        sortedMembers.Sort((a, b) => b.level.CompareTo(a.level));
        DisplayMembers(sortedMembers);
    }

    /// <summary>
    /// 按职业排序
    /// </summary>
    public void SortByProfession()
    {
        if (currentGuildData?.members == null) return;
        sortedMembers = new List<GuildMemberInfo>(currentGuildData.members);
        sortedMembers.Sort((a, b) => a.profession.CompareTo(b.profession));
        DisplayMembers(sortedMembers);
    }

    /// <summary>
    /// 按最后登录时间排序（最近登录的在前）
    /// </summary>
    public void SortByLastOnlineTime()
    {
        if (currentGuildData?.members == null) return;
        sortedMembers = new List<GuildMemberInfo>(currentGuildData.members);
        sortedMembers.Sort((a, b) => b.lastOnlineTime.CompareTo(a.lastOnlineTime));
        DisplayMembers(sortedMembers);
    }

    /// <summary>
    /// 测试功能：向当前公会添加随机生成的成员
    /// </summary>
    /// <param name="count">要添加的成员数量</param>
    public async UniTaskVoid AddRandomMembersToGuild(int count)
    {
        if (currentGuildData == null)
        {
            Debug.LogError("当前公会数据为空，无法添加测试成员");
            return;
        }

        int successCount = 0;
        try
        {
            for (int i = 0; i < count; i++)
            {
                // 生成随机角色数据
                CharacterData randomCharacter = GenerateRandomCharacterData(currentGuildData.serverId);

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

                // 添加到公会成员列表
                currentGuildData.members.Add(newMember);
                successCount++;
                Debug.Log($"成功生成测试成员: {randomCharacter.characterName}, 职业: {randomCharacter.profession}, 等级: {randomCharacter.level}");
            }

            // 保存更新后的公会数据
            bool guildSaveSuccess = await MongoDBManager.Instance.SaveGuildDataAsync(currentGuildData);
            if (guildSaveSuccess)
            {
                Debug.Log($"成功向公会 {currentGuildData.guildName} 添加了 {successCount} 个随机成员");
                // 刷新成员列表显示
                Init(currentGuildData);
                SortByRank(); // 默认按职级排序
                // 通知父面板更新详情（例如成员数）
                alreadyHaveGuildPanel?.UpdateGuildData(currentGuildData);
            }
            else
            {
                Debug.LogError("保存公会数据失败");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"添加随机成员时发生异常: {ex.Message}");
        }
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

