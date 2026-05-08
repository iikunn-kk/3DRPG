using UnityEngine;
using UnityEngine.Events;
[CreateAssetMenu(fileName = "VoidEventSO", menuName = "Events/VoidEventSO")]
// 专门的无参数事件ScriptableObject
public class VoidEventSO : ScriptableObject
{
    [TextArea]
    public string description;

    public UnityAction onEventRaised;//事件回调
    public string lastSender;  // 记录最后一个发送者（调试用）

    public void Raise(object sender)
    {
        onEventRaised?.Invoke();   // 触发所有监听者
        lastSender = sender?.ToString();
    }
}