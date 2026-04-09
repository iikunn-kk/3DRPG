using System;
using System.Collections.Generic;
using UnityEngine;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// 角色数据类，用于存储角色的所有信息
/// </summary>
[Serializable]
public class CharacterData
{
    [BsonId]
    public string Id { get; set; }

    public string characterName;
    [BsonRepresentation(BsonType.String)]
    public CharacterProfession profession;
    public int level;
    public int exp;
    public int hp;
    public int gold;
    public int gem;
    public string currentScene;
    public Vector3 position;
    public int serverId; // 角色所属服务器ID
    public string playerUid; // 关联的玩家UID
    public string guildId;
    public int iconID; // 角色头像ID

    // 正在进行中的任务（仅保存未完成的任务及其首目标进度）
    public List<TaskLiteData> taskList = new();
    // 已完成（含已领取奖励）任务ID 集合，用于前置依赖判断与避免重复接受
    public List<int> completedTaskIds = new();

    // 技能数据字段
    public List<SkillSaveData> skills = new();

    // 新增: 世界商店购买记录（按物品ID累计购买次数）
    public List<ShopPurchaseRecord> worldShopPurchases = new();
    // 新增: NPC 商店购买记录（按 npcId + itemId 累计购买次数）
    public List<NpcShopPurchaseRecord> npcShopPurchases = new();

    public CharacterData(string thisPlayerUid, int serverId, string characterName, CharacterProfession pro)
    {
        this.Id = ObjectId.GenerateNewId().ToString();
        position = Vector3.zero;
        level = 1;
        playerUid = thisPlayerUid;
        profession = pro;
        this.serverId = serverId;
        this.characterName = characterName;
        guildId = String.Empty;
        iconID = 0; // 默认头像ID
        hp = GameManager.Instance.playerCharacterStateDataSo.
            GetPlayerCharacterStateBaseData(profession).GetMaxHp(level);
        taskList = new List<TaskLiteData>();
        completedTaskIds = new List<int>();
        skills = new List<SkillSaveData>();
        // currentScene = "Level_1";

        currentScene = "Village"; // 改为 Village

        worldShopPurchases = new List<ShopPurchaseRecord>();
        npcShopPurchases = new List<NpcShopPurchaseRecord>();
    }

    public CharacterData() { }
}

[Serializable]
public class TaskLiteData
{
    public int taskId;
    public int progress; // 对应任务首个目标 currentAmount 的快照
    public TaskLiteData() { }
    public TaskLiteData(int id, int prog) { taskId = id; progress = prog; }
}

// 新增: 世界商店购买记录结构
[Serializable]
public class ShopPurchaseRecord
{
    public int itemId;
    public int purchased; // 已购买次数
}

// 新增: NPC 商店购买记录结构
[Serializable]
public class NpcShopPurchaseRecord
{
    public int npcId;
    public int itemId;
    public int purchased; // 已购买次数
}
