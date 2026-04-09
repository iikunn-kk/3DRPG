using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 好友系统数据类
/// </summary>
[Serializable]
public class FriendData
{
    public string playerUid;                 // 玩家UID
    public int serverId;                     // 所属服务器ID
    public List<FriendInfo> friends;         // 好友列表
    public List<FriendRequestInfo> requests; // 好友请求列表
    
    public FriendData()
    {
        friends = new List<FriendInfo>();
        requests = new List<FriendRequestInfo>();
    }
}

/// <summary>
/// 好友信息
/// </summary>
[Serializable]
public class FriendInfo
{
    public string friendUid;          // 好友UID
    public string characterName;      // 好友角色名
    public int characterId;           // 角色ID
    public int level;                 // 好友等级
    public CharacterProfession profession; // 职业
    public bool isOnline;             // 是否在线
    public long lastOnlineTime;       // 最后在线时间 (使用ticks)
    public string remark;             // 好友备注
}

/// <summary>
/// 好友请求信息
/// </summary>
[Serializable]
public class FriendRequestInfo
{
    public string requesterUid;       // 请求者UID
    public string requesterName;      // 请求者名称
    public int requesterLevel;        // 请求者等级
    public CharacterProfession requesterProfession; // 请求者职业
    public string requestMessage;     // 请求消息
    public long requestTime;          // 请求时间 (使用ticks)
    public FriendRequestType type;    // 请求类型（申请/接受）
}

/// <summary>
/// 好友请求类型
/// </summary>
public enum FriendRequestType
{
    Request,   // 申请
    Accept     // 接受
}