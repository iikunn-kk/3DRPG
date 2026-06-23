using UnityEngine;

/// <summary>
/// 零侵入位置同步组件 — 挂载到玩家 GameObject，检测 transform.position 变化后自动发送 TCP 位置消息。
/// 不改 MoveMent / FSM / CharacterState 任何一行代码。
/// </summary>
public class NetworkPlayerMover : MonoBehaviour
{
    [Header("同步设置")]
    [Tooltip("位置变化超过此阈值（米）才发送更新")]
    [SerializeField] private float _moveThreshold = 0.05f;

    [Tooltip("发送频率（秒），默认 0.033s ≈ 30Hz")]
    [SerializeField] private float _sendInterval = 0.033f;

    private Vector3 _lastSentPosition;
    private float _sendTimer;

    void Start()
    {
        _lastSentPosition = transform.position;
        _sendTimer = _sendInterval;
    }

    void Update()
    {
        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
            return;

        _sendTimer += Time.deltaTime;
        if (_sendTimer < _sendInterval) return;

        var currentPos = transform.position;
        if (Vector3.Distance(currentPos, _lastSentPosition) > _moveThreshold || _sendTimer > 0.5f)
        {
            _lastSentPosition = currentPos;
            _sendTimer = 0f;
            NetworkManager.Instance.SendPosition(currentPos, transform.rotation);
        }
    }
}
