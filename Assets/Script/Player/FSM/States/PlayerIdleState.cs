using UnityEngine;

namespace PlayerFSM
{
    /// <summary>
    /// 待机状态：无移动输入。
    /// 物理行为：减速到停止 + 面向摄像机前方。
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

        /// <summary>
        /// 物理更新：减速到停止
        /// </summary>
        public override void FixedUpdate()
        {
            Decelerate();
        }

        /// <summary>
        /// 动画后处理：面向摄像机前方（在 Animator 更新后执行，避免动画帧引入的旋转偏移）
        /// </summary>
        public override void LateUpdate()
        {
            RotateTowardCameraForward();
        }

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
