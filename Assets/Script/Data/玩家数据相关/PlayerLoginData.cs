using System;
using System.Collections.Generic;
using UnityEngine;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// 玩家登录数据类，只包含登录验证所需的基本信息
/// </summary>
[Serializable]
public class PlayerLoginData
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    
    public string uid;
    public string username;
    
    // 移除旧的密码字段
    // public string password; 
    
    // 添加用于存储哈希和盐的新字段
    public string passwordHash { get; set; }
    public string passwordSalt { get; set; }

    // 更新构造函数
    public PlayerLoginData(string username, string passwordHash, string passwordSalt)
    {
        this.uid = Guid.NewGuid().ToString();
        this.username = username;
        this.passwordHash = passwordHash;
        this.passwordSalt = passwordSalt;
    }
    
    public PlayerLoginData() {}
}
