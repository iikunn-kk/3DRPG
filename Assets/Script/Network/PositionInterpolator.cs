using UnityEngine;


/// <summary>
/// 远程实体位置/旋转插值器。
/// - 位置和旋转共用同一插值时间线，解决朝向不更新问题
/// - 从当前位置续接到新目标，避免续接到上一个目标产生跳跃
/// - 提供平滑速度输出，供 MonsterLocomotionDriver 直接使用（避免位置增量反推尖峰）
/// </summary>
[DefaultExecutionOrder(-100)]  // 在 MonsterLocomotionDriver 之前执行，确保当帧位置已插值
public class PositionInterpolator : MonoBehaviour
{
    [Tooltip("插值时长（秒）。应 >= 发送间隔以平滑过渡，默认 66ms ≈ 2×30Hz")]
    [SerializeField] private float _renderDelay = 0.066f;

    public float RenderDelay
    {
        get => _renderDelay;
        set => _renderDelay = Mathf.Max(0.016f, value);
    }

    /// <summary>平滑后的水平速度（m/s），供动画驱动器使用</summary>
    public Vector3 SmoothedVelocity => _smoothedVel;

    private Vector3 _prevPos;
    private Vector3 _targetPos;
    private Quaternion _prevRot;
    private Quaternion _targetRot;
    private float _startTime, _endTime;
    private bool _hasTarget;

    // 速度计算
    private Vector3 _lastFramePos;
    private Vector3 _smoothedVel;

    void Awake()
    {
        _lastFramePos = transform.position;
    }

    /// <summary>设置位置和旋转目标（推荐用法）</summary>
    public void SetTarget(Vector3 pos, Quaternion rot)
    {
        if (!_hasTarget)
        {
            // 首次：直接跳到目标，避免初始长距离滑行
            transform.position = pos;
            transform.rotation = rot;
            _prevPos = pos;
            _prevRot = rot;
            _targetPos = pos;
            _targetRot = rot;
            _startTime = Time.time;
            _endTime = Time.time + 0.001f;
            _lastFramePos = pos;
            _hasTarget = true;
            return;
        }

        // 从当前位置续接（无跳跃）
        _prevPos = transform.position;
        _prevRot = transform.rotation;
        _startTime = Time.time;
        _endTime = Time.time + _renderDelay;
        _targetPos = pos;
        _targetRot = rot;
    }

    /// <summary>仅设置位置（旋转保持上一个目标）</summary>
    public void SetTarget(Vector3 pos)
    {
        SetTarget(pos, _hasTarget ? _targetRot : transform.rotation);
    }

    /// <summary>兼容旧接口：更新目标旋转（优先用 SetTarget(pos,rot) 一起传）</summary>
    public void SetRotation(Quaternion rot)
    {
        if (_hasTarget)
            _targetRot = rot;
    }

    void Update()
    {
        if (!_hasTarget) return;

        float t = Mathf.InverseLerp(_startTime, _endTime, Time.time);
        t = Mathf.Clamp01(t);

        // 位置和旋转用同一时间线插值
        transform.position = Vector3.Lerp(_prevPos, _targetPos, t);
        transform.rotation = Quaternion.Slerp(_prevRot, _targetRot, t);

        // 计算平滑速度（基于实际帧位移，指数移动平均，时间常数 80ms）
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 instantVel = (transform.position - _lastFramePos) / dt;
        instantVel.y = 0f;
        float alpha = 1f - Mathf.Exp(-dt / 0.08f);
        _smoothedVel = Vector3.Lerp(_smoothedVel, instantVel, alpha);
        _lastFramePos = transform.position;
    }
}
