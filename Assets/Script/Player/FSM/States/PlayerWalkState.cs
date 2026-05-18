using UnityEngine;

namespace PlayerFSM
{
    /// <summary>
    /// 行走状态：有移动输入，非冲刺非蹲伏。
    /// 可切换到：Idle（无输入）、Sprint（Sprint + 前向）、Jump、Roll。
    /// </summary>
    public class PlayerWalkState : PlayerStateBase
    {
        public override PlayerState StateType => PlayerState.Walk;

        public override void Enter()
        {
            Debug.Log("[PlayerFSM] 进入 Walk 状态");
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
            else if (sprintHeld)
            {
                if (input.y > 0f)
                    owner.ChangeState(PlayerState.Sprint);
            }
        }

        public override void Exit()
        {
            Debug.Log("[PlayerFSM] 退出 Walk 状态");
        }
    }
}
