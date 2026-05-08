using UnityEngine;

/// <summary>
/// 倒计时示例 - 使用 TimerManager
/// </summary>
public class CountdownTimer : MonoBehaviour
{
    [SerializeField] private float totalTime = 10f;
    private TimerHandle _countdownHandle;

    void Start()
    {
        StartCountdown();
    }

    /// <summary>
    /// 开始倒计时
    /// </summary>
    public void StartCountdown()
    {
        _countdownHandle = TimerManager.Instance.Countdown(totalTime,
            (progress) =>
            {
                // 进度回调（0-1）
                Debug.Log($"进度: {progress * 100:F0}%");
            },
            () =>
            {
                // 完成回调
                Debug.Log("倒计时结束！");
            });
    }

    /// <summary>
    /// 停止倒计时
    /// </summary>
    public void StopCountdown()
    {
        _countdownHandle?.Stop();
    }

    /// <summary>
    /// 暂停倒计时
    /// </summary>
    public void PauseCountdown()
    {
        _countdownHandle?.Pause();
    }

    /// <summary>
    /// 恢复倒计时
    /// </summary>
    public void ResumeCountdown()
    {
        _countdownHandle?.Resume();
    }
}
