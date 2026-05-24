using System;
using UnityEngine;

/// <summary>
/// 任务事件桥接器 - 统一管理任务事件的订阅与解绑生命周期
/// 
/// 职责：
/// 1. 作为 TaskEvents 静态事件与 UI 订阅者之间的唯一中转
/// 2. Attach() — 玩家生成后，连接静态事件
/// 3. Detach() — 场景切换前，断开静态事件并清空所有 UI 订阅者
/// 
/// 使用方式：
/// - MapManager.SpawnPlayer() 中调用 Attach()
/// - SceneLoadManager 加载新场景前调用 Detach()
/// - UI 组件订阅 TaskEventBridge.Instance.OnXxx 而非直接订阅 TaskEvents.OnXxx
/// </summary>
public class TaskEventBridge : Singleton<TaskEventBridge>
{
    // ==================== Relay 事件（UI 订阅者绑定到这里） ====================

    public event Action<ObjectiveType, int, int> OnObjectiveProgress;
    public event Action<int> OnTaskCompleted;
    public event Action<int> OnTaskStarted;
    public event Action<int> OnTaskRewardsClaimed;
    public event Action<int> OnTaskTrackedChanged;

    // ==================== 内部状态 ====================

    private bool _attached;

    // ==================== 生命周期控制 ====================

    /// <summary>
    /// 连接静态 TaskEvents，开始转发事件给 UI 订阅者。
    /// 在玩家生成后调用。
    /// </summary>
    public void Attach()
    {
        if (_attached) return;

        TaskEvents.OnObjectiveProgress += ForwardObjectiveProgress;
        TaskEvents.OnTaskCompleted += ForwardTaskCompleted;
        TaskEvents.OnTaskStarted += ForwardTaskStarted;
        TaskEvents.OnTaskRewardsClaimed += ForwardTaskRewardsClaimed;
        TaskEvents.OnTaskTrackedChanged += ForwardTaskTrackedChanged;

        _attached = true;
        Debug.Log("[TaskEventBridge] Attached — task event relay active");
    }

    /// <summary>
    /// 断开静态 TaskEvents，并清空所有 UI 订阅者引用。
    /// 在场景切换/销毁玩家前调用。
    /// </summary>
    public void Detach()
    {
        if (!_attached) return;

        TaskEvents.OnObjectiveProgress -= ForwardObjectiveProgress;
        TaskEvents.OnTaskCompleted -= ForwardTaskCompleted;
        TaskEvents.OnTaskStarted -= ForwardTaskStarted;
        TaskEvents.OnTaskRewardsClaimed -= ForwardTaskRewardsClaimed;
        TaskEvents.OnTaskTrackedChanged -= ForwardTaskTrackedChanged;

        // 清空所有 relay 事件，防止旧 UI 实例泄漏
        OnObjectiveProgress = null;
        OnTaskCompleted = null;
        OnTaskStarted = null;
        OnTaskRewardsClaimed = null;
        OnTaskTrackedChanged = null;

        _attached = false;
        Debug.Log("[TaskEventBridge] Detached — all task event relays cleared");
    }

    protected override void OnDestroy()
    {
        Detach();
        base.OnDestroy();
    }

    // ==================== 转发方法 ====================

    private void ForwardObjectiveProgress(ObjectiveType type, int targetId, int amount) =>
        OnObjectiveProgress?.Invoke(type, targetId, amount);

    private void ForwardTaskCompleted(int taskId) =>
        OnTaskCompleted?.Invoke(taskId);

    private void ForwardTaskStarted(int taskId) =>
        OnTaskStarted?.Invoke(taskId);

    private void ForwardTaskRewardsClaimed(int taskId) =>
        OnTaskRewardsClaimed?.Invoke(taskId);

    private void ForwardTaskTrackedChanged(int taskId) =>
        OnTaskTrackedChanged?.Invoke(taskId);
}
