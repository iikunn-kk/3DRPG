// Unity 客户端：替换 Assets/Script/Network/NetworkPlayerMover.cs
// ProtoBuf 版本：位置消息直接序列化为 proto binary，走 UDP

using UnityEngine;
using Proto = Mmo;

/// <summary>
/// 零侵入位置同步组件 — ProtoBuf 版本。
/// 挂载到玩家 GameObject，检测位置/朝向/状态变化后发送 UDP ProtoBuf 消息。
/// 带宽: ~0.9 KB/s (vs JSON 的 2.4 KB/s)
/// </summary>
public class NetworkPlayerMover : MonoBehaviour
{
    [Tooltip("位置变化超过此阈值（米）才发送更新")]
    [SerializeField] private float _moveThreshold = 0.05f;

    [Tooltip("朝向变化超过此阈值（度）才发送更新")]
    [SerializeField] private float _rotThreshold = 1.0f;

    [Tooltip("发送频率（秒），默认 0.033s ≈ 30Hz")]
    [SerializeField] private float _sendInterval = 0.033f;

    private Vector3 _lastSentPosition;
    private float _lastSentRotY = float.NaN;
    private float _sendTimer;
    private MoveMent _movement;
    private bool _lastCrouch, _lastJump, _lastRoll, _lastDead;

    void Start()
    {
        _lastSentPosition = transform.position;
        _sendTimer = _sendInterval;
        _movement = GetComponent<MoveMent>();
    }

    void Update()
    {
        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
            return;

        _sendTimer += Time.deltaTime;
        if (_sendTimer < _sendInterval) return;

        // 朝向：用 Atan2(forward) 避免 eulerAngles 的 0/360 跳变
        Vector3 fwd = transform.rotation * Vector3.forward;
        float currentRotY = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        if (currentRotY < 0f) currentRotY += 360f;

        bool posChanged = Vector3.Distance(transform.position, _lastSentPosition) > _moveThreshold;
        bool rotChanged = float.IsNaN(_lastSentRotY) || Mathf.Abs(Mathf.DeltaAngle(_lastSentRotY, currentRotY)) > _rotThreshold;

        // 状态变化检测（蹲伏/跳跃/翻滚/死亡一旦变化立即发送）
        bool crouch = _movement != null && _movement.isCrouching;
        bool jump = _movement != null && _movement.IsJumping;
        bool roll = _movement != null && _movement.IsRolling;
        bool dead = CharacterService.Instance?.CurrentPlayerCharacter()?.CurrentHealth <= 0;
        bool stateChanged = crouch != _lastCrouch || jump != _lastJump || roll != _lastRoll || dead != _lastDead;

        bool forceSend = _sendTimer > 0.5f;

        if (posChanged || rotChanged || stateChanged || forceSend)
        {
            _lastSentPosition = transform.position;
            _lastSentRotY = currentRotY;
            _lastCrouch = crouch; _lastJump = jump; _lastRoll = roll; _lastDead = dead;
            _sendTimer = 0f;

            NetworkManager.Instance.SendPosition(transform.position, transform.rotation, crouch, jump, roll, dead);
        }
    }
}
