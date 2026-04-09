using UnityEngine;
using UnityEngine.Events;
[CreateAssetMenu(fileName = "VoidEventSO", menuName = "Events/VoidEventSO")]
// 专门的无参数事件ScriptableObject
public class VoidEventSO : ScriptableObject
{
    [TextArea]
    public string description;
    
    public UnityAction onEventRaised;
    public string lastSender;
    
    public void Raise(object sender)
    {
        onEventRaised?.Invoke();
        lastSender = sender?.ToString();
    }
}