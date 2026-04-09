using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GameDemo.Models
{
    /// <summary>
    /// 任务进度数据类，用于存储玩家的任务进度信息
    /// </summary>
    [Serializable]
    public class TaskProgressData
    {
        [BsonId]
        public string Id { get; set; }
        
        // 角色ID，作为查询任务进度的键
        [BsonElement("characterId")]
        public string characterId;
        
        // 任务进度数据，以JSON字符串形式存储
        [BsonElement("taskData")]
        public string taskData;
        
        // 最后更新时间
        [BsonElement("lastUpdated")]
        public DateTime lastUpdated;
        
        public TaskProgressData()
        {
            Id = ObjectId.GenerateNewId().ToString();
            taskData = string.Empty;
            lastUpdated = DateTime.UtcNow;
        }
        
        public TaskProgressData(string characterId, string taskData)
        {
            Id = ObjectId.GenerateNewId().ToString();
            this.characterId = characterId;
            this.taskData = taskData;
            this.lastUpdated = DateTime.UtcNow;
        }
    }
}