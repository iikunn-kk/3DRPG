using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class TaskPanel : UIPopPanelBase
{
    [Header("详情界面")]
    [Header("任务名字")] [SerializeField] private TMP_Text taskName;
    [Header("任务介绍")][SerializeField] private TMP_Text taskDescription;
    [Header("任务奖励")][SerializeField] private TMP_Text taskReward;
    [Header("任务进度")][SerializeField] private TMP_Text taskProgress;
    [Header("选择任务的按钮")] [SerializeField] private GameObject taskSelectButton;
    [Header("选择任务界面的选择条Parent")]
    [SerializeField] private Transform taskListParent;
    private List<TaskSelectMod> _taskSelectMods = new List<TaskSelectMod>();
    
    private BaseTask _currentTask; // 当前显示详情的任务

    private void OnEnable()
    {
        // 订阅全局任务事件
        TaskEvents.OnObjectiveProgress += HandleObjectiveProgress;
        TaskEvents.OnTaskStarted += HandleTaskStarted;
        TaskEvents.OnTaskCompleted += HandleTaskCompleted;
        TaskEvents.OnTaskRewardsClaimed += HandleTaskRewardsClaimed;
        // 初次打开时构建列表
        if (TaskManager.Instance != null)
        {
            RebuildTaskList(_currentTask != null ? _currentTask.id : (int?)null);
        }
    }

    private new void OnDisable()
    {
        TaskEvents.OnObjectiveProgress -= HandleObjectiveProgress;
        TaskEvents.OnTaskStarted -= HandleTaskStarted;
        TaskEvents.OnTaskCompleted -= HandleTaskCompleted;
        TaskEvents.OnTaskRewardsClaimed -= HandleTaskRewardsClaimed;
    }
    
    /// <summary>
    /// 初始化任务面板，显示已接受的任务列表
    /// </summary>
    /// <param name="tasks">已接受的任务列表</param>
    public void Init(List<BaseTask> tasks)
    {
        // 清除之前创建的任务选择模块
        foreach (var mod in _taskSelectMods)
        {
            Destroy(mod.gameObject);
        }
        _taskSelectMods.Clear();
        
        // 为每个任务创建一个TaskSelectMod
        foreach (var task in tasks)
        {
            GameObject taskSelectObj = Instantiate(taskSelectButton, taskListParent);
            TaskSelectMod taskSelectMod = taskSelectObj.GetComponent<TaskSelectMod>();
            taskSelectMod.Init(task, OnTaskSelected);
            taskSelectObj.SetActive(true);
            _taskSelectMods.Add(taskSelectMod);
        }
        
        // 如果有任务，默认显示第一个任务的详情
        if (tasks.Count > 0)
        {
            ShowTaskDetails(tasks[0]);
        }
        Show();
    }
    
    /// <summary>
    /// 当任务被选中时调用此方法
    /// </summary>
    /// <param name="task">选中的任务</param>
    private void OnTaskSelected(BaseTask task)
    {
        ShowTaskDetails(task);
    }
    
    private void HandleObjectiveProgress(ObjectiveType type, int targetId, int amount)
    {
        // 仅刷新当前任务进度与选择列表显示
        if (_currentTask != null)
        {
            var id = _currentTask.id;
            if (TaskManager.Instance != null && TaskManager.Instance.tasks.TryGetValue(id, out var refreshed))
            {
                ShowTaskDetails(refreshed);
            }
        }
        foreach (var mod in _taskSelectMods)
        {
            if (mod != null) mod.UpdateDisplay();
        }
    }

    private void HandleTaskStarted(int taskId)
    {
        RebuildTaskList(_currentTask != null ? _currentTask.id : (int?)null);
    }

    private void HandleTaskCompleted(int taskId)
    {
        if (_currentTask != null && _currentTask.id == taskId)
        {
            ShowTaskDetails(_currentTask);
        }
        foreach (var mod in _taskSelectMods)
        {
            if (mod != null) mod.UpdateDisplay();
        }
    }

    private void HandleTaskRewardsClaimed(int taskId)
    {
        // 任务奖励领取后该任务会从 TaskManager 中移除，需要重建列表
        if (_currentTask != null && _currentTask.id == taskId)
        {
            _currentTask = null; // 清空当前引用（已被移除）
            RebuildTaskList(null);
            return;
        }
        // 非当前任务被移除，只需刷新列表显示
        RebuildTaskList(_currentTask != null ? _currentTask.id : (int?)null);
    }
    
    private void RebuildTaskList(int? keepSelectedTaskId)
    {
        var list = TaskManager.Instance.tasks.Values.ToList();
        // 清除旧
        foreach (var mod in _taskSelectMods)
        {
            if (mod != null) Destroy(mod.gameObject);
        }
        _taskSelectMods.Clear();
        foreach (var task in list)
        {
            GameObject taskSelectObj = Instantiate(taskSelectButton, taskListParent);
            TaskSelectMod taskSelectMod = taskSelectObj.GetComponent<TaskSelectMod>();
            taskSelectMod.Init(task, OnTaskSelected);
            taskSelectObj.SetActive(true);
            _taskSelectMods.Add(taskSelectMod);
        }
        if (keepSelectedTaskId.HasValue)
        {
            var t = list.FirstOrDefault(x => x.id == keepSelectedTaskId.Value);
            if (t != null)
            {
                ShowTaskDetails(t);
                return;
            }
        }
        if (list.Count > 0)
        {
            ShowTaskDetails(list[0]);
        }
        else
        {
            _currentTask = null;
            taskName.text = "—";
            taskDescription.text = string.Empty;
            taskReward.text = string.Empty;
            taskProgress.text = string.Empty;
        }
    }
    
    /// <summary>
    /// 显示任务的详细信息
    /// </summary>
    /// <param name="task">要显示详情的任务</param>
    private void ShowTaskDetails(BaseTask task)
    {
        if (task == null)
        {
            return;
        }
        _currentTask = task;
        // 显示任务名称
        taskName.text = task.taskName;
        // 显示任务描述
        taskDescription.text = task.taskDescription;
        // 显示任务奖励
        string rewardText = "";
        foreach (var reward in task.rewards)
        {
            if (!string.IsNullOrEmpty(rewardText))
                rewardText += "\n";
            rewardText += GetRewardDescription(reward);
        }
        taskReward.text = rewardText;
        // 显示任务进度
        string progressText = "";
        foreach (var objective in task.objectives)
        {
            if (!string.IsNullOrEmpty(progressText))
                progressText += "\n";
            progressText += $"{GetObjectDescription(objective)}: {objective.currentAmount}/{objective.requiredAmount}";
        }
        taskProgress.text = progressText;
        // 更新选择高亮
        foreach (var mod in _taskSelectMods)
        {
            if (mod == null) continue;
            mod.SetSelected(mod.TaskId == task.id);
        }
    }
    
    /// <summary>
    /// 领取任务奖励
    /// </summary>
    private void ClaimTaskReward()
    {
        if (_currentTask != null && _currentTask.isCompleted && !_currentTask.isRewardClaimed)
        {
            // 领取奖励
            _currentTask.ClaimRewards();
            
            // TODO: 在这里添加实际的奖励发放逻辑
            Debug.Log($"已领取任务 '{_currentTask.taskName}' 的奖励");
        }
    }
    
    /// <summary>
    /// 获取奖励描述
    /// </summary>
    /// <param name="reward">奖励数据</param>
    /// <returns>奖励描述字符串</returns>
    private string GetRewardDescription(TaskReward reward)
    {
        switch (reward.rewardType)
        {
            case RewardType.Item:
                return $"物品奖励: {reward.rewardDescription} x{reward.amount}";
            case RewardType.Money:
                return $"金币奖励: {reward.amount}";
            case RewardType.Exp:
                return $"经验奖励: {reward.amount}";
            case RewardType.Equipment:
                return $"装备奖励: {reward.rewardDescription}";
            default:
                return reward.rewardDescription;
        }
    }
    
    /// <summary>
    /// 获取任务目标描述
    /// </summary>
    /// <param name="objective">任务目标数据</param>
    /// <returns>目标描述字符串</returns>
    private string GetObjectDescription(TaskObjective objective)
    {
        switch (objective.objectiveType)
        {
            case ObjectiveType.击杀敌人:
                return $"击杀敌人";
            case ObjectiveType.收集物品:
                return $"收集物品";
            case ObjectiveType.和Npc对话:
                return $"与NPC对话";
            default:
                return "任务目标";
        }
    }
    
    public void OnCloseButtonClick()
    {
        UIManager.Instance.ClosePanel<TaskPanel>();
        Hide();
    }
}