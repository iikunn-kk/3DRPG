using UnityEngine;
using UnityEngine.Events;
[CreateAssetMenu(fileName = "VoidEventSO", menuName = "Events/VoidEventSO")]
// 专门的无参数事件ScriptableObject
public class VoidEventListener : MonoBehaviour
{
    /// <summary>
    /// 事件方法载体,广播者用这个文件进行标记,需要广播的时候就调用这个文件
    /// </summary>
    public VoidEventSO eventSO;
    /// <summary>
    /// 事件响应,根据上面的文件收集的广播者,当广播者调用这个文件的时候,就执行这个Event里挂载的全部事件
    /// </summary>
    public UnityEvent response;

    private void OnEnable()
    {
        if (eventSO != null)
        {
            eventSO.onEventRaised += OnEventRaised;//执行列表里的方法
        }
    }

    private void OnDisable()
    {
        if (eventSO != null)
        {
            eventSO.onEventRaised -= OnEventRaised;
        }
    }

    private void OnEventRaised()
    {
        //运行所有被添加进入的方法
        response.Invoke();
    }
}