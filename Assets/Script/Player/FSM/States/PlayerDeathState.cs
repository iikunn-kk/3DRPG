using UnityEngine;

namespace PlayerFSM
{
    /// <summary>
    /// 死亡状态：终态，最高优先级。
    /// 由 PlayerStateMachine.UpdateStateMachine 检测 _isDead 进入。
    /// 等待复活后退出。
    /// </summary>
    public class PlayerDeathState : PlayerStateBase
    {
        public override PlayerState StateType => PlayerState.Death;

        // 复活回调标记：已订阅 CharacterState 的 PlayerRespawnEventSo
        private bool _hasSubscribed;

        public override void Enter()
        {
            // Debug.Log($"[PlayerFSM] 进入 Death 状态");

            // 现有死亡流程已由 CharacterState.Die() 处理
            // FSM 仅做状态标记，确保不响应任何输入转换
        }

        public override void Update()
        {
            // 不检查任何转换——死亡是终态
        }

        public override void CheckTransitions()
        {
            // 死亡状态下不进行任何转换检查
        }

        public override void Exit()
        {
            // Debug.Log($"[PlayerFSM] 退出 Death 状态");
        }

        /// <summary>
        /// 外部调用：标记复活（供 CharacterState 复活后调用）
        /// </summary>
        public void OnRespawn()
        {
            if (owner.CurrentState == PlayerState.Death)
            {
                // 复活后恢复到 Idle
                owner.ChangeState(PlayerState.Idle);
            }
        }
    }
}
