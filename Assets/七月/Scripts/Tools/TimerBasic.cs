using UnityEngine;

/// <summary>
/// 简单计时器示例 - 使用 TimerManager
/// </summary>
public class TimerBasic : MonoBehaviour
{
    [SerializeField] private float duration = 5f;

    private TimerHandle _timerHandle;

    void Start()
    {
        // 使用 TimerManager 启动计时器
        _timerHandle = TimerManager.Instance.Delay(duration, () =>
        {
            Debug.Log("计时结束！");
        });
    }

    /// <summary>
    /// 手动启动计时器
    /// </summary>
    public void StartTimer()
    {
        _timerHandle = TimerManager.Instance.Delay(duration, () =>
        {
            Debug.Log("计时结束！");
        });
    }

    /// <summary>
    /// 停止计时器
    /// </summary>
    public void StopTimer()
    {
        _timerHandle?.Stop();
    }
}
