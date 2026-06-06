using UnityEngine;

/// <summary>
/// 监听 PlayerDeathEventSo，弹出死亡面板并触发惩罚逻辑。
/// 备用方案：当前 Die() 已直接调用 ApplyDeathPenaltyAndPersist()，
/// 此组件暂不使用，保留供后续事件驱动架构迁移。
/// </summary>
public class DeathEventHandler : Singleton<DeathEventHandler>
{
    [Header("事件引用")]
    [SerializeField] private GameObjectEventSO _playerDeathEventSo;

    private void OnEnable()
    {
        if (_playerDeathEventSo != null)
            _playerDeathEventSo.onEventRaised += HandlePlayerDeath;
    }

    private void OnDisable()
    {
        if (_playerDeathEventSo != null)
            _playerDeathEventSo.onEventRaised -= HandlePlayerDeath;
    }

    private void HandlePlayerDeath(GameObject playerGo)
    {
        if (playerGo == null) return;
        var state = playerGo.GetComponent<CharacterState>();
        if (state == null) return;
        if (!state.ApplyPenaltyImmediately) return;
        state.ApplyDeathPenaltyAndPersist();
    }
}
