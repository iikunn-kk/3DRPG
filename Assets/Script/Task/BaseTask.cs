// 基础任务类

using System;
using System.Collections.Generic;
using UnityEngine;
using MongoDB.Bson.Serialization.Attributes;

[System.Serializable]
public class BaseTask
{
    // 运行时任务数据
    public int id;                      // 任务唯一ID
    public string introduction;         // 任务介绍
    public bool isCompleted;            // 是否已完成
    public bool isRewardClaimed;        // 副奖励是否已领取
    // Unity types (Sprite) are not BSON-serializable. Ignore when persisting to MongoDB.
    [BsonIgnore]
    public Sprite icon;
    public TaskType type;
    
    // 任务目标列表
    public List<TaskObjective> objectives = new List<TaskObjective>();
    
    // 任务奖励列表
    public List<TaskReward> rewards = new List<TaskReward>();
    
    // 任务完成额外需要消耗的物品（仅在真正完成时一次性扣除）
    public List<TaskConsumeCost> completionCosts = new List<TaskConsumeCost>();
    
    // 前置 & 后续
    public int prerequisiteTaskId = -1;
    public int nextTaskId = -1;
    
    // 任务显示信息
    public string taskName;
    public string taskDescription;
    
    public TaskCategory taskCategory;

    public BaseTask(TaskData taskData)
    {
        this.id = taskData.taskId;
        this.taskName = taskData.taskName;
        this.taskDescription = taskData.taskDescription;
        this.taskCategory = taskData.taskCategory;
        this.prerequisiteTaskId = taskData.prerequisiteTaskId;
        this.nextTaskId = taskData.nextTaskId;

        icon = taskData.icon;
        // 深拷贝目标列表
        this.objectives = new List<TaskObjective>();
        foreach (var objective in taskData.objectives
        )
        {
            this.objectives.Add(new TaskObjective
            {
                objectiveType = objective.objectiveType,
                targetId = objective.targetId,
                requiredAmount = objective.requiredAmount,
                currentAmount = objective.currentAmount,
                // 补充之前遗漏的可追踪与静态位置信息字段，避免运行时丢失导致位置始终为(0,0,0)
                canTrack = objective.canTrack,
                staticWorldPosition = objective.staticWorldPosition,
                staticSceneName = objective.staticSceneName,
                trackRangeRadius = objective.trackRangeRadius
            });
        }
        // 深拷贝奖励列表
        this.rewards = new List<TaskReward>();
        foreach (var reward in taskData.rewards)
        {
            this.rewards.Add(new TaskReward
            {
                rewardType = reward.rewardType,
                itemId = reward.itemId,
                amount = reward.amount,
                rewardDescription = reward.rewardDescription
            });
        }
        // 深拷贝完成消耗
        this.completionCosts = new List<TaskConsumeCost>();
        if (taskData.completionCosts != null)
        {
            foreach (var cost in taskData.completionCosts)
            {
                if (cost == null) continue;
                this.completionCosts.Add(new TaskConsumeCost { itemId = cost.itemId, amount = cost.amount });
            }
        }
    }
    
    public BaseTask() {}
    
    public bool CheckObjectiveCompletion(int objectiveIndex)
    {
        if (objectiveIndex < 0 || objectiveIndex >= objectives.Count)
            return false;
        return objectives[objectiveIndex].currentAmount >= objectives[objectiveIndex].requiredAmount;
    }
    
    public bool CheckAllObjectivesCompletion()
    {
        foreach (var objective in objectives)
        {
            if (objective.currentAmount < objective.requiredAmount)
                return false;
        }
        return true;
    }
    
    public void UpdateObjectiveProgress(ObjectiveType objectiveType, int targetId, int amount)
    {
        if (isCompleted)
            return;
        foreach (var objective in objectives)
        {
            if (objective.objectiveType == objectiveType && objective.targetId == targetId)
            {
                objective.currentAmount = Math.Min(objective.currentAmount + amount, objective.requiredAmount);
            }
        }
        if (CheckAllObjectivesCompletion())
        {
            CompleteTask();
        }
    }
    
    public void CompleteTask()
    {
        if (!isCompleted)
        {
            // 在标记完成前尝试扣除 completionCosts（如果有）
            if (completionCosts != null && completionCosts.Count > 0)
            {
                var inv = InventoryManager.Instance;
                if (inv == null)
                {
                    Debug.LogWarning($"无法完成任务 {id}，InventoryManager 不存在");
                    UIManager.Instance?.ShowToast("背包系统未初始化, 无法提交任务物品");
                    return;
                }
                if (!inv.HasItemsForCosts(completionCosts))
                {
                    // 物品不足，阻止任务完成
                    UIManager.Instance?.ShowToast("提交失败: 所需物品不足");
                    return;
                }
                // 为构造提示先收集物品名称
                List<string> parts = new List<string>();
                foreach (var cost in completionCosts)
                {
                    if (cost == null || cost.amount <= 0) continue;
                    var itemData = GameManager.Instance?.ItemDataSo?.GetItemDataById(cost.itemId);
                    string itemName = itemData != null && !string.IsNullOrEmpty(itemData.itemName) ? itemData.itemName : $"物品{cost.itemId}";
                    parts.Add($"{itemName} x{cost.amount}");
                }
                // 正式扣除
                bool consumed = inv.ConsumeItemsForCosts(completionCosts);
                if (!consumed)
                {
                    UIManager.Instance?.ShowToast("提交失败: 扣除物品时出错");
                    return;
                }
                string itemsText = parts.Count > 0 ? string.Join("、", parts) : "所需物品";
                // 使用任务图标(若无则不传)作为 toast 图标
                UIManager.Instance?.ShowToast($"完成任务《{taskName}》, 提交了 {itemsText}", icon);
            }
            else
            {
                // 没有需要提交的消耗物品也给一个完成提示（可根据需要去掉）
                UIManager.Instance?.ShowToast($"完成任务《{taskName}》", icon);
            }
            isCompleted = true;
            OnComplete();
            // 自动领取奖励（即便没有奖励列表也会标记已领取，以便任务被移除并写入 completedTaskIds）
            if (!isRewardClaimed)
            {
                // 直接调用 ClaimRewards() 以复用事件触发逻辑
                ClaimRewards(); // 这会触发 TaskRewardsClaimed -> TaskManager 移除并记录 completedTaskIds
            }
            // 最后再广播任务完成事件（此时若奖励已触发移除，下一任务的 AcceptTask 通过 completedTaskIds 判断前置）
            TaskEvents.TriggerTaskCompleted(id);
        }
    }
    
    public virtual void Reset()
    {
        foreach (var objective in objectives)
        {
            objective.currentAmount = 0;
        }
        isCompleted = false;
        isRewardClaimed = false;
    }
    
    public virtual void OnComplete() { }
    public virtual void OnRewardClaimed() { }
    
    public virtual void ClaimRewards()
    {
        if ((isCompleted || CheckAllObjectivesCompletion()) && !isRewardClaimed)
        {
            // 实际发放奖励
            bool success = true;
            if (rewards != null && rewards.Count > 0)
            {
                foreach (var reward in rewards)
                {
                    if (reward == null) continue;
                    switch (reward.rewardType)
                    {
                        case RewardType.Item:
                        case RewardType.Equipment:
                        {
                            var inv = InventoryManager.Instance;
                            if (inv == null)
                            {
                                Debug.LogWarning("InventoryManager 不存在，无法发放物品奖励");
                                UIManager.Instance?.ShowToast("背包未初始化，无法发放物品奖励");
                                success = false;
                                break;
                            }
                            int cnt = Mathf.Max(1, reward.amount);
                            bool ok = inv.AddItem(reward.itemId, cnt);
                            if (!ok)
                            {
                                // AddItem 内部会弹 Toast；此处仅标记失败
                                success = false;
                            }
                            break;
                        }
                        case RewardType.Money:
                        {
                            var pcm = PlayerCurrencyManager.Instance;
                            if (pcm == null)
                            {
                                Debug.LogWarning("PlayerCurrencyManager 不存在，无法发放金币奖励");
                                UIManager.Instance?.ShowToast("货币系统未初始化，无法发放金币");
                                success = false;
                                break;
                            }
                            int amt = Mathf.Max(0, reward.amount);
                            if (amt > 0)
                            {
                                bool ok = pcm.AddMoney(amt);
                                if (!ok) success = false;
                            }
                            break;
                        }
                        case RewardType.Exp:
                        {
                            var cs = GameManager.Instance?.CurrentPlayerCharacter();
                            if (cs == null)
                            {
                                Debug.LogWarning("玩家角色不存在，无法发放经验奖励");
                                UIManager.Instance?.ShowToast("无法发放经验：未找到玩家");
                                success = false;
                                break;
                            }
                            int exp = Mathf.Max(0, reward.amount);
                            if (exp > 0) cs.AddExp(exp);
                            break;
                        }
                        default:
                            Debug.LogWarning($"未知的奖励类型: {reward.rewardType}");
                            break;
                    }
                }
            }
            // 若全部成功则标记领取完成并广播；否则不标记，提示玩家稍后重试
            if (success)
            {
                isRewardClaimed = true;
                OnRewardClaimed();
                TaskEvents.TriggerTaskRewardsClaimed(id);
            }
            else
            {
                // 给出统一失败提示（物品满等情况）
                UIManager.Instance?.ShowToast("部分奖励未能发放，请清理背包后重试");
            }
        }
    }
}

// 任务保存数据
[System.Serializable]
public class TaskSaveData
{
    public int taskId;                  // 任务ID
    public bool isCompleted;            // 是否已完成
    public bool isRewardClaimed;        // 奖励是否已领取
    public List<int> objectiveCurrentAmounts; // 各任务目标的当前进度列表
}

[System.Serializable]
public class StringBoolPair
{
    public string key;
    public bool value;
    public StringBoolPair(string key, bool value)
    {
        this.key = key;
        this.value = value;
    }
}

[System.Serializable]
public class StringIntPair
{
    public string key;
    public int value;
    public StringIntPair(string key, int value)
    {
        this.key = key;
        this.value = value;
    }
}

[System.Serializable]
public class GameSaveData
{
    public List<TaskSaveData> taskProgress;         // 任务进度列表
    public List<StringBoolPair> progressFlagsList;     // 进度标志列表
    public List<StringIntPair> globalCountersList;     // 全局计数器列表
    public string lastSaveTime;                        // 最后保存时间
    public string lastResetDate;                       // 最后重置日期
}
