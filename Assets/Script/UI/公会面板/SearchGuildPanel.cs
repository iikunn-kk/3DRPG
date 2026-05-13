using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using System;
using Random = UnityEngine.Random;

public class SearchGuildPanel : UIPopPanelBase
{
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private GameObject guildItemPrefab;
    [SerializeField] private ConfirmJoinGuildPanel confirmJoinGuildPanel;
    [SerializeField] private Transform guildListContent;

    // 添加对NotHaveGuildPanel的引用
    private NotHaveGuildPanel notHaveGuildPanel;

    // 存储当前显示的所有公会项
    private List<SearchGuildSinglePrefab> guildItems = new List<SearchGuildSinglePrefab>();
    // 存储所有公会数据，用于搜索过滤
    private List<GuildData> allGuilds = new List<GuildData>();

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

    public void Init(NotHaveGuildPanel panel = null)
    {
        // 保存NotHaveGuildPanel引用
        notHaveGuildPanel = panel;

        // 清空之前的公会列表
        ClearGuildList();

        // 加载当前服务器上的所有公会
        LoadGuildsForCurrentServer();

        // 初始化搜索输入框
        searchInput.onValueChanged.AddListener(OnSearchInputValueChange);
        Show();
    }

    /// <summary>
    /// 加载当前服务器上的所有公会
    /// </summary>
    private async void LoadGuildsForCurrentServer()
    {
        // 获取当前服务器ID
        int serverId = GameManager.Instance.CurrentCharacter.serverId;

        // 从数据库获取该服务器上的所有公会
        List<GuildData> guilds = await MongoDBManager.Instance.GetGuildsByServerId(serverId);

        // 保存所有公会数据
        allGuilds = guilds;

        // 显示所有公会
        DisplayGuilds(guilds);
    }

    /// <summary>
    /// 显示公会列表
    /// </summary>
    /// <param name="guilds">要显示的公会列表</param>
    private void DisplayGuilds(List<GuildData> guilds)
    {
        ClearGuildList();

        foreach (GuildData guild in guilds)
        {
            // 创建公会项
            GameObject guildItemObj = Instantiate(guildItemPrefab, guildListContent);
            SearchGuildSinglePrefab guildItem = guildItemObj.GetComponent<SearchGuildSinglePrefab>();

            if (guildItem != null)
            {
                // 初始化公会项
                guildItem.Init(guild, OnJoinGuildButtonClick);

                // 设置公会项的显示信息
                guildItems.Add(guildItem);
            }
        }
    }

    /// <summary>
    /// 清空公会列表
    /// </summary>
    private void ClearGuildList()
    {
        foreach (SearchGuildSinglePrefab item in guildItems)
        {
            if (item != null && item.gameObject != null)
            {
                Destroy(item.gameObject);
            }
        }
        guildItems.Clear();
    }

    /// <summary>
    /// 当点击加入公会按钮时调用
    /// </summary>
    /// <param name="guildId">公会ID</param>
    private void OnJoinGuildButtonClick(string guildId)
    {
        // 直接显示确认加入公会面板，让用户确认是否要申请加入
        confirmJoinGuildPanel.gameObject.SetActive(true);

        // 获取公会信息用于显示在确认面板上
        SearchGuildSinglePrefab guildItem = guildItems.Find(item => item.GetGuildData().guildId == guildId);
        if (guildItem != null)
        {
            GuildData guildData = guildItem.GetGuildData();
            confirmJoinGuildPanel.Init(guildData, () => OnConfirmJoinGuildButtonClick(guildId));
        }
        else
        {
            // 如果找不到对应的公会项，则从数据库获取公会信息
            _ = LoadGuildDataAndInitConfirmPanel(guildId);
        }
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }

    /// <summary>
    /// 异步加载公会数据并初始化确认面板
    /// </summary>
    /// <param name="guildId">公会ID</param>
    /// <returns></returns>
    private async System.Threading.Tasks.Task LoadGuildDataAndInitConfirmPanel(string guildId)
    {
        GuildData guildData = await MongoDBManager.Instance.GetGuildData(guildId);
        if (guildData != null)
        {
            confirmJoinGuildPanel.Init(guildData, () => OnConfirmJoinGuildButtonClick(guildId));
        }
    }

    /// <summary>
    /// 当在确认面板点击确认按钮时调用
    /// </summary>
    /// <param name="guildId">公会ID</param>
    private async void OnConfirmJoinGuildButtonClick(string guildId)
    {
        // 调用GameManager的加入公会功能
        bool success = await GameManager.Instance.JoinGuild(guildId);

        if (success)
        {
            Debug.Log("成功加入公会");
            // 可以在这里添加UI反馈，比如关闭面板或显示成功消息
            Hide(false);

            // 如果有NotHaveGuildPanel引用，则通知它加入公会成功
            if (notHaveGuildPanel != null)
            {
                await notHaveGuildPanel.OnJoinGuildSuccess(guildId);
            }
        }
        else
        {
            Debug.LogError("加入公会失败");
        }
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }

    public void OnSearchInputValueChange(string value)
    {
        // 如果输入为空，则显示所有公会
        if (string.IsNullOrEmpty(value))
        {
            DisplayGuilds(allGuilds);
            return;
        }

        // 根据公会名称过滤公会列表（不区分大小写）
        List<GuildData> filteredGuilds = allGuilds
            .Where(guild => guild.guildName.ToLower().Contains(value.ToLower()))
            .ToList();

        // 显示过滤后的公会列表
        DisplayGuilds(filteredGuilds);
    }

    public void OnCancelButtonClick()
    {
        // 移除事件监听器避免内存泄漏
        searchInput.onValueChanged.RemoveListener(OnSearchInputValueChange);
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
        Hide(false);
    }

    /// <summary>
    /// 测试功能：随机生成几个公会（无参数版本，用于UnityEvent调用）
    /// </summary>
    public void GenerateRandomGuilds()
    {
        GenerateRandomGuilds(5); // 默认生成5个公会
    }

    /// <summary>
    /// 测试功能：随机生成几个公会
    /// </summary>
    /// <param name="count">要生成的公会数量</param>
    public async void GenerateRandomGuilds(int count)
    {
        if (count <= 0)
        {
            Debug.LogWarning("生成公会数量必须大于0");
            return;
        }

        int successCount = 0;
        try
        {
            CharacterData currentCharacter = GameManager.Instance.CurrentCharacter;
            if (currentCharacter == null)
            {
                Debug.LogError("当前角色数据为空，无法生成测试公会");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                // 生成随机公会数据
                GuildData randomGuild = GenerateRandomGuildData(currentCharacter.serverId, Guid.NewGuid().ToString());

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

            // 重新加载公会列表以显示新生成的公会
            LoadGuildsForCurrentServer();
        }
        catch (Exception ex)
        {
            Debug.LogError($"生成随机公会时发生异常: {ex.Message}");
        }
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
            Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
            playerUid = creatorPlayerUid,
            characterName = guildData.leaderCharacterName,
            characterId = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
            level = UnityEngine.Random.Range(1, 100),
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
    /// 生成随机公会名
    /// </summary>
    /// <returns>随机公会名</returns>
    private string GenerateRandomGuildName()
    {
        return testGuildNames[UnityEngine.Random.Range(0, testGuildNames.Length)] +
               "_" + UnityEngine.Random.Range(100, 999);
    }

    private void OnDestroy()
    {
        // 清理资源
        ClearGuildList();
    }
}