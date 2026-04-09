using System.Collections.Generic;
using UnityEngine;

public class NpcData : ScriptableObject
{
    public string NpcName;
    public int NpcID;
    [Header("商店设置")]
    [Header("如果NPC是商人，可以为其指定单独的商店配置文件")]
    public NpcShopDataSO npcShopData;
    [Header("对话内容列表")]
    [TextArea]
    public List<string> basicDialogs = new List<string>();
}