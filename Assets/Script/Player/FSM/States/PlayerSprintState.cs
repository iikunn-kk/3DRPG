using UnityEngine;

namespace PlayerFSM
{
    /// <summary>
    /// 冲刺状态：按住 Sprint + 前向输入。
    /// 物理行为：加速到冲刺速度 + 面向摄像机前方。
    /// 可切换到：Idle（无输入）、Walk（松开 Sprint 或非前向）、Jump、Roll。
    /// </summary>
    public class PlayerSprintState : PlayerStateBase
    {
        public override PlayerState StateType => PlayerState.Sprint;

        public override void Enter()
        {
            // Debug.Log("[PlayerFSM] 进入 Sprint 状态");
        }

        public override void Update() { }

        /// <summary>
        /// 物理更新：加速到冲刺速度
        /// </summary>
        public override void FixedUpdate()
        {
            float speed = movement.MoveSpeed * movement.MaxSpeedMultiplier;
            Vector3 targetVelocity = CalculateMoveVelocity(speed);
            ApplyMovementVelocity(targetVelocity, movement.MovementAcceleration);
        }

        /// <summary>
        /// 动画后处理：面向摄像机前方（在 Animator 更新后执行）
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
            // Debug.Log("[PlayerFSM] 退出 Sprint 状态");
        }

        private Vector3 CalculateMoveVelocity(float speed)
        {
            Vector2 input = GetInputVector();
            Vector3 moveDir = new Vector3(input.x, 0, input.y);
            if (MainCamera != null)
            {
                moveDir = MainCamera.transform.TransformDirection(moveDir);
            }
            moveDir.y = 0;
            moveDir.Normalize();
            return moveDir * speed;
        }
    }
}
