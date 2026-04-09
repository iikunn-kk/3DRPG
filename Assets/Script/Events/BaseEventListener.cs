using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 代码逻辑:
/// 此代码中转所有的监听者,
/// 相当于这个文件是个监听者总代理,收集所有的监听方法,只内置一键启动一个方法
/// 所有的方法执行者,把方法加入到监听方法UnityEvent里,这个文件负责全部启动
/// </summary>
/// <typeparam stationName="T">需要传递的数据类型</typeparam>
/// <typeparam name="T"></typeparam>
public class BaseEventListener<T> : MonoBehaviour
{
  /// <summary>
  /// 事件方法载体,广播者用这个文件进行标记,需要广播的时候就调用这个文件
  /// </summary>
  public BaseEventSO<T> eventSO;
  /// <summary>
  /// 事件响应,根据上面的文件收集的广播者,当广播者调用这个文件的时候,就执行这个Event里挂载的全部事件
  /// </summary>
  public UnityEvent<T> response;

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

  private void OnEventRaised(T value)
  {
    response.Invoke(value);
  }
}
