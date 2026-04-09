using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BaseEventSO<T> : ScriptableObject
{
    /// <summary>
    /// 此代码逻辑:
    /// 将广播者全部收集到这个文件里,并且发送给所有的监听者
    /// 当广播者需要广播的时候,就把自己标记给这个文件,
    /// 触发的时候,监听者会根据文件里标记的广播者,执行所有的方法
    /// T为需要传递的数据类型
    /// </summary>
    [TextArea]
    public string description;//描述
    //这个方法里只内置了一个一键启动,触发的时候,就会执行所有的方法
    public UnityAction<T> onEventRaised;
    public string lastSender;
    /// <summary>
    /// 广播
    /// </summary>
    /// <param stationName="value">事件变量</param>
    /// <param stationName="sender">广播者</param>
    public void RaiseEvent(T value,object sender)
    {
        onEventRaised?.Invoke(value);
        lastSender = sender.ToString();
    }
}
