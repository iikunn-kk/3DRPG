using UnityEngine;
using System.Collections;
using System.Threading.Tasks;
using System;
using Random = UnityEngine.Random;
using MongoDB.Bson; // 用于生成 ObjectId

public class GuildPanel : UIPopPanelBase
{
    [SerializeField] private AlreadyHaveGuildPanel alreadyHaveGuildPanel;
    [SerializeField] private NotHaveGuildPanel notHaveGuildPanel;

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

    [Header("公会测试功能设置")]
    [SerializeField]
    private string[] testGuildNames = {
        "嘉然的守护者", "妮可的信徒团", "流萤战队", "浮波柚叶联盟",
        "星辰公会", "龙之谷联盟", "暗影兄弟会", "光明圣殿骑士团",
        "铁血军团", "翡翠议会", "风暴之眼", "永恒守护者",
        "烈焰之心", "寒冰堡垒", "雷鸣战团", "大地之盾"
    };

    [SerializeField]
    private string[] testGuildDescriptions = {
        "追求力量与荣耀的勇士们聚集于此",
        "热爱和平与正义的守护者联盟",
        "探索未知世界的冒险者组织",
        "技艺精湛的工匠与创造者协会",
        "精英战士组成的战斗团体",
        "研究古老魔法的学者公会",
        "保护弱小的正义使者联盟",
        "追求极限挑战的勇者团队"
    };

    public async void Init(CharacterData data)
    {
        Show();
        // 根据玩家是否有公会决定显示哪个面板
        if (data.guildId != null && data.guildId != "")
        {
            // 玩家有公会，显示已有公会面板
            notHaveGuildPanel.gameObject.SetActive(false);
            alreadyHaveGuildPanel.gameObject.SetActive(true);

            // 从数据库获取公会数据并初始化面板
            var guildData = await MongoDBManager.Instance.GetGuildData(data.guildId);
            if (guildData != null)
            {
                alreadyHaveGuildPanel.Init(guildData, this);
            }
            else
            {
                Debug.LogError("无法获取公会数据，显示默认面板");
            }
        }
        else
        {
            // 玩家没有公会，显示创建/搜索公会面板
            alreadyHaveGuildPanel.gameObject.SetActive(false);
            notHaveGuildPanel.gameObject.SetActive(true);

            // 初始化无公会面板并传入当前面板引用
            notHaveGuildPanel.Init(this);
        }

    }

    /// <summary>
    /// 显示已有公会面板
    /// </summary>
    /// <param name="guildData">公会数据</param>
    public void ShowAlreadyHaveGuildPanel(GuildData guildData)
    {
        notHaveGuildPanel.gameObject.SetActive(false);
        alreadyHaveGuildPanel.gameObject.SetActive(true);
        alreadyHaveGuildPanel.Init(guildData, this);
    }

    public void OnCloseButtonClick()
    {
        UIManager.Instance.ClosePanel<GuildPanel>();
        Hide();
    }

    /// <summary>
    /// 测试功能：向当前公会添加随机生成的成员
    /// </summary>
    /// <param name="count">要添加的成员数量</param>
    public async void AddRandomMembersToGuild(int count)
    {
        if (alreadyHaveGuildPanel == null)
        {
            Debug.LogError("已有公会面板未设置，无法添加测试成员");
            return;
        }

        GuildData currentGuildData = alreadyHaveGuildPanel.GetCurrentGuildData();
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
                // 刷新公会面板以显示新成员
                alreadyHaveGuildPanel.RefreshGuildMembers();
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
    /// 测试功能：随机生成几个公会
    /// </summary>
    /// <param name="count">要生成的公会数量</param>
    public async void GenerateRandomGuilds(int count)
    {
        if (notHaveGuildPanel == null)
        {
            Debug.LogError("无公会面板未设置，无法生成测试公会");
            return;
        }

        int successCount = 0;
        try
        {
            CharacterData currentCharacter = SessionManager.Instance.CurrentCharacter;
            if (currentCharacter == null)
            {
                Debug.LogError("当前角色数据为空，无法生成测试公会");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                // 生成随机公会数据
                GuildData randomGuild = GenerateRandomGuildData(currentCharacter.serverId, currentCharacter.playerUid);

                // 保存公会数据到数据库
                bool guildSaveSuccess = await MongoDBManager.Instance.SaveGuildDataAsync(randomGuild);
                if (guildSaveSuccess)
                {
                    successCount++;
                    Debug.Log($"成功生成测试公会: {randomGuild.guildName}");
                }
                else
                {
                    Debug.LogError($"保存公会 {randomGuild.guildName} 数据失败");
                }
            }

            Debug.Log($"成功生成了 {successCount} 个随机公会");
        }
        catch (Exception ex)
        {
            Debug.LogError($"生成随机公会时发生异常: {ex.Message}");
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
    /// 生成随机公会数据
    /// </summary>
    /// <param name="serverId">服务器ID</param>
    /// <param name="creatorPlayerUid">创建者玩家UID</param>
    /// <returns>随机生成的公会数据</returns>
    private GuildData GenerateRandomGuildData(int serverId, string creatorPlayerUid)
    {
        GuildData guildData = new GuildData();
        guildData.guildName = GenerateRandomGuildName();
        guildData.guildDescription = testGuildDescriptions[UnityEngine.Random.Range(0, testGuildDescriptions.Length)];
        guildData.serverId = serverId;
        guildData.leaderUid = creatorPlayerUid;
        guildData.leaderCharacterName = "测试会长_" + UnityEngine.Random.Range(1000, 9999);
        guildData.createTime = DateTime.Now.Ticks;

        // 为生成的测试公会添加会长为首个成员，确保公会不会没有会长
        GuildMemberInfo leaderMember = new GuildMemberInfo
        {
            Id = ObjectId.GenerateNewId().ToString(),
            playerUid = creatorPlayerUid,
            characterName = guildData.leaderCharacterName,
            characterId = ObjectId.GenerateNewId().ToString(),
            level = UnityEngine.Random.Range(minLevel, maxLevel + 1),
            iconID = UnityEngine.Random.Range(0, 10),
            profession = (CharacterProfession)UnityEngine.Random.Range(0, Enum.GetValues(typeof(CharacterProfession)).Length),
            rank = GuildMemberRank.Leader,
            joinTime = DateTime.Now.Ticks,
            lastOnlineTime = DateTime.Now.Ticks
        };

        guildData.members.Add(leaderMember);

        return guildData;
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

    /// <summary>
    /// 生成随机公会名
    /// </summary>
    /// <returns>随机公会名</returns>
    private string GenerateRandomGuildName()
    {
        return testGuildNames[UnityEngine.Random.Range(0, testGuildNames.Length)] +
               "_" + UnityEngine.Random.Range(100, 999);
    }
}