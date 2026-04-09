using System;
using System.Collections.Generic;
using UnityEngine;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;


/// <summary>
/// 公会数据类
/// </summary>
[Serializable]

public class GuildData
{
    [BsonId]
    public string guildId;                    // 公会唯一ID
    public string guildName;                 // 公会名称
    public string guildDescription;          // 公会描述
    public string guildAnnouncement;         // 公会公告（新增字段，用于存储面板中的公告）
    public int serverId;                     // 所属服务器ID
    public string leaderUid;                 // 会长UID
    public string leaderCharacterName;       // 会长角色名
    public List<GuildMemberInfo> members;    // 公会成员列表
    public List<GuildApplicationInfo> applications; // 公会申请列表
    public long createTime;                  // 创建时间 (使用ticks)
    
    public GuildData()
    {
        guildId = ObjectId.GenerateNewId().ToString();
        members = new List<GuildMemberInfo>();
        applications = new List<GuildApplicationInfo>();
        createTime = DateTime.Now.Ticks;
        guildAnnouncement = string.Empty;
    }
}

/// <summary>
/// 公会成员信息
/// </summary>
[Serializable]

public class GuildMemberInfo
{
    [BsonId]
    public string Id { get; set; }
    public string playerUid;          // 玩家UID
    public string characterName;      // 角色名
    public string characterId;           // 角色ID
    public int level;                 // 角色等级
    public int iconID;                //头像ID
    [BsonRepresentation(BsonType.String)]
    public CharacterProfession profession; // 职业
    [BsonRepresentation(BsonType.String)]
    public GuildMemberRank rank;      // 公会职位
    public long joinTime;             // 加入时间 (使用ticks)
    public long lastOnlineTime;       // 最后在线时间 (使用ticks)
}

/// <summary>
/// 公会申请信息
/// </summary>
[Serializable]

public class GuildApplicationInfo
{
    [BsonId]
    public string Id { get; set; }
    
    public string playerUid;          // 申请人UID
    public string characterName;      // 申请人角色名
    public int characterId;           // 角色ID
    public int level;                 // 申请人等级
    [BsonRepresentation(BsonType.String)]
    public CharacterProfession profession; // 职业
    public string applicationMessage; // 申请留言
    public long applicationTime;      // 申请时间 (使用ticks)
}

/// <summary>
/// 公会成员职位枚举
/// </summary>
public enum GuildMemberRank
{
    Member,      // 普通成员
    Officer,     // 干事
    ViceLeader,  // 副会长
    Leader       // 会长
}