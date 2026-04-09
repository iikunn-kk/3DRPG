using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TaskTrackerPanel : MonoBehaviour
{
    [Header("行预制体(包含 TaskTrackerLine)")] [SerializeField] private TaskTrackerLine linePrefab;
    [Header("列表父节点 (只包含任务行)")] [SerializeField] private Transform contentParent;
    [Header("最大显示任务条数 (-1 表示全部)")] [SerializeField] private int maxLines = 5;
    [Header("自动隐藏已完成任务")] [SerializeField] private bool hideCompleted;

    [Header("折叠相关 (可选) ")]
    [Tooltip("需要被折叠/展开动画的内容容器(不要包含头部Button)")]
    [SerializeField] private CanvasGroup containerCanvasGroup; // 用于淡入淡出（并控制交互）
    [SerializeField] private Animator animator; // 用于播放折叠/展开动画（通过 trigger）
    [Tooltip("Animator 打开 trigger 名称")] [SerializeField] private string openTrigger = "Open";
    [Tooltip("Animator 折叠 trigger 名称")] [SerializeField] private string closeTrigger = "Close";
    [Tooltip("动画播放等待时长(秒)，若 Animator 状态机中动画长度不固定，可在这里调整等待时间)")]
    [SerializeField] private float collapseAnimationDuration = 0.25f;
    [Tooltip("是否在 Init 时默认折叠")][SerializeField] private bool startCollapsed;

    [Header("自动刷新间隔(秒)")] [SerializeField] private float autoRefreshInterval = 0.6f;

    private float _refreshTimer;

    private readonly List<TaskTrackerLine> _lines = new List<TaskTrackerLine>();
    private bool _isCollapsed;
    private bool _initialized;

    // Icon pulse
    private Coroutine _iconPulseRoutine;

    #region Unity
    private void OnEnable()
    {
        if (!_initialized)
        {
            Init();
        }
        SubscribeTaskEvents();
        _refreshTimer = 0f;
    }

    private void OnDestroy()
    {
        UnsubscribeTaskEvents();
        if (_iconPulseRoutine != null)
            StopCoroutine(_iconPulseRoutine);
    }

    private void Update()
    {
        // 周期刷新：用于距离/箭头信息的文本更新（避免频繁 GC 可适当加大间隔）
        _refreshTimer += Time.unscaledDeltaTime;
        if (_refreshTimer >= autoRefreshInterval)
        {
            _refreshTimer = 0f;
            Refresh();
        }
    }
    #endregion

    public void Init()
    {
        if (_initialized) return;
        _initialized = true;


        // 初始化 CanvasGroup 与 container 显示状态
        if (containerCanvasGroup != null)
        {
            containerCanvasGroup.alpha = startCollapsed ? 0f : 1f;
            containerCanvasGroup.interactable = !startCollapsed;
            containerCanvasGroup.blocksRaycasts = !startCollapsed;
        }

        _isCollapsed = startCollapsed;

        UpdateHeaderSummary();
        Rebuild();
    }

    #region Event Subscriptions
    private void SubscribeTaskEvents()
    {
        // 避免重复订阅
        UnsubscribeTaskEvents();
        TaskEvents.OnObjectiveProgress += HandleObjectiveProgress;
        TaskEvents.OnTaskStarted += HandleTaskStarted;
        TaskEvents.OnTaskCompleted += HandleTaskCompletedEvent;
        TaskEvents.OnTaskRewardsClaimed += HandleTaskRewardsClaimed;
        TaskEvents.OnTaskTrackedChanged += HandleTaskTrackedChanged; // 新增
    }

    private void UnsubscribeTaskEvents()
    {
        TaskEvents.OnObjectiveProgress -= HandleObjectiveProgress;
        TaskEvents.OnTaskStarted -= HandleTaskStarted;
        TaskEvents.OnTaskCompleted -= HandleTaskCompletedEvent;
        TaskEvents.OnTaskRewardsClaimed -= HandleTaskRewardsClaimed;
        TaskEvents.OnTaskTrackedChanged -= HandleTaskTrackedChanged; // 新增
    }
    #endregion

    #region TaskEvents Handlers
    private void HandleObjectiveProgress(ObjectiveType type, int targetId, int amount)
    {
        Refresh();
    }
    private void HandleTaskStarted(int taskId)
    {
        Rebuild();
    }
    private void HandleTaskCompletedEvent(int taskId)
    {
        // 为了避免事件顺序导致的 race（TaskManager 可能也在 OnTaskCompleted 中接取下一个任务并切换追踪），
        // 我们延后一帧处理，让 TaskManager 先完成它的链式逻辑。
        StartCoroutine(HandleTaskCompletedCoroutine(taskId));
    }

    private IEnumerator HandleTaskCompletedCoroutine(int completedTaskId)
    {
        // 等一帧以让 TaskManager 的 HandleTaskCompletedChain 先执行（若订阅顺序不同会造成 race）
        yield return null;

        if (TaskManager.Instance == null)
        {
            Rebuild();
            yield break;
        }

        TaskManager tm = TaskManager.Instance;
        // 尝试找到对应行（行此时仍指向旧任务 ID）
        TaskTrackerLine line = _lines.FirstOrDefault(l => l != null && l.TaskId == completedTaskId);

        // 尝试拿运行时任务（可能已被自动领取奖励逻辑移除）
        tm.tasks.TryGetValue(completedTaskId, out var completedTaskRuntime);
        int nextId = -1;
        if (completedTaskRuntime != null)
        {
            nextId = completedTaskRuntime.nextTaskId;
        }
        else
        {
            // 运行时可能已被移除（自动领奖励），回退 TaskDataSO 查 nextTaskId
            var td = tm.FindTaskDataById(completedTaskId);
            if (td != null) nextId = td.nextTaskId;
        }

        // 如果存在下一个任务并且已被 TaskManager 自动接受
        if (nextId != -1 && tm.tasks.TryGetValue(nextId, out var nextTaskRuntime))
        {
            if (line != null)
            {
                line.SetData(nextTaskRuntime, OnLineClicked);
            }
            else
            {
                Rebuild();
            }
            UpdateLinesTrackedState();
            Refresh();
            UpdateHeaderSummary();
            yield break;
        }

        // 没有后续任务：移除该行并刷新
        if (line != null)
        {
            if (line.gameObject != null) Destroy(line.gameObject);
            _lines.Remove(line);
        }
        else
        {
            Rebuild();
        }
        UpdateLinesTrackedState();
        Refresh();
        UpdateHeaderSummary();
    }
    private void HandleTaskRewardsClaimed(int taskId)
    {
        Refresh();
    }
    private void HandleTaskTrackedChanged(int taskId)
    {
        // 只需要刷新行的高亮与距离显示
        UpdateLinesTrackedState();
        Refresh();
    }
    #endregion

    /// <summary>
    /// 完全重建列表（任务增减时）
    /// </summary>
    private void Rebuild()
    {
        foreach (var l in _lines)
        {
            if (l != null) Destroy(l.gameObject);
        }
        _lines.Clear();
        if (TaskManager.Instance == null || linePrefab == null || contentParent == null)
        {
            UpdateHeaderSummary();
            return;
        }
        var all = TaskManager.Instance.tasks.Values.ToList();
        // 排序：主线优先 -> 支线 -> ID
        all.Sort((a,b)=>
        {
            int cat = a.taskCategory.CompareTo(b.taskCategory);
            if (cat != 0) return cat;
            return a.id.CompareTo(b.id);
        });
        if (hideCompleted)
            all = all.Where(t=>!t.isCompleted).ToList();
        if (maxLines > 0)
            all = all.Take(maxLines).ToList();
        foreach (var t in all)
        {
            var line = Instantiate(linePrefab, contentParent);
            // 注入点击回调，由 Panel 统一处理追踪逻辑
            line.SetData(t, OnLineClicked);
            _lines.Add(line);
        }

        // 同步高亮状态
        UpdateLinesTrackedState();

        UpdateHeaderSummary();
    }

    /// <summary>
    /// 刷新已存在行（进度变化 or 领奖励）
    /// </summary>
    private void Refresh()
    {
        if (TaskManager.Instance == null)
            return;
        if (hideCompleted)
        {
            // 如果隐藏已完成，发现有已完成任务则重建
            bool needRebuild = _lines.Any(l => l != null && l.gameObject != null && l.enabled && l.gameObject.activeSelf && TaskManager.Instance.tasks.TryGetValue(l.TaskId, out var t) && t.isCompleted);
            if (needRebuild)
            {
                Rebuild();
                return;
            }
        }
        foreach (var l in _lines)
        {
            if (l != null)
                l.Refresh();
        }
        UpdateHeaderSummary();
    }

    /// <summary>
    /// 头部按钮绑定：折叠/展开（通过 Animator trigger + 等待动画结束后设置 CanvasGroup）
    /// </summary>
    public void ToggleCollapse()
    {
        _isCollapsed = !_isCollapsed;

        if (animator != null)
        {
            animator.SetTrigger(_isCollapsed ? closeTrigger : openTrigger);
        }

        // 启动协程在动画结束后设置 CanvasGroup 和可交互性
        if (containerCanvasGroup != null)
        {
            StartCoroutine(WaitForCollapseAnimationAndApply(_isCollapsed));
        }

        UpdateHeaderSummary();
    }

    private IEnumerator WaitForCollapseAnimationAndApply(bool collapsed)
    {
        // 如果有动画器并且 duration > 0，则等待指定时长，否则一帧后立即应用
        if (collapseAnimationDuration > 0f)
            yield return new WaitForSeconds(collapseAnimationDuration);
        else
            yield return null;

        if (containerCanvasGroup != null)
        {
            containerCanvasGroup.alpha = collapsed ? 0f : 1f;
            containerCanvasGroup.interactable = !collapsed;
            containerCanvasGroup.blocksRaycasts = !collapsed;
        }
    }

    /// <summary>
    /// 更新头部摘要：任务总数 / 可领取奖励数
    /// </summary>
    private void UpdateHeaderSummary()
    {
        if (TaskManager.Instance == null) return;
        int total = TaskManager.Instance.tasks.Count;
        int claimable =  TaskManager.Instance.tasks.Values.Count(t=>t.isCompleted && !t.isRewardClaimed);
  
      
    }


    /// <summary>
    /// 当某一行被点击，由 Panel 统一处理追踪逻辑：
    /// - 如果当前已追踪该任务 => 取消追踪
    /// - 如果当前追踪其它任务 => 切换到该任务
    /// - 如果当前没追踪任何任务 => 开始追踪该任务
    /// </summary>
    private void OnLineClicked(int taskId)
    {
        var svc = TaskTrackingService.Instance;
        if (svc == null) return;
        if (svc.CurrentTrackedTaskId == taskId)
            svc.ClearTrackedTask();
        else
            svc.SetTrackedTask(taskId);

        // 同步行高亮状态
        UpdateLinesTrackedState();
        Refresh();
        UpdateHeaderSummary();
    }

    /// <summary>
    /// 同步各行的高亮状态（由 TaskTrackingService 决定当前被追踪的任务）
    /// </summary>
    private void UpdateLinesTrackedState()
    {
        int cur = TaskTrackingService.Instance != null ? TaskTrackingService.Instance.CurrentTrackedTaskId : -1;
        foreach (var l in _lines)
        {
            if (l != null)
            {
                l.SetTracked(l.TaskId == cur);
            }
        }
    }
}
