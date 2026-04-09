using System;
using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// 用于存储角色拥有的技能及其等级的数据类。
/// 这个类的实例将被保存在 CharacterData 中，用于数据持久化。
/// </summary>
[Serializable]
public class SkillSaveData
{
    /// <summary>
    /// 技能的唯一ID，关联到 SkillSO.SkillID
    /// </summary>
    [BsonElement("SkillID")]
    public string SkillID { get; set; }

    /// <summary>
    /// 技能的当前等级
    /// </summary>
    [BsonElement("Level")]
    public int Level { get; set; }

    public SkillSaveData(string skillID, int level)
    {
        SkillID = skillID;
        Level = level;
    }
}

