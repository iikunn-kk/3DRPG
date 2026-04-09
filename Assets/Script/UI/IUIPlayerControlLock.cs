using UnityEngine;

/// <summary>
/// UI面板玩家控制锁定接口
/// 实现此接口的UI面板可以在显示时锁定或解锁玩家控制
/// </summary>
public interface IUIPlayerControlLock
{
    /// <summary>
    /// 当UI面板显示时调用
    /// </summary>
    void OnUIPanelShow();
    
    /// <summary>
    /// 当UI面板隐藏时调用
    /// </summary>
    void OnUIPanelHide();
}