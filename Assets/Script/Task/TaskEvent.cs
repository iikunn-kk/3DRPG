using System;
using UnityEngine;

public class TaskEvents : MonoBehaviour
{
    // 事件：当任务目标进度更新时触发
    public static event Action<ObjectiveType, int, int> OnObjectiveProgress;
    
    // 事件：当任务完成时触发
    public static event Action<int> OnTaskCompleted;
    
    // 事件：当任务开始时触发
    public static event Action<int> OnTaskStarted;
    
    // 事件：当任务奖励领取时触发
    public static event Action<int> OnTaskRewardsClaimed;
    // 新增：任务被设为当前追踪时触发
    public static event Action<int> OnTaskTrackedChanged;

    /// <summary>
    /// 触发任务目标进度更新事件
    /// </summary>
    /// <param name="objectiveType">目标类型</param>
    /// <param name="targetId">目标ID</param>
    /// <param name="amount">增加的数量</param>
    public static void TriggerObjectiveProgress(ObjectiveType objectiveType, int targetId, int amount)
    {
        OnObjectiveProgress?.Invoke(objectiveType, targetId, amount);
    }
    
    /// <summary>
    /// 触发任务完成事件
    /// </summary>
    /// <param name="taskId">任务ID</param>
    public static void TriggerTaskCompleted(int taskId)
    {
        OnTaskCompleted?.Invoke(taskId);
    }
    
    /// <summary>
    /// 触发任务开始事件
    /// </summary>
    /// <param name="taskId">任务ID</param>
    public static void TriggerTaskStarted(int taskId)
    {
        OnTaskStarted?.Invoke(taskId);
    }
    
    /// <summary>
    /// 触发任务奖励领取事件
    /// </summary>
    /// <param name="taskId">任务ID</param>
    public static void TriggerTaskRewardsClaimed(int taskId)
    {
        OnTaskRewardsClaimed?.Invoke(taskId);
    }

    // 新增：触发追踪任务变更
    public static void TriggerTaskTrackedChanged(int taskId)
    {
        OnTaskTrackedChanged?.Invoke(taskId);
    }
    
    // 为不同的目标类型提供特定的方法，提高API可用性
    
    /// <summary>
    /// 触发击杀敌人事件
    /// </summary>
    /// <param name="enemyId">敌人ID</param>
    /// <param name="killCount">击杀数量</param>
    public static void TriggerEnemyKilled(int enemyId, int killCount = 1)
    {
        TriggerObjectiveProgress(ObjectiveType.击杀敌人, enemyId, killCount);
    }
    
    /// <summary>
    /// 触发收集物品事件
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <param name="itemCount">物品数量</param>
    public static void TriggerItemCollected(int itemId, int itemCount = 1)
    {
        TriggerObjectiveProgress(ObjectiveType.收集物品, itemId, itemCount);
    }
    
    /// <summary>
    /// 触发与NPC对话事件
    /// </summary>
    /// <param name="npcId">NPC ID</param>
    public static void TriggerNpcTalked(int npcId)
    {
        TriggerObjectiveProgress(ObjectiveType.和Npc对话, npcId, 1);
    }

}