using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using System.Text;

public class NpcDialogUI : UIPopPanelBase, IPointerClickHandler
{
    #region UI组件引用
    [Header("选项按钮的父容器，用于放置所有选项按钮")]
    public Transform optionsContainer;
    [Header("选项按钮预制体，用于动态创建选项按钮")]
    public GameObject optionButtonPrefab;
    [Header("对话文本组件")]
    public TMP_Text dialogText;
    [Header("NPC名字文本组件")]
    public TMP_Text npcNameText;
    #endregion

    #region 奖励显示相关
    [Header("任务奖励显示（可选）")]
    [Tooltip("用于在对话结束时显示本次获得的奖励（可为空，若为空回退到文本显示）")]
    [SerializeField] private GameObject rewardPrefab;
    [Tooltip("实例化 rewardPrefab 的父容器（可为空）")]
    [SerializeField] private Transform rewardsContainer;
    #endregion

    #region 状态变量
    // 当前交互的NPC引用
    private NpcBase _currentNpc;
    // 选项按钮列表(仅使用 NpcOptionButton)
    private List<NpcOptionButton> _optionButtons = new List<NpcOptionButton>();
    // 基础对话索引
    private int _currentDialogIndex;
    // 基础对话是否处于可点击推进状态（显示最后一条后自动锁定）
    private bool _basicDialogsActive;
    // 防止重复触发任务对话进度事件
    private bool _talkProgressTriggered;
    #endregion

    #region 临时对话序列（用于接受任务对话和其他短序列）
    // 临时对话行列表
    private List<string> _tempLines;
    // 临时对话索引
    private int _tempIndex;
    // 临时对话完成回调
    private Action _tempFinished;
    #endregion

    #region 任务目标自动对话
    // 任务目标对话行列表
    private List<string> _objectiveLines;
    // 任务目标对话索引
    private int _objectiveIndex;
    // 任务目标任务列表
    private List<BaseTask> _objectiveTasks;
    // 每条 objective line 对应的源任务ID（用于确定最后一条属于哪个任务）
    private List<int> _objectiveLinesSourceTaskIds;
    // 关闭时需要触发一次NpcTalk
    private bool _pendingTalkProgress;
    #endregion

    #region 公共入口：显示通用对话（从NpcBase.Interact调用）
    /// <summary>
    /// 显示NPC对话
    /// </summary>
    /// <param name="npc">当前NPC</param>
    /// <param name="playerInteract">玩家交互对象</param>
    public void ShowDialog(NpcBase npc, PlayerInteraction playerInteract)
    {
        _currentNpc = npc;
        var data = npc?.NpcData;
        if (npcNameText != null && data != null) npcNameText.text = data.NpcName;

        ClearOptions();
        _currentDialogIndex = 0;
        ResetSequenceState();

        // 如果该 NPC 是当前任一任务对话目标，则播放任务 taskDialog 序列
        if (ShouldAutoPlayObjectiveDialog(out _objectiveLines, out _objectiveTasks))
        {
            _objectiveIndex = 0;
            _basicDialogsActive = false; // 不是基础对话
            PlayNextObjectiveLine();
            Show();
            return;
        }

        // 否则正常显示基础对话（逐条点击继续 -> 选项）
        var dataForBasic = _currentNpc?.NpcData;
        if (dataForBasic != null)
        {
            if (dataForBasic.basicDialogs == null || dataForBasic.basicDialogs.Count == 0 || _currentDialogIndex >= dataForBasic.basicDialogs.Count)
            {
                // 没有基础对话，直接显示选项
                _basicDialogsActive = false;
                ClearOptions();
                AddOptionsForNpc(_currentNpc?.NpcData);
                if (optionsContainer != null) optionsContainer.gameObject.SetActive(true);
            }
            else
            {
                // 显示第一条
                if (dialogText != null) dialogText.text = dataForBasic.basicDialogs[_currentDialogIndex];
                _currentDialogIndex++;
                _basicDialogsActive = true; // 处于基础对话推进状态
                if (optionsContainer != null) optionsContainer.gameObject.SetActive(false);

                // 如果只有一条，立刻显示选项并锁定点击
                if (_currentDialogIndex >= dataForBasic.basicDialogs.Count)
                {
                    ClearOptions();
                    AddOptionsForNpc(_currentNpc?.NpcData);
                    if (optionsContainer != null) optionsContainer.gameObject.SetActive(true);
                    _basicDialogsActive = false; // 已经是最后一条，锁定点击
                }
            }
        }
        Show();
    }
    #endregion

    #region 接受任务序列API
    /// <summary>
    /// 显示任务接受对话序列
    /// </summary>
    public void ShowAcceptSequence(NpcBase npc, TaskData taskData)
    {
        _currentNpc = npc;
        PlayAcceptDialog(taskData);
    }

    /// <summary>
    /// 播放接受任务对话
    /// </summary>
    public void PlayAcceptDialog(TaskData taskData)
    {
        if (taskData == null) return;
        Show();
        ClearOptions();
        ResetSequenceState();

        bool isChainFirst = taskData.prerequisiteTaskId == -1 && taskData.chainPreviewRewards != null && taskData.chainPreviewRewards.Count > 0;

        if (taskData.acceptDialog != null && taskData.acceptDialog.Count > 0)
        {
            StartTempSequence(new List<string>(taskData.acceptDialog), () =>
            {
                ClearOptions();
                if (isChainFirst)
                {
                    // 展示任务链奖励预览
                    if (dialogText != null) dialogText.text = BuildChainPreviewDescription(taskData);
                    ShowChainPreviewRewards(taskData);
                }
                else
                {
                    // 原行为：显示任务详情
                    if (dialogText != null) dialogText.text = BuildTaskDetailDescription(taskData);
                    ClearPendingRewards();
                }
                CreateOption(OptionType.Close, "确认", OnCloseOptionSelected);
                if (optionsContainer != null) optionsContainer.gameObject.SetActive(true);
            });
        }
        else
        {
            if (isChainFirst)
            {
                if (dialogText != null) dialogText.text = BuildChainPreviewDescription(taskData);
                ShowChainPreviewRewards(taskData);
            }
            else
            {
                if (dialogText != null) dialogText.text = BuildTaskDetailDescription(taskData);
                ClearPendingRewards();
            }
            ClearOptions();
            CreateOption(OptionType.Close, "确认", OnCloseOptionSelected);
            if (optionsContainer != null) optionsContainer.gameObject.SetActive(true);
        }
    }
    #endregion

    #region 对话序列工具方法
    private void StartTempSequence(List<string> lines, Action finished)
    {
        _tempLines = lines ?? new List<string>();
        _tempIndex = 0;
        _tempFinished = finished;
        if (dialogText != null && _tempLines.Count > 0) dialogText.text = _tempLines[0];
        _tempIndex = 1; // 下一个要显示的索引
        if (optionsContainer != null) optionsContainer.gameObject.SetActive(false);
    }

    private void ResetSequenceState()
    {
        _tempLines = null;
        _tempIndex = 0;
        _tempFinished = null;
        _objectiveLines = null;
        _objectiveIndex = 0;
        _objectiveTasks = null;
        _objectiveLinesSourceTaskIds = null;
        _pendingTalkProgress = false;
        _basicDialogsActive = false;
        _talkProgressTriggered = false;
        ClearPendingRewards();
    }
    #endregion

    #region 任务目标对话（任务完成）
    private bool ShouldAutoPlayObjectiveDialog(out List<string> mergedLines, out List<BaseTask> relatedTasks)
    {
        mergedLines = null;
        relatedTasks = null;
        if (_currentNpc == null) return false;
        var tm = TaskManager.Instance;
        if (tm == null) return false;
        int id = _currentNpc.NpcData.NpcID;
        var active = tm.GetActiveTalkTasksForNpc(id);
        if (active == null || active.Count == 0) return false;
        relatedTasks = active;
        mergedLines = new List<string>();
        _objectiveLinesSourceTaskIds = new List<int>();
        foreach (var rt in active)
        {
            var td = tm.FindTaskDataById(rt.id);
            if (td?.taskDialog != null && td.taskDialog.Count > 0) mergedLines.AddRange(td.taskDialog);
            if (td?.taskDialog != null && td.taskDialog.Count > 0)
            {
                for (int i = 0; i < td.taskDialog.Count; i++) _objectiveLinesSourceTaskIds.Add(rt.id);
            }
        }
        if (mergedLines.Count == 0) mergedLines.Add("...");
        _objectiveTasks = relatedTasks;
        return true;
    }

    private void PlayNextObjectiveLine()
    {
        if (_objectiveLines == null) return;
        if (_objectiveIndex < _objectiveLines.Count)
        {
            bool isLastLine = _objectiveIndex == _objectiveLines.Count - 1;
            if (dialogText != null) dialogText.text = _objectiveLines[_objectiveIndex];
            _objectiveIndex++;
            if (optionsContainer != null) optionsContainer.gameObject.SetActive(false);
            if (!isLastLine) return; // 不是最后一条继续等待下一次点击
        }
        // 到这里表示最后一条刚刚显示完，后续不再允许点击推进 objectiveLines

        ClearOptions();

        var tm = TaskManager.Instance;
        bool handledNoRewardCompletable = false;

        if (_objectiveTasks != null && tm != null)
        {
            int npcId = _currentNpc?.NpcData?.NpcID ?? -1;
            int lastSourceTaskId = -1;
            if (_objectiveLinesSourceTaskIds != null && _objectiveLinesSourceTaskIds.Count > 0)
                lastSourceTaskId = _objectiveLinesSourceTaskIds[_objectiveLinesSourceTaskIds.Count - 1];

            if (lastSourceTaskId != -1)
            {
                var lastBase = _objectiveTasks.Find(bt => bt != null && bt.id == lastSourceTaskId);
                if (lastBase != null)
                {
                    bool willComplete = true;
                    foreach (var o in lastBase.objectives)
                    {
                        int future = o.currentAmount;
                        if (o.objectiveType == ObjectiveType.和Npc对话 && o.targetId == npcId)
                            future = Math.Min(o.currentAmount + 1, o.requiredAmount);
                        if (future < o.requiredAmount) { willComplete = false; break; }
                    }

                    if (willComplete && (lastBase.rewards == null || lastBase.rewards.Count == 0))
                    {
                        var td = tm.FindTaskDataById(lastBase.id);
                        string btnText = (td != null && !string.IsNullOrEmpty(td.postTaskOptionText)) ? td.postTaskOptionText : "继续";
                        CreateOption(OptionType.Close, btnText, () =>
                        {
                            TaskEvents.TriggerNpcTalked(_currentNpc?.NpcData?.NpcID ?? -1);
                            HideDialog();
                        });
                        if (optionsContainer != null) optionsContainer.gameObject.SetActive(true);
                        handledNoRewardCompletable = true;
                    }
                }
            }

            if (!handledNoRewardCompletable)
            {
                foreach (var t in _objectiveTasks)
                {
                    if (t == null) continue;
                    bool willComplete = true;
                    foreach (var o in t.objectives)
                    {
                        int future = o.currentAmount;
                        if (o.objectiveType == ObjectiveType.和Npc对话 && o.targetId == npcId)
                            future = Math.Min(o.currentAmount + 1, o.requiredAmount);
                        if (future < o.requiredAmount) { willComplete = false; break; }
                    }
                    if (!willComplete) continue;

                    if (t.rewards == null || t.rewards.Count == 0)
                    {
                        var td = tm.FindTaskDataById(t.id);
                        string btnText = (td != null && !string.IsNullOrEmpty(td.postTaskOptionText)) ? td.postTaskOptionText : "继续";
                        CreateOption(OptionType.Close, btnText, () =>
                        {
                            TaskEvents.TriggerNpcTalked(_currentNpc?.NpcData?.NpcID ?? -1);
                            HideDialog();
                        });
                        if (optionsContainer != null) optionsContainer.gameObject.SetActive(true);
                        handledNoRewardCompletable = true;
                        break;
                    }
                }
            }
        }

        if (!handledNoRewardCompletable)
        {
            if (TryShowPendingRewardsVisually())
            {
                // 成功显示视觉奖励
            }
            else
            {
                var preview = BuildPendingRewardsPreview();
                if (!string.IsNullOrEmpty(preview) && dialogText != null) dialogText.text = preview;
            }

            CreateOption(OptionType.Close, "领取奖励", OnCloseAfterObjectiveDialogs);
            if (optionsContainer != null) optionsContainer.gameObject.SetActive(true);
            _pendingTalkProgress = true;
        }
    }
    #endregion

    #region 选项创建
    private void AddOptionsForNpc(NpcData npcData)
    {
        if (npcData == null)
        {
            return;
        }
        var tm = TaskManager.Instance;
        int npcId = npcData.NpcID;

        // 先获取可开始任务列表（即便后面是 objective 目标）
        List<TaskData> starts = tm != null ? tm.GetStartTasksForNpc(npcId) : new List<TaskData>();

        // 如果未找到显式 startNpcId 的任务，尝试通过 relatedNpcId 兜底
        if ((starts == null || starts.Count == 0) && tm != null)
        {
            var fallback = tm.GetFallbackStartTasksForNpc(npcId);
            if (fallback != null && fallback.Count > 0)
            {
                starts = fallback; // 用 fallback 列表代替
            }
        }

        bool isObjective = tm != null && tm.IsNpcCurrentObjectiveTarget(npcId);

        // 先添加商店（若存在）
        if (npcData.npcShopData != null)
        {
            CreateOption(OptionType.Shop, "商店", OnShopOptionSelected);
        }

        // 添加可开始任务按钮
        if (starts != null && starts.Count > 0)
        {
            foreach (var td in starts)
            {
                if (td == null) continue;
                var local = td; // capture
                CreateOption(OptionType.Quest, td.taskName, () =>
                {
                    tm.AcceptTask(local.taskId);
                    ClearOptions(false);
                    ShowAcceptSequence(_currentNpc, local);
                }, td.icon);
            }
        }
        else if (isObjective)
        {
            // 只有在没有可开始任务时才退化为 objective 关闭按钮
            _pendingTalkProgress = true; // 标记需要在关闭时推进
            CreateOption(OptionType.Close, "关闭", OnCloseAfterObjectiveDialogs);
        }

        // 保底关闭按钮（避免重复添加：只有当不是 objective-only 的分支或我们还需要正常关闭时）
        if (!isObjective || (starts != null && starts.Count > 0))
        {
            CreateOption(OptionType.Close, "关闭", OnCloseOptionSelected);
        }
    }

    private void ClearOptions(bool hideContainer = true)
    {
        foreach (var ob in _optionButtons)
        {
            if (ob != null) Destroy(ob.gameObject);
        }
        _optionButtons.Clear();
        if (hideContainer && optionsContainer != null) optionsContainer.gameObject.SetActive(false);
    }

    private void CreateOption(OptionType type, string text, Action onClick, Sprite overrideIcon = null)
    {
        if (optionsContainer == null || optionButtonPrefab == null) return;
        var go = Instantiate(optionButtonPrefab, optionsContainer);
        var npcBtn = go.GetComponent<NpcOptionButton>();
        if (npcBtn == null)
        {
            Destroy(go);
            return;
        }
        npcBtn.Initialize(type, text, onClick, overrideIcon);
        _optionButtons.Add(npcBtn);
    }
    #endregion

    #region 辅助/UI 方法（补充实现）
    private void HideDialog()
    {
        _currentDialogIndex = 0;
        ResetSequenceState();
        ClearOptions();
        var ui = UIManager.Instance;
        if (ui != null) ui.ClosePanel<NpcDialogUI>();
        try { Hide(); } catch (Exception) { /* suppressed */ }
    }

    private void OnCloseOptionSelected() { HideDialog(); }

    private void OnCloseAfterObjectiveDialogs()
    {
        if (_currentNpc != null)
        {
            // 只触发一次
            if ((_pendingTalkProgress || IsCurrentNpcObjectiveTarget()) && !_talkProgressTriggered)
            {
                _talkProgressTriggered = true;
                TaskEvents.TriggerNpcTalked(_currentNpc.NpcData.NpcID);
            }
        }
        _pendingTalkProgress = false;
        HideDialog();
    }

    private bool IsCurrentNpcObjectiveTarget()
    {
        var tm = TaskManager.Instance;
        if (tm == null || _currentNpc == null) return false;
        return tm.IsNpcCurrentObjectiveTarget(_currentNpc.NpcData.NpcID);
    }

    private void ClearPendingRewards()
    {
        if (rewardsContainer == null) return;
        for (int i = rewardsContainer.childCount - 1; i >= 0; i--) Destroy(rewardsContainer.GetChild(i).gameObject);
        if (rewardsContainer.gameObject.activeSelf) rewardsContainer.gameObject.SetActive(false);
    }

    private string BuildTaskDetailDescription(TaskData task)
    {
        if (task == null) return string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine($"任务: {task.taskName}");
        if (!string.IsNullOrEmpty(task.taskDescription)) sb.AppendLine(task.taskDescription);
        if (task.objectives != null && task.objectives.Count > 0)
        {
            sb.AppendLine("目标:");
            foreach (var o in task.objectives)
            {
                sb.Append(" - ");
                switch (o.objectiveType)
                {
                    case ObjectiveType.击杀敌人: sb.Append($"击杀ID={o.targetId} x{o.requiredAmount}"); break;
                    case ObjectiveType.收集物品: sb.Append($"收集物品ID={o.targetId} x{o.requiredAmount}"); break;
                    case ObjectiveType.和Npc对话: sb.Append($"与NPC(ID={o.targetId})对话 {o.requiredAmount} 次"); break;
                }
                sb.AppendLine();
            }
        }
        if (task.rewards != null && task.rewards.Count > 0)
        {
            sb.AppendLine("奖励:");
            foreach (var r in task.rewards) sb.AppendLine($" - {r.rewardType} {(r.amount>0?"x"+r.amount:string.Empty)} {r.rewardDescription}");
        }
        return sb.ToString();
    }

    private string BuildChainPreviewDescription(TaskData task)
    {
        if (task == null) return string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine($"任务: {task.taskName}");
        if (!string.IsNullOrEmpty(task.taskDescription)) sb.AppendLine(task.taskDescription);
        sb.AppendLine("完成任务链后可获得:");
        if (task.chainPreviewRewards != null && task.chainPreviewRewards.Count > 0)
        {
            foreach (var r in task.chainPreviewRewards)
            {
                string line = string.IsNullOrEmpty(r.rewardDescription) ? r.rewardType.ToString() : r.rewardDescription;
                if (r.amount > 0) line += $" x{r.amount}";
                sb.AppendLine(" - " + line);
            }
        }
        sb.AppendLine();
        return sb.ToString();
    }

    private void ShowChainPreviewRewards(TaskData taskData)
    {
        if (taskData == null) return;
        if (rewardsContainer == null || rewardPrefab == null)
        {
            return;
        }
        ClearPendingRewards(); // 清空之前的
        if (taskData.chainPreviewRewards == null || taskData.chainPreviewRewards.Count == 0)
        {
            if (rewardsContainer != null) rewardsContainer.gameObject.SetActive(false);
            return;
        }
        foreach (var r in taskData.chainPreviewRewards)
        {
            try
            {
                var go = Instantiate(rewardPrefab, rewardsContainer);
                var trp = go.GetComponent<TaskRewardPrefab>();
                if (trp != null)
                {
                    trp.Init(r.rewardType, r.amount, r.itemId);
                }
                else
                {
                    var text = go.GetComponentInChildren<TMP_Text>();
                    if (text != null)
                    {
                        text.text = string.IsNullOrEmpty(r.rewardDescription) ? $"{r.rewardType}{(r.amount>0?" x"+r.amount: string.Empty)}" : r.rewardDescription;
                    }
                }
            }
            catch (Exception) { /* suppressed */ }
        }
        if (rewardsContainer != null) rewardsContainer.gameObject.SetActive(true);
    }
    #endregion

    #region 奖励相关
    private bool TryShowPendingRewardsVisually()
    {
        ClearPendingRewards();
        if (rewardsContainer == null || rewardPrefab == null || _objectiveTasks == null) return false;
        bool any = false;
        foreach (var t in _objectiveTasks)
        {
            if (t == null || t.rewards == null) continue;
            foreach (var r in t.rewards)
            {
                try
                {
                    var go = Instantiate(rewardPrefab, rewardsContainer);
                    var trp = go.GetComponent<TaskRewardPrefab>();
                    if (trp != null)
                    {
                        trp.Init(r.rewardType, r.amount, r.itemId);
                    }
                    else
                    {
                        var text = go.GetComponentInChildren<TMP_Text>();
                        if (text != null)
                        {
                            text.text = string.IsNullOrEmpty(r.rewardDescription) ? $"{r.rewardType} {(r.amount>0?"x"+r.amount:string.Empty)}" : r.rewardDescription;
                        }
                    }
                    any = true;
                }
                catch (Exception) { /* suppressed */ }
            }
        }
        if (!any) { ClearPendingRewards(); return false; }
        if (rewardsContainer != null) rewardsContainer.gameObject.SetActive(true);
        return true;
    }

    private string BuildPendingRewardsPreview()
    {
        if (_objectiveTasks == null) return string.Empty;
        var sb = new StringBuilder();
        bool any = false;
        foreach (var t in _objectiveTasks)
        {
            if (t == null || t.rewards == null || t.rewards.Count == 0) continue;
            foreach (var r in t.rewards)
            {
                any = true;
                sb.AppendLine(string.IsNullOrEmpty(r.rewardDescription) ? $"{r.rewardType} {(r.amount>0?"x"+r.amount:string.Empty)}" : r.rewardDescription);
            }
        }
        return any ? sb.ToString() : string.Empty;
    }
    #endregion

    #region UI事件
    public void OnPointerClick(PointerEventData eventData)
    {
        // 如果已经不在任何可推进的对话序列中（基础/临时/任务），则忽略点击，让玩家只能点选项按钮
        if (!_basicDialogsActive && (_tempLines == null || _tempIndex >= _tempLines.Count) && (_objectiveLines == null || _objectiveIndex >= _objectiveLines.Count))
        {
            AudioManager.Instance.PlayUISound(UISoundType.按下按钮); // 仍可播放反馈声音（可根据需要移除）
            return;
        }

        if (_tempLines != null && _tempIndex < _tempLines.Count)
        {
            // 临时序列推进
            if (dialogText != null) dialogText.text = _tempLines[_tempIndex];
            _tempIndex++;
            if (_tempIndex >= _tempLines.Count)
            {
                // 显示完最后一条，立即执行完成回调并锁定点击
                var cb = _tempFinished;
                _tempLines = null; _tempFinished = null; _tempIndex = 0;
                cb?.Invoke();
            }
            AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
            return;
        }
        else if (_objectiveLines != null && _objectiveIndex < _objectiveLines.Count)
        {
            // 任务目标对话推进
            PlayNextObjectiveLine();
            AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
            return;
        }
        else if (_basicDialogsActive)
        {
            var dataForBasic2 = _currentNpc?.NpcData;
            if (dataForBasic2 == null)
            {
                HideDialog();
                return;
            }
            if (dataForBasic2.basicDialogs != null && _currentDialogIndex < dataForBasic2.basicDialogs.Count)
            {
                bool isLast = _currentDialogIndex == dataForBasic2.basicDialogs.Count - 1;
                if (dialogText != null) dialogText.text = dataForBasic2.basicDialogs[_currentDialogIndex];
                _currentDialogIndex++;
                if (isLast)
                {
                    // 显示最后一条时立即生成选项并锁定点击
                    ClearOptions();
                    AddOptionsForNpc(_currentNpc?.NpcData);
                    if (optionsContainer != null) optionsContainer.gameObject.SetActive(true);
                    _basicDialogsActive = false;
                }
                else
                {
                    if (optionsContainer != null) optionsContainer.gameObject.SetActive(false);
                }
            }
            AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
            return;
        }
    }
    #endregion

    #region 商店选项选中回调
    private void OnShopOptionSelected()
    {
        if (_currentNpc == null) return;
        HideDialog();
        var panel = UIManager.Instance.OpenPanel<NpcShopPanel>(out var open);
        if (panel != null && open) panel.Init(_currentNpc);
    }
    #endregion
}
