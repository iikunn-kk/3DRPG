using UnityEngine;

namespace PlayerFSM
{
    /// <summary>
    /// 冲刺状态：按住 Sprint + 前向输入。
    /// 可切换到：Idle（无输入）、Walk（松开 Sprint 或非前向）、Jump、Roll。
    /// </summary>
    public class PlayerSprintState : PlayerStateBase
    {
        public override PlayerState StateType => PlayerState.Sprint;

        public override void Enter()
        {
            Debug.Log("[PlayerFSM] 进入 Sprint 状态");
        }

        public override void Update() { }

        public override void CheckTransitions()
        {
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

            if (input == Vector2.zero)
            {
                owner.ChangeState(PlayerState.Idle);
            }
            else if (!sprintHeld || input.y <= 0f)
            {
                owner.ChangeState(PlayerState.Walk);
            }
        }

        public override void Exit()
        {
            Debug.Log("[PlayerFSM] 退出 Sprint 状态");
        }
    }
}
