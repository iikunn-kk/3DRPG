using System;
using UnityEngine;

/// <summary>
/// 无参数 ScriptableObject 事件。广播者调用 Raise，所有 VoidEventListener 收到回调。
/// 使用 System.Action 替代 UnityEngine.Events.UnityAction 以减少 GC 分配。
/// </summary>
[CreateAssetMenu(fileName = "VoidEventSO", menuName = "Events/VoidEventSO")]
public class VoidEventSO : ScriptableObject
{
    [TextArea]
    public string description;

    /// <summary>
    /// 事件回调列表，使用 C# 原生 Action 减少 GC
    /// </summary>
    public Action onEventRaised;

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

    public void Raise(object sender)
    {
        onEventRaised?.Invoke();
        _lastSender = sender?.ToString() ?? "null";
#if UNITY_EDITOR
        _subscriberCount = onEventRaised?.GetInvocationList().Length ?? 0;
#endif
    }

    /// <summary>
    /// 移除所有监听者
    /// </summary>
    public void RemoveAllListeners()
    {
        onEventRaised = null;
        _subscriberCount = 0;
    }

    private void OnDisable()
    {
        RemoveAllListeners();
    }
}