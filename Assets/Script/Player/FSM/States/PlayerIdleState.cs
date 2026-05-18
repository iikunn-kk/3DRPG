using UnityEngine;

namespace PlayerFSM
{
    /// <summary>
    /// 待机状态：无移动输入。
    /// 可切换到：Walk（有移动输入）、Sprint（移动 + Sprint）、Jump、Roll。
    /// </summary>
    public class PlayerIdleState : PlayerStateBase
    {
        public override PlayerState StateType => PlayerState.Idle;

        public override void Enter()
        {
            Debug.Log("[PlayerFSM] 进入 Idle 状态");
        }

        public override void Update() { }

        public override void CheckTransitions()
        {
            // 跳跃/翻滚检测（由 MoveMent 输入回调触发标志）
            if (movement.IsJumping)
            {
                owner.ChangeState(PlayerState.Jump);
                return;
            }
            if (movement.IsRolling)
            {
                owner.ChangeState(PlayerState.Roll);
                return;
            }

            Vector2 input = GetInputVector();
            bool sprintHeld = IsSprintHeld();

            if (input != Vector2.zero)
            {
                if (sprintHeld)
                    owner.ChangeState(PlayerState.Sprint);
                else
                    owner.ChangeState(PlayerState.Walk);
            }
        }

        public override void Exit()
        {
            Debug.Log("[PlayerFSM] 退出 Idle 状态");
        }
    }
}
