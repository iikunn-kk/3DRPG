using UnityEngine;

/// <summary>
/// 远程实体位置/旋转插值器。
/// - 位置：Lerp 插值，从当前位置续接，renderDelay 内平滑过渡
/// - 旋转：角速度外推（Dead Reckoning）。从最近两个快照计算旋转速率，
///   插值完成后持续同速旋转，不等下一帧。消除 Slerp 被中断导致的朝向滞后。
/// - 提供平滑速度输出，供 MonsterLocomotionDriver 直接使用
/// </summary>
[DefaultExecutionOrder(-100)]  // 在 MonsterLocomotionDriver 之前执行，确保当帧位置已插值
public class PositionInterpolator : MonoBehaviour
{
    [Tooltip("位置插值时长（秒）。默认 66ms ≈ 2×30Hz")]
    [SerializeField] private float _renderDelay = 0.066f;

    public float RenderDelay
    {
        get => _renderDelay;
        set => _renderDelay = Mathf.Max(0.016f, value);
    }

    /// <summary>平滑后的水平速度（m/s），供动画驱动器使用</summary>
    public Vector3 SmoothedVelocity => _smoothedVel;

    // 位置插值
    private Vector3 _prevPos;
    private Vector3 _targetPos;
    private float _startTime, _endTime;
    private bool _hasTarget;

    // 旋转外推
    private Quaternion _prevRot;
    private Quaternion _targetRot;
    private float _angularVelocity;       // 旋转速率（度/秒），从最近两个快照计算
    private float _lastRotTargetTime;     // 上次 SetTarget 的时间
    private float _lastRotTargetAngle;    // 上次快照对应的 rotY（用于计算 delta angle）

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
        float now = Time.time;

        if (!_hasTarget)
        {
            // 首次：直接跳到目标，避免初始长距离滑行
            transform.position = pos;
            transform.rotation = rot;
            _prevPos = pos;
            _targetPos = pos;
            _prevRot = rot;
            _targetRot = rot;
            _startTime = now;
            _endTime = now + 0.001f;
            _lastFramePos = pos;
            _lastRotTargetTime = now;
            _lastRotTargetAngle = rot.eulerAngles.y;
            _angularVelocity = 0f;
            _hasTarget = true;
            return;
        }

        // --- 位置：从当前续接 ---
        _prevPos = transform.position;
        _startTime = now;
        _endTime = now + _renderDelay;
        _targetPos = pos;

        // --- 旋转：计算角速度，从当前（含外推）续接 ---
        float newAngle = rot.eulerAngles.y;
        float dt = now - _lastRotTargetTime;
        if (dt > 0.001f)
        {
            // 用 DeltaAngle 处理 0°/360° 环绕
            float delta = Mathf.DeltaAngle(_lastRotTargetAngle, newAngle);
            // 平滑角速度（EMA，时间常数 100ms），过滤网络抖动
            float instantAngVel = delta / dt;
            float alpha = 1f - Mathf.Exp(-dt / 0.1f);
            _angularVelocity = Mathf.Lerp(_angularVelocity, instantAngVel, alpha);
        }
        _lastRotTargetTime = now;
        _lastRotTargetAngle = newAngle;

        // 从当前（含外推的）朝向续接，无跳跃
        _prevRot = transform.rotation;
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

        // --- 位置插值 ---
        float posT = Mathf.InverseLerp(_startTime, _endTime, Time.time);
        posT = Mathf.Clamp01(posT);
        transform.position = Vector3.Lerp(_prevPos, _targetPos, posT);

        // --- 旋转：Slerp 到外推目标；完成后角速度继续外推 ---
        float rotT = Mathf.InverseLerp(_startTime, _endTime, Time.time);
        if (rotT >= 1f)
        {
            // Slerp 完成：用角速度继续外推
            float extraTime = Time.time - _endTime;
            if (_angularVelocity != 0f)
            {
                Quaternion extrapolated = Quaternion.AngleAxis(_angularVelocity * extraTime, Vector3.up);
                transform.rotation = _targetRot * extrapolated;
            }
            else
            {
                transform.rotation = _targetRot;
            }
        }
        else
        {
            // Slerp 进行中：目标外推到 renderDelay 后的预测位置
            // 关键：不 Slerp 到当前快照位置，而是到预测的终点位置
            Quaternion predictedTarget = _targetRot * Quaternion.AngleAxis(
                _angularVelocity * _renderDelay * (1f - rotT), Vector3.up);
            transform.rotation = Quaternion.Slerp(_prevRot, predictedTarget, rotT);
        }

        // 计算平滑速度（基于实际帧位移，指数移动平均，时间常数 80ms）
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 instantVel = (transform.position - _lastFramePos) / dt;
        instantVel.y = 0f;
        float alpha = 1f - Mathf.Exp(-dt / 0.08f);
        _smoothedVel = Vector3.Lerp(_smoothedVel, instantVel, alpha);
        _lastFramePos = transform.position;
    }
}
