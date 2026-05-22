using UnityEngine;

/// <summary>
/// 远程实体位置插值渲染 — 30ms 延迟缓冲 + Lerp 平滑（原100ms，降低延迟）。
/// 挂载到远程玩家/怪物的 Prefab 上。
/// </summary>
public class PositionInterpolator : MonoBehaviour
{
    [SerializeField] private float _renderDelay = 0.03f;
    [SerializeField] private float _lerpSpeed = 10f;

    /// <summary>运行时调整渲染延迟（秒），EntitySyncManager 创建远程实体时调用</summary>
    public float RenderDelay { get => _renderDelay; set => _renderDelay = value; }

    private Vector3 _prevPos, _targetPos;
    private float _prevTime, _targetTime;
    private bool _hasTarget;

    public void SetTarget(Vector3 pos)
    {
        _prevPos = transform.position;
        _prevTime = Time.time;
        _targetPos = pos;
        _targetTime = Time.time + _renderDelay;
        _hasTarget = true;
    }

    void Update()
    {
        if (!_hasTarget) return;
        float t = Mathf.InverseLerp(_prevTime, _targetTime, Time.time);
        transform.position = Vector3.Lerp(_prevPos, _targetPos, Mathf.Clamp01(t));
    }
}
