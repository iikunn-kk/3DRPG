using UnityEngine;

namespace PlayerFSM
{
    /// <summary>
    /// 跳跃状态：空中物理状态。
    /// 物理逻辑由 MoveMent 处理（OnJump 设置 isJumping + 施加冲量）。
    /// FSM 负责状态标记和落地恢复。
    /// </summary>
    public class PlayerJumpState : PlayerStateBase
    {
        public override PlayerState StateType => PlayerState.Jump;

        public override void Enter()
        {
            Debug.Log($"[PlayerFSM] 进入 Jump 状态（物理由 MoveMent 驱动）");
        }

        public override void Update()
        {
            // 检测落地：MoveMent 在接地时会将 isJumping 置为 false
            if (!movement.IsJumping)
            {
                owner.ChangeState(owner.PreviousState);
            }
        }

        public override void Exit()
        {
            Debug.Log($"[PlayerFSM] 退出 Jump 状态");
        }
    }
}
