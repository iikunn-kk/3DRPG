using System;
using System.Collections.Generic;
using UnityEngine;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// 玩家物品数据类，用于存储玩家的背包和装备信息
/// </summary>
[Serializable]
public class PlayerInventoryData
{
    [BsonId]
    public string Id { get; set; } // 这可以作为文档ID
    // 关联的角色ID
    public string characterId;
    // 核心改动：用一个列表存储所有物品
    public List<InventoryItem> allItems = new();
    public PlayerInventoryData(string characterId)
    {
        this.Id = ObjectId.GenerateNewId().ToString(); // 或者直接使用 characterId 作为主键
        this.characterId = characterId;
        this.allItems = new List<InventoryItem>();
    }
    
    public PlayerInventoryData() 
    {
        this.allItems = new List<InventoryItem>();
    }
}