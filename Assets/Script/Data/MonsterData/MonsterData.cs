using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "MonsterData", menuName = "Data/单个怪物数据")]
public class MonsterData : ScriptableObject
{
    public int monsterID;
    public string monsterName;
    public int health;
    public int damage;
    public int speed;
    public int level;
    public Sprite monsterSprite;
    [Header("死亡掉落的物品列表")]
    public List<int> dropItemList;
    [Header("怪物的模型")]
    public GameObject monsterModel;

    [Header("击杀奖励")]
    [Tooltip("击杀后玩家获得的经验值")] public int expReward;
}
