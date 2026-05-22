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
        _sendTimer = _sendInterval; // 首帧立即发送一次，确保服务器有初始位置
    }

    void Update()
    {
        if (NetworkManager.Instance == null) return;

        if (!NetworkManager.Instance.IsConnected)
        {
            // 连接未就绪时不做任何事
            if (!_loggedDisconnected)
            {
                Debug.LogWarning($"[NetworkPlayerMover] 等待 MMO 连接... (IsConnected={NetworkManager.Instance.IsConnected})");
                _loggedDisconnected = true;
            }
            return;
        }

        _loggedDisconnected = false;
        _sendTimer += Time.deltaTime;
        if (_sendTimer < _sendInterval) return;

        var currentPos = transform.position;
        var delta = Vector3.Distance(currentPos, _lastSentPosition);

        if (delta > _moveThreshold || _sendTimer > 0.5f)
        {
            Debug.Log($"[NetworkPlayerMover] 发送位置: ({currentPos.x:F2},{currentPos.y:F2},{currentPos.z:F2}) delta={delta:F3}");
            _lastSentPosition = currentPos;
            _sendTimer = 0f;
            NetworkManager.Instance.SendPosition(currentPos, transform.rotation);
        }
    }

    private bool _loggedDisconnected;
}
