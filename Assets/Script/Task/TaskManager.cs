using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-500)]
public class TaskManager : Singleton<TaskManager>
{
    [SerializeField] private TaskDataSO taskDataSo; // 任务数据SO

    // 运行时任务字典（仅保留仍在进行 / 未完成或未自动移除的任务）
    public Dictionary<int, BaseTask> tasks = new Dictionary<int, BaseTask>();

    // 进度标志与全局计数器（保留）
    private Dictionary<string, bool> _progressFlags = new Dictionary<string, bool>();
    public Dictionary<string, int> GlobalCounters = new Dictionary<string, int>();

    public TaskDataSO TaskDataSO => taskDataSo;

    // 当前已经初始化过的角色ID（用于防止在同一角色跨场景再次 Spawn 时重复初始化/清空）
    private string _initializedCharacterId;

    private void OnEnable() => SubscribeToEvents();
    // 移除 Start 中的自动初始化，改为在玩家真正生成后由 MapManager 调用 InitializeForCurrentCharacter()
    private void OnDisable() => UnsubscribeFromEvents();

    /// <summary>
    /// 在玩家角色真正生成 (MapManager.SpawnPlayer 之后) 调用，基于当前 GameManager.CurrentCharacter 初始化任务。
    /// 1. 仅当新角色或从未初始化过时才会真正执行加载。
    /// 2. 跨场景同一角色再次 Spawn 不会重复清空/重新添加任务，避免任务进度丢失。
    /// 3. 若存档没有任务则自动接取首个主线任务。
    /// </summary>
    public void InitializeForCurrentCharacter(bool force = false)
    {
        var currentChar = SessionManager.Instance.CurrentCharacter;
        if (currentChar == null) return;
        var charData = currentChar;

        bool isSameCharacter = !force && _initializedCharacterId == charData.Id;
        if (isSameCharacter)
        {
            // 同一角色跨场景二次生成：只需广播现有任务（让 UI/监听者重新获得状态）
            BroadcastExistingTasks();
            return;
        }

        // 新角色（或强制重载）——重置内部运行时状态
        tasks.Clear();
        _progressFlags.Clear();
        GlobalCounters.Clear();
        _initializedCharacterId = charData.Id;

        // 从角色数据装载进行中的任务
        LoadTasksFromCharacterData();

        // 若没有任何进行中的任务，则初始化首个主线
        if (tasks.Count == 0)
        {
            InitializeMainMissions();
        }

        BroadcastExistingTasks();
    }

    public void InitializeMainMissions()
    {
        if (taskDataSo == null || taskDataSo.mainMission == null || taskDataSo.mainMission.Count == 0) return;
        var curChar = GameManager.Instance?.CurrentCharacter;
        bool hasRuntimeMainQuest = tasks.Values.Any(t => t.taskCategory == TaskCategory.MainQuest);
        bool hasPersistedAny = curChar != null && curChar.taskList != null && curChar.taskList.Count > 0;
        if (hasRuntimeMainQuest || hasPersistedAny) return;
        var first = taskDataSo.mainMission.FirstOrDefault(t => t.prerequisiteTaskId == -1) ?? taskDataSo.mainMission[0];
        AcceptTask(first.taskId, true);
    }

    public void AcceptTask(int taskId, bool isAuto = false)
    {
        if (tasks.ContainsKey(taskId)) return;
        var data = FindTaskDataById(taskId);
        if (data == null)
        {
            Debug.LogError($"未找到任务数据 ID={taskId}");
            return;
        }
        var curChar = GameManager.Instance?.CurrentCharacter;
        // 若已完成过（在 completedTaskIds 中）则不再接受
        if (curChar != null && curChar.completedTaskIds != null && curChar.completedTaskIds.Contains(taskId))
        {
            if (!isAuto) Debug.Log($"任务 {taskId} 已完成过，不再接受");
            return;
        }
        if (data.prerequisiteTaskId != -1)
        {
            bool preFinished = false;
            if (tasks.TryGetValue(data.prerequisiteTaskId, out var preTask))
            {
                preFinished = preTask.isCompleted && preTask.isRewardClaimed;
            }
            else if (curChar != null && curChar.completedTaskIds != null && curChar.completedTaskIds.Contains(data.prerequisiteTaskId))
            {
                preFinished = true;
            }
            if (!preFinished)
            {
                if (!isAuto) Debug.LogWarning($"前置任务 {data.prerequisiteTaskId} 未完成，无法接受任务 {taskId}");
                return;
            }
        }
        var runtimeTask = new BaseTask(data);
        tasks.Add(taskId, runtimeTask);

        // 持久化：写入 CharacterData.taskList（只记录首个目标的进度）
        if (curChar != null)
        {
            if (curChar.taskList == null) curChar.taskList = new System.Collections.Generic.List<TaskLiteData>();
            if (!curChar.taskList.Any(t => t.taskId == taskId))
                curChar.taskList.Add(new TaskLiteData(taskId, 0));
        }

        TaskEvents.TriggerTaskStarted(taskId);
        Debug.Log($"接受任务: {runtimeTask.taskName} (ID={taskId})");
    }

    public TaskData FindTaskDataById(int taskId)
    {
        if (taskDataSo == null) return null;
        foreach (var t in taskDataSo.mainMission) if (t.taskId == taskId) return t;
        foreach (var t in taskDataSo.sideMission) if (t.taskId == taskId) return t;
        return null;
    }

    public bool CheckIfNpcHasRelatedTasks(int npcId)
    {
        if (taskDataSo == null) return false;
        foreach (var t in taskDataSo.mainMission) if (t.relatedNpcId == npcId) return true;
        foreach (var t in taskDataSo.sideMission) if (t.relatedNpcId == npcId) return true;
        return false;
    }

    public List<TaskData> GetRelatedTasksForNpc(int npcId)
    {
        var list = new List<TaskData>();
        if (taskDataSo == null) return list;
        foreach (var t in taskDataSo.mainMission) if (t.relatedNpcId == npcId) list.Add(t);
        foreach (var t in taskDataSo.sideMission) if (t.relatedNpcId == npcId) list.Add(t);
        return list;
    }

    public bool IsNpcCurrentObjectiveTarget(int npcId)
    {
        foreach (var task in tasks.Values)
        {
            if (task.isCompleted) continue;
            foreach (var obj in task.objectives)
            {
                if (obj.objectiveType == ObjectiveType.和Npc对话 && obj.targetId == npcId && obj.currentAmount < obj.requiredAmount)
                    return true;
            }
        }
        return false;
    }

    public List<BaseTask> GetActiveTalkTasksForNpc(int npcId)
    {
        var result = new List<BaseTask>();
        foreach (var task in tasks.Values)
        {
            if (task.isCompleted) continue;
            foreach (var obj in task.objectives)
            {
                if (obj.objectiveType == ObjectiveType.和Npc对话 && obj.targetId == npcId && obj.currentAmount < obj.requiredAmount)
                {
                    result.Add(task);
                    break;
                }
            }
        }
        return result;
    }

    public List<TaskData> GetStartTasksForNpc(int npcId)
    {
        var list = new List<TaskData>();
        if (taskDataSo == null)
        {
            Debug.LogWarning($"[TaskManager][GetStartTasksForNpc] taskDataSo is null, npcId={npcId}");
            return list;
        }
        var all = (taskDataSo.mainMission ?? new List<TaskData>()).Concat(taskDataSo.sideMission ?? new List<TaskData>());
        var curChar = GameManager.Instance?.CurrentCharacter;
        foreach (var td in all)
        {
            if (td == null) continue;
            if (td.startNpcId != npcId)
            {
                // 不是这个NPC启动
                continue;
            }
            // 记录初步匹配
            string reason = "OK";
            bool add = true;

            if (tasks.ContainsKey(td.taskId))
            {
                reason = $"RUNTIME_ALREADY_ACCEPTED isCompleted={tasks[td.taskId].isCompleted} rewardClaimed={tasks[td.taskId].isRewardClaimed}";
                add = false;
            }
            else if (curChar != null && curChar.completedTaskIds != null && curChar.completedTaskIds.Contains(td.taskId))
            {
                reason = "ALREADY_COMPLETED";
                add = false;
            }
            // prerequisite check
            if (add && td.prerequisiteTaskId != -1)
            {
                bool preFinished = false;
                if (tasks.TryGetValue(td.prerequisiteTaskId, out var pre)) preFinished = pre.isCompleted && pre.isRewardClaimed;
                else if (curChar != null && curChar.completedTaskIds != null && curChar.completedTaskIds.Contains(td.prerequisiteTaskId)) preFinished = true;
                if (!preFinished)
                {
                    reason = $"PREREQUISITE_NOT_FINISHED preId={td.prerequisiteTaskId}";
                    add = false;
                }
            }

            if (add)
            {
                list.Add(td);
            }
        }
        return list;
    }

    public List<TaskData> GetFallbackStartTasksForNpc(int npcId)
    {
        var list = new List<TaskData>();
        if (taskDataSo == null) return list;
        var all = (taskDataSo.mainMission ?? new List<TaskData>()).Concat(taskDataSo.sideMission ?? new List<TaskData>());
        var curChar = GameManager.Instance?.CurrentCharacter;
        foreach (var td in all)
        {
            if (td == null) continue;
            if (td.startNpcId != -1) continue; // 只处理未显式指定 startNpcId 的
            if (td.relatedNpcId != npcId) continue;
            // 已经拥有或已完成跳过
            if (tasks.ContainsKey(td.taskId)) continue;
            if (curChar != null && curChar.completedTaskIds != null && curChar.completedTaskIds.Contains(td.taskId)) continue;
            // 前置判断
            bool preOk = true;
            if (td.prerequisiteTaskId != -1)
            {
                if (tasks.TryGetValue(td.prerequisiteTaskId, out var pre)) preOk = pre.isCompleted && pre.isRewardClaimed;
                else if (curChar != null && curChar.completedTaskIds != null && curChar.completedTaskIds.Contains(td.prerequisiteTaskId)) preOk = true;
                else preOk = false;
            }
            if (!preOk) continue;
            list.Add(td);
        }
        if (list.Count > 0)
        {
            Debug.Log($"[TaskManager][GetFallbackStartTasksForNpc] npcId={npcId} found {list.Count} fallback tasks by relatedNpcId");
        }
        return list;
    }

    private void SubscribeToEvents()
    {
        TaskEvents.OnObjectiveProgress += HandleObjectiveProgress;
        TaskEvents.OnTaskCompleted += HandleTaskCompletedChain;
        TaskEvents.OnTaskRewardsClaimed += HandleTaskRewardsClaimedRemoval; // 新增：奖励领取后移除
    }
    private void UnsubscribeFromEvents()
    {
        TaskEvents.OnObjectiveProgress -= HandleObjectiveProgress;
        TaskEvents.OnTaskCompleted -= HandleTaskCompletedChain;
        TaskEvents.OnTaskRewardsClaimed -= HandleTaskRewardsClaimedRemoval;
    }

    private void HandleObjectiveProgress(ObjectiveType objectiveType, int targetId, int amount)
    {
        var curChar = GameManager.Instance?.CurrentCharacter;
        // 避免遍历过程中结构变化
        var snapshot = tasks.Values.ToArray();
        foreach (var task in snapshot)
        {
            if (task.isCompleted) continue;
            task.UpdateObjectiveProgress(objectiveType, targetId, amount);
            if (curChar != null && curChar.taskList != null && task.objectives.Count > 0)
            {
                var lite = curChar.taskList.FirstOrDefault(t => t.taskId == task.id);
                if (lite != null)
                {
                    lite.progress = task.objectives[0].currentAmount;
                }
            }
        }
    }

    private void HandleTaskCompletedChain(int completedTaskId)
    {
        // 在自动领取奖励的流程中, BaseTask.CompleteTask 会先 ClaimRewards -> 触发移除运行时任务, 然后才触发 TaskCompleted 事件
        // 因此这里有可能在 tasks 字典中已找不到该任务, 需要使用 TaskData 查找 nextTaskId
        int nextId = -1;
        BaseTask completed = null;
        if (tasks.TryGetValue(completedTaskId, out completed))
        {
            nextId = completed.nextTaskId;
        }
        else
        {
            // 任务运行时已被移除(奖励已领取), 回退到 TaskDataSO 查 nextTaskId
            var td = FindTaskDataById(completedTaskId);
            if (td != null) nextId = td.nextTaskId;
        }

        if (nextId != -1)
        {
            // 前置任务已进入 completedTaskIds (在 HandleTaskRewardsClaimedRemoval 中记录), 可直接尝试自动接受
            if (!tasks.ContainsKey(nextId))
            {
                AcceptTask(nextId, true);
            }
            // 自动切换追踪到下一个任务
            var trackingSvc = TaskTrackingService.Instance;
            if (trackingSvc != null)
            {
                trackingSvc.SetTrackedTask(nextId);
            }
        }
        else
        {
            // 没有后续任务, 如果当前追踪的是这个已完成的任务, 清空追踪
            var trackingSvc = TaskTrackingService.Instance;
            if (trackingSvc != null && trackingSvc.CurrentTrackedTaskId == completedTaskId)
            {
                trackingSvc.ClearTrackedTask();
            }
        }
    }

    private void HandleTaskRewardsClaimedRemoval(int taskId)
    {
        // 移除已完成并已领取奖励的任务，记录到 completedTaskIds
        if (!tasks.TryGetValue(taskId, out var finished)) return;
        if (!finished.isCompleted || !finished.isRewardClaimed) return; // 保护
        var curChar = GameManager.Instance?.CurrentCharacter;
        if (curChar != null)
        {
            if (curChar.completedTaskIds == null) curChar.completedTaskIds = new System.Collections.Generic.List<int>();
            if (!curChar.completedTaskIds.Contains(taskId)) curChar.completedTaskIds.Add(taskId);
            // 从活动列表中移除
            if (curChar.taskList != null) curChar.taskList.RemoveAll(t => t.taskId == taskId);
        }
        tasks.Remove(taskId);
        // 如果追踪中的是此任务，清空追踪
        var tracking = TaskTrackingService.Instance;
        if (tracking != null && tracking.CurrentTrackedTaskId == taskId)
        {
            tracking.ClearTrackedTask();
        }
        Debug.Log($"任务 {taskId} 已完成并领取奖励，已从活动任务中移除");
    }

    public void LoadTasksFromCharacterData()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.CurrentCharacter == null) return;
        var cd = gm.CurrentCharacter;
        if (cd.taskList == null || cd.taskList.Count == 0) return;
        var listSnapshot = cd.taskList.ToArray();
        foreach (var lite in listSnapshot)
        {
            if (lite == null) continue;
            // 跳过已在 completedTaskIds 中（说明曾完成，不需要重新创建运行时任务）
            if (cd.completedTaskIds != null && cd.completedTaskIds.Contains(lite.taskId)) continue;
            if (!tasks.ContainsKey(lite.taskId)) AcceptTask(lite.taskId, true);
            if (tasks.TryGetValue(lite.taskId, out var rt))
            {
                if (rt.objectives.Count > 0)
                {
                    var obj = rt.objectives[0];
                    obj.currentAmount = Mathf.Clamp(lite.progress, 0, obj.requiredAmount);
                    if (rt.CheckAllObjectivesCompletion()) rt.CompleteTask();
                }
            }
        }
    }

    public void PopulateCharacterDataTasks(CharacterData data)
    {
        if (data == null) return;
        if (data.taskList == null) data.taskList = new List<TaskLiteData>();
        data.taskList.Clear();
        foreach (var kv in tasks)
        {
            var t = kv.Value;
            // 只保存仍在进行中的任务
            if (t.isCompleted && t.isRewardClaimed) continue;
            int prog = 0;
            if (t.objectives.Count > 0) prog = t.objectives[0].currentAmount;
            data.taskList.Add(new TaskLiteData(t.id, prog));
        }
        // completedTaskIds 已持久化在 data.completedTaskIds
    }

    public void SaveTaskProgressToMongoDB(string characterId)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.CurrentCharacter == null) return;
        PopulateCharacterDataTasks(gm.CurrentCharacter);
        _ = MongoDBManager.Instance.CreateAndSaveCharacterData(gm.CurrentCharacter);
    }

    public void LoadTaskProgressFromMongoDB(string characterId)
    {
        LoadTasksFromCharacterData();
    }

    public void BroadcastExistingTasks()
    {
        foreach (var kv in tasks)
        {
            TaskEvents.TriggerTaskStarted(kv.Key);
        }
    }

    public void DumpAllTaskData()
    {
        if (taskDataSo == null)
        {
            Debug.LogWarning("[TaskManager][DumpAllTaskData] taskDataSo null");
            return;
        }
        var all = (taskDataSo.mainMission ?? new List<TaskData>()).Concat(taskDataSo.sideMission ?? new List<TaskData>()).ToList();
        Debug.Log($"[TaskManager][DumpAllTaskData] total={all.Count}");
        foreach (var td in all)
        {
            if (td == null) continue;
            Debug.Log($"[TaskManager][DumpAllTaskData] taskId={td.taskId} name={td.taskName} startNpcId={td.startNpcId} relatedNpcId={td.relatedNpcId} pre={td.prerequisiteTaskId} next={td.nextTaskId}");
        }
    }
}
