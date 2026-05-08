using UnityEngine;

/// <summary>
/// 协程计时器示例 - 使用 TimerManager
/// </summary>
public class TimerCoroutine : MonoBehaviour
{
    private TimerHandle _repeatTimer;

    void Start()
    {
        // 单次延迟
        TimerManager.Instance.Delay(5f, () => Debug.Log("5秒后执行！"));

        // 重复执行（每2秒一次，共10次）
        _repeatTimer = TimerManager.Instance.Repeat(2f,
            () => Debug.Log("每2秒执行一次"),
            10);
    }

    /// <summary>
    /// 开始无限重复计时
    /// </summary>
    public void StartInfiniteRepeat(float interval)
    {
        _repeatTimer = TimerManager.Instance.Repeat(interval, () =>
        {
            Debug.Log("重复执行");
        });
    }

    /// <summary>
    /// 停止重复计时
    /// </summary>
    public void StopRepeat()
    {
        _repeatTimer?.Stop();
    }
}
