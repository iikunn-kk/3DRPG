using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "所有的任务数据", menuName = "Data/所有的任务数据")]
public class TaskDataSO : ScriptableObject
{
    [Header("主线任务")] public List<TaskData> mainMission = new List<TaskData>();
    [Header("支线任务")] public List<TaskData> sideMission = new List<TaskData>();
}

[Serializable]
public class TaskConsumeCost
{
    [Header("物品ID")] public int itemId;
    [Header("需要消耗的数量")] public int amount;
}

[Serializable]
public class TaskData
{
    [Header("基础信息")] public int taskId; public string taskName; public string taskDescription; public Sprite icon;
    [Header("任务分类")] public TaskCategory taskCategory;

    [Header("任务目标")] public List<TaskObjective> objectives = new List<TaskObjective>();

    [Header("任务奖励")] public List<TaskReward> rewards = new List<TaskReward>();
    [Header("任务链奖励预览(仅第一环显示, 完成整条链后一次性发放 – 这里只展示不发放)")]
    public List<TaskReward> chainPreviewRewards = new List<TaskReward>();

    [Header("任务链")] public int prerequisiteTaskId = -1; // 前置任务ID，-1表示无前置任务
    public int nextTaskId = -1; // 后续任务ID，-1表示无后续任务

    [Header("任务开始 NPC")][Tooltip("如果该任务是一个任务链的开始任务, 配置其开始 NPC 的 NpcID (-1 表示未配置)")] public int startNpcId = -1;

    [Header("(已精简) 与该任务直接关联的 NPC (原 relatedNpcIds List 现在改为 单个 int)")][Tooltip("如果任务与某个 NPC 对话/标记有关(用于旧系统, 保持一个引用方便检查), -1 表示无")] public int relatedNpcId = -1;

    [Header("任务完成时的对话(TaskDialog) —— 当玩家与目标 NPC 对话且该 NPC 是任务目标时优先播放")]
    [TextArea]
    public List<string> taskDialog = new List<string>();

    [Header("接任务时的对话 (原 string acceptDialog -> List<string>)")]
    [TextArea]
    public List<string> acceptDialog = new List<string>();

    [Header("完成对话播放后, 只显示的自定义按钮文字")] public string postTaskOptionText = "继续";

    [Header("完成任务时需要一次性扣除的物品(不会在收集阶段扣除)")][Tooltip("例如收集 5 个蘑菇并交付时才真正扣除它们")] public List<TaskConsumeCost> completionCosts = new List<TaskConsumeCost>();
}

[Serializable]
public enum TaskType { 杀敌, 收集物品, 和npc对话, }

[Serializable]
public enum TaskCategory { MainQuest, SideQuest }

[Serializable]
public class TaskObjective
{
    [Header("目标类型")] public ObjectiveType objectiveType;

    [Header("目标参数")] public int targetId; // 目标ID（如怪物ID、物品ID、NPC ID等）
    [Header("目标数量")] public int requiredAmount;
    [Header("无需更改")] public int currentAmount; // 当前完成的数量
    [Header("追踪设置")][Tooltip("该目标是否可被追踪(决定是否显示方向箭头/距离)")] public bool canTrack = true;
    [Tooltip("如果没有动态锚点(TaskTargetAnchor)，可在此提供一个静态世界坐标(可选)")] public Vector3 staticWorldPosition;
    [Tooltip("静态坐标所在场景名称(留空表示当前场景)")] public string staticSceneName;
    [Tooltip("到达判定半径(玩家进入该范围视为已到达任务区域)")] public float trackRangeRadius = 5f;
}

[Serializable]
public enum ObjectiveType { 击杀敌人, 收集物品, 和Npc对话 }

[Serializable]
public class TaskReward
{
    [Header("奖励类型")] public RewardType rewardType;
    [Header("奖励参数")] public int itemId; // 物品ID（用于道具奖励）
    [Header("奖励的数量")] public int amount; // 数量（用于道具、金钱、经验奖励）
    [Header("奖励描述")] public string rewardDescription;
}

[Serializable]
public enum RewardType { Item, Money, Exp, Equipment }
