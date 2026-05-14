using System;
using UnityEngine;

/// <summary>
/// ScriptableObject 事件基类（带参数）。
/// 广播者调用 RaiseEvent，所有通过 BaseEventListener 订阅的监听者收到回调。
/// 使用 System.Action&lt;T&gt; 替代 UnityAction&lt;T&gt; 以减少 GC 分配。
/// </summary>
/// <typeparam name="T">事件传递的数据类型</typeparam>
public class BaseEventSO<T> : ScriptableObject
{
    [TextArea]
    public string description;

    /// <summary>
    /// 事件回调列表，使用 C# 原生 Action 减少 GC（替代 UnityEngine.Events.UnityAction）
    /// </summary>
    public Action<T> onEventRaised;

    [SerializeField, Tooltip("上次广播者（调试用）")]
    private string _lastSender;
    public string lastSender => _lastSender;

    [SerializeField, Tooltip("当前订阅数（调试用）")]
    private int _subscriberCount;

    /// <summary>
    /// 当前订阅者数量（编辑器可查看）
    /// </summary>
    public int SubscriberCount
    {
        get
        {
            if (onEventRaised != null)
                _subscriberCount = onEventRaised.GetInvocationList().Length;
            else
                _subscriberCount = 0;
            return _subscriberCount;
        }
    }

    /// <summary>
    /// 广播事件
    /// </summary>
    /// <param name="value">事件数据</param>
    /// <param name="sender">广播者</param>
    public void RaiseEvent(T value, object sender)
    {
        onEventRaised?.Invoke(value);
        _lastSender = sender?.ToString() ?? "null";
#if UNITY_EDITOR
        _subscriberCount = onEventRaised?.GetInvocationList().Length ?? 0;
#endif
    }

    /// <summary>
    /// 移除所有监听者（用于场景卸载或测试清理）
    /// </summary>
    public void RemoveAllListeners()
    {
        onEventRaised = null;
        _subscriberCount = 0;
    }

    private void OnDisable()
    {
        // ScriptableObject 禁用时清理，防止残留引用
        RemoveAllListeners();
    }
}
