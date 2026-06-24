using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 任务追踪服务：管理当前被玩家追踪的任务，并提供获取当前目标位置的辅助方法。
/// </summary>
public class TaskTrackingService : Singleton<TaskTrackingService>
{
    [Tooltip("当前追踪的任务ID，-1 表示无")] [SerializeField] private int currentTrackedTaskId = -1;
    public int CurrentTrackedTaskId => currentTrackedTaskId;

    // --- 新增：缓存（仅缓存静态目标位置，动态锚点不缓存以保持实时性） ---
    private int _cachedTaskId = -1;
    private TaskObjective _cachedObjective; // 引用运行时的目标对象
    private Vector3 _cachedWorldPos;
    private string _cachedSceneName;
    private bool _cacheValid;
    private bool _cacheWasDynamic; // 若上次是动态锚点结果则不使用缓存

    private void OnEnable()
    {
        TaskEvents.OnObjectiveProgress += OnObjectiveProgress; // 监听以便完成一个目标后切换下一目标时失效
        TaskEvents.OnTaskTrackedChanged += OnTrackedChangedExternal; // 额外保护（虽然 SetTrackedTask 自己会失效）
    }
    private void OnDisable()
    {
        TaskEvents.OnObjectiveProgress -= OnObjectiveProgress;
        TaskEvents.OnTaskTrackedChanged -= OnTrackedChangedExternal;
    }

    private void InvalidateCache()
    {
        _cacheValid = false;
        _cachedTaskId = -1;
        _cachedObjective = null;
        _cacheWasDynamic = false;
    }

    /// <summary>
    /// 外部调用以强制刷新/失效追踪位置缓存（调试/工具用）。
    /// </summary>
    public void ForceRefreshTrackedPosition()
    {
        InvalidateCache();
    }

    private void OnTrackedChangedExternal(int taskId)
    {
        // 其它地方可能直接触发事件，保险起见失效缓存
        InvalidateCache();
    }

    private void OnObjectiveProgress(ObjectiveType type, int targetId, int amount)
    {
        if (currentTrackedTaskId == -1) return;
        // 若当前缓存的目标已经完成，下一帧应该指向下一个目标 -> 主动检查
        if (!_cacheValid || _cachedObjective == null) return;
        if (_cachedObjective.currentAmount >= _cachedObjective.requiredAmount)
        {
            InvalidateCache();
        }
    }

    /// <summary>设置追踪任务（再次设置同一个会忽略）</summary>
    public void SetTrackedTask(int taskId)
    {
        if (currentTrackedTaskId == taskId) return;
        currentTrackedTaskId = taskId;
        InvalidateCache();
        TaskEvents.TriggerTaskTrackedChanged(currentTrackedTaskId);
        print("追踪的任务ID"+taskId);
    }

    /// <summary>取消追踪</summary>
    public void ClearTrackedTask()
    {
        if (currentTrackedTaskId == -1) return;
        currentTrackedTaskId = -1;
        InvalidateCache();
        TaskEvents.TriggerTaskTrackedChanged(-1);
    }

    /// <summary>
    /// 获取被追踪任务对象。
    /// </summary>
    public BaseTask GetTrackedTask()
    {
        if (currentTrackedTaskId == -1 || TaskManager.Instance == null) return null;
        TaskManager.Instance.tasks.TryGetValue(currentTrackedTaskId, out var task);
        return task;
    }

    /// <summary>
    /// 获取当前追踪任务的"活动目标"（第一个未完成且可追踪的目标；若都完成则返回最后一个可追踪目标）。
    /// </summary>
    public TaskObjective GetActiveObjective(BaseTask task)
    {
        if (task == null) return null;
        foreach (var o in task.objectives)
        {
            if (o.canTrack && o.currentAmount < o.requiredAmount)
                return o;
        }
        // 所有完成：返回最后一个可追踪
        for (int i = task.objectives.Count - 1; i >= 0; i--)
        {
            if (task.objectives[i].canTrack) return task.objectives[i];
        }
        return null;
    }

    /// <summary>
    /// 尝试获取当前追踪目标的世界坐标。
    /// 1) 动态锚点 TaskTargetAnchor
    /// 2) 静态坐标（TaskObjective.staticWorldPosition）
    /// </summary>
    public bool TryGetTrackedObjectiveWorldPosition(out Vector3 worldPos, out string sceneName)
    {
        worldPos = Vector3.zero; sceneName = string.Empty;
        var task = GetTrackedTask();
        if (task == null) return false;
        var objective = GetActiveObjective(task);
        if (objective == null) return false;

        // 兼容修复：旧版本已生成的 BaseTask 未深拷贝静态追踪字段，需要按需补齐
        if (NeedsPatch(objective))
        {
            TryPatchObjectiveFromSourceData(task.id, objective);
        }

        // 若缓存可用并且仍然指向同一 task & objective 且不是动态结果
        if (_cacheValid && !_cacheWasDynamic && _cachedTaskId == currentTrackedTaskId && _cachedObjective == objective)
        {
            worldPos = _cachedWorldPos;
            sceneName = _cachedSceneName;
            return true;
        }

        // 先找动态锚点（动态锚点不缓存 —— 其位置可能变化）
        var anchors = global::TaskTargetAnchor.GetAnchors(objective.objectiveType, objective.targetId);
        if (anchors != null && anchors.Count > 0)
        {
            var player = CharacterService.Instance?.CurrentPlayerCharacter();
            if (player != null)
            {
                Transform best = null; float bestSqr = float.MaxValue;
                foreach (var a in anchors)
                {
                    if (a == null) continue;
                    float sq = (player.transform.position - a.transform.position).sqrMagnitude;
                    if (sq < bestSqr) { bestSqr = sq; best = a.transform; }
                }
                if (best != null)
                {
                    worldPos = best.position;
                    sceneName = best.gameObject.scene.name;
                    _cacheWasDynamic = true; // 标记本次是动态
                    _cacheValid = false;     // 不缓存动态位置
                    return true;
                }
            }
            else if (anchors[0] != null)
            {
                worldPos = anchors[0].transform.position;
                sceneName = anchors[0].gameObject.scene.name;
                _cacheWasDynamic = true;
                _cacheValid = false;
                return true;
            }
        }

        // 静态：直接使用 objective.staticWorldPosition + sceneName
        worldPos = objective.staticWorldPosition;
        if (!string.IsNullOrEmpty(objective.staticSceneName))
        {
            sceneName = objective.staticSceneName;
        }
        else
        {
            sceneName = SceneManager.GetActiveScene().name;
        }

        // 写入缓存
        _cachedTaskId = currentTrackedTaskId;
        _cachedObjective = objective;
        _cachedWorldPos = worldPos;
        _cachedSceneName = sceneName;
        _cacheWasDynamic = false;
        _cacheValid = true;
        return true; // 即便是零向量也返回 true
    }

    /// <summary>
    /// 计算箭头应指向的目标（返回平方距离的快速版本以避免每次调用开方运算）。
    /// 返回是否找到目标；insideRange 表示玩家已在目标范围内（此时可显示“已到达”）。
    /// distanceSqr 是平方距离（用于避免频繁开方）。
    /// </summary>
    public bool TryGetArrowTargetFast(out Vector3 worldPos, out bool insideRange, out float distanceSqr, out float requiredRange, out string targetScene, out string hint)
    {
        worldPos = Vector3.zero; insideRange = false; distanceSqr = 0f; requiredRange = 0f; targetScene = string.Empty; hint = string.Empty;
        var task = GetTrackedTask();
        if (task == null) { hint = "未追踪任务"; return false; }
        var objective = GetActiveObjective(task);
        if (objective == null || !objective.canTrack) { hint = "无可追踪目标"; return false; }

        if (!TryGetTrackedObjectiveWorldPosition(out var objPos, out var objScene)) { hint = "无目标位置"; return false; }
        targetScene = objScene;

        var activeScene = SceneManager.GetActiveScene().name;
        var player = CharacterService.Instance?.CurrentPlayerCharacter();
        if (player == null) { hint = "无玩家"; return false; }

        var playerPos = player.transform.position;

        // 场景不同：寻找传送点
        if (activeScene != objScene)
        {
            var tp = CharacterService.Instance?.currentMapManager.teleportPoint;
            if (tp != null)
            {
                worldPos = tp.transform.position;
                distanceSqr = (playerPos - worldPos).sqrMagnitude;
                requiredRange = 2f; // 传送点默认到达判定
                insideRange = distanceSqr <= requiredRange * requiredRange;
                hint = insideRange ? "进入传送点" : $"前往传送点 -> {objScene}";
                return true;
            }
            // 没有传送点，仍指向真实目标（但提示需要切场景）
            worldPos = objPos;
            distanceSqr = (playerPos - worldPos).sqrMagnitude;
            requiredRange = objective.trackRangeRadius;
            insideRange = false;
            hint = $"目标在场景 {objScene}";
            return true;
        }

        // 同场景：直接指向目标
        worldPos = objPos;
        distanceSqr = (playerPos - worldPos).sqrMagnitude;
        requiredRange = Mathf.Max(0.1f, objective.trackRangeRadius);
        insideRange = distanceSqr <= requiredRange * requiredRange;
        hint = insideRange ? "已到达目标区域" : "前往任务目标";
        return true;
    }

    /// <summary>
    /// 向后兼容接口：调用快速版本并在返回前计算线性距离。
    /// </summary>
    public bool TryGetArrowTarget(out Vector3 worldPos, out bool insideRange, out float distance, out float requiredRange, out string targetScene, out string hint)
    {
        distance = 0f; worldPos = Vector3.zero; insideRange = false; requiredRange = 0f; targetScene = string.Empty; hint = string.Empty;
        if (!TryGetArrowTargetFast(out worldPos, out insideRange, out float distanceSqr, out requiredRange, out targetScene, out hint))
            return false;
        distance = Mathf.Sqrt(distanceSqr);
        return true;
    }

    /// <summary>
    /// 检查指定场景是否包含可追踪的任务目标。
    /// </summary>
    public bool SceneHasAnyObjective(string sceneName, out bool hasTrackedObjective)
    {
        hasTrackedObjective = false;
        if (TaskManager.Instance == null || string.IsNullOrEmpty(sceneName)) return false;
        bool any = false;
        var trackedTask = GetTrackedTask();
        foreach (var task in TaskManager.Instance.tasks.Values)
        {
            foreach (var obj in task.objectives)
            {
                if (!obj.canTrack) continue;
                // 检查静态场景
                string objScene = obj.staticSceneName;
                if (string.IsNullOrEmpty(objScene))
                {
                    // 如果未填写静态场景名，只有在玩家当前场景匹配时才算（避免全部都亮）
                    objScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                }
                bool matched = objScene == sceneName;
                if (!matched)
                {
                    // 检查动态锚点
                    var anchors = global::TaskTargetAnchor.GetAnchors(obj.objectiveType, obj.targetId);
                    if (anchors != null)
                    {
                        foreach (var a in anchors)
                        {
                            if (a != null && a.gameObject.scene.name == sceneName)
                            {
                                matched = true;
                                break;
                            }
                        }
                    }
                }
                if (matched)
                {
                    any = true;
                    if (trackedTask != null && trackedTask == task)
                    {
                        hasTrackedObjective = true;
                    }
                }
            }
        }
        return any;
    }

    // 判断是否需要补丁：仅在静态位置/场景名可能丢失并且目标可追踪时尝试
    private bool NeedsPatch(TaskObjective obj)
    {
        // 如果 canTrack 仍为默认 true 且 位置为(0,0,0)，无法仅凭数字判断是否配置过，因此再看 trackRangeRadius 是否为默认5f
        // 这里只做一个启发式：如果所有字段都保持默认值，就尝试一次 Patch
        return obj != null && obj.staticWorldPosition == Vector3.zero && string.IsNullOrEmpty(obj.staticSceneName);
    }

    private void TryPatchObjectiveFromSourceData(int taskId, TaskObjective runtimeObj)
    {
        if (TaskManager.Instance == null || TaskManager.Instance.TaskDataSO == null) return;
        var data = TaskManager.Instance.FindTaskDataById(taskId);
        if (data == null || data.objectives == null) return;
        // 根据 objectiveType + targetId 匹配（假设组合具备唯一性）
        foreach (var src in data.objectives)
        {
            if (src.objectiveType == runtimeObj.objectiveType && src.targetId == runtimeObj.targetId)
            {
                // 仅在运行时仍为默认时才覆盖，避免误改已更新数据
                if (runtimeObj.staticWorldPosition == Vector3.zero) runtimeObj.staticWorldPosition = src.staticWorldPosition;
                if (string.IsNullOrEmpty(runtimeObj.staticSceneName)) runtimeObj.staticSceneName = src.staticSceneName;
                if (Mathf.Approximately(runtimeObj.trackRangeRadius, 0f) || Mathf.Approximately(runtimeObj.trackRangeRadius, 5f))
                    runtimeObj.trackRangeRadius = src.trackRangeRadius;
                runtimeObj.canTrack = src.canTrack; // 保底同步
                break;
            }
        }
    }
}
