using UnityEngine;

/// <summary>
/// 公共冷却（GCD）控制器：集中管理一条全局冷却计时。
/// - 通过 StartGCD 启动，Update 中倒计时；
/// - 供 SkillController 等在施放技能前进行拦截检查。
/// </summary>
public class GlobalCooldownController : MonoBehaviour
{
    [Tooltip("公共冷却时长（秒），用于未显式指定时的默认值")] 
    [SerializeField] private float defaultDuration = 1.5f;

    public bool IsOnGCD => _timer > 0f;
    public float Remaining => Mathf.Max(0f, _timer);
    public float DefaultDuration => defaultDuration;

    // 新增事件：开始/更新/结束
    public event System.Action<float> GcdStarted; // duration
    public event System.Action<float> GcdUpdated; // remaining
    public event System.Action GcdEnded;

    private float _timer = 0f;

    private void Update()
    {
        if (_timer > 0f)
        {
            _timer -= Time.deltaTime;
            if (_timer < 0f) _timer = 0f;
            // 广播更新
            GcdUpdated?.Invoke(_timer);
            if (_timer == 0f)
            {
                GcdEnded?.Invoke();
            }
        }
    }

    /// <summary>
    /// 启动一次公共冷却。
    /// </summary>
    public void StartGCD(float duration = -1f)
    {
        _timer = (duration > 0f) ? duration : defaultDuration;
        Debug.Log($"[GlobalCooldownController] StartGCD duration={_timer}");
        GcdStarted?.Invoke(_timer);
    }

    /// <summary>
    /// 强制清除公共冷却。
    /// </summary>
    public void Clear()
    {
        _timer = 0f;
        GcdEnded?.Invoke();
    }
}
