using UnityEngine;

namespace PlayerFSM
{
    /// <summary>
    /// 行走状态：有移动输入，非冲刺非蹲伏。
    /// 物理行为：加速到行走速度 + 面向摄像机前方。
    /// 可切换到：Idle（无输入）、Sprint（Sprint + 前向）、Jump、Roll。
    /// </summary>
    public class PlayerWalkState : PlayerStateBase
    {
        public override PlayerState StateType => PlayerState.Walk;

        public override void Enter()
        {
            // Debug.Log("[PlayerFSM] 进入 Walk 状态");
        }

        public override void Update() { }

        /// <summary>
        /// 物理更新：加速到行走速度
        /// </summary>
        public override void FixedUpdate()
        {
            Vector3 targetVelocity = CalculateMoveVelocity(movement.MoveSpeed);
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
            else if (sprintHeld)
            {
                if (input.y > 0f)
                    owner.ChangeState(PlayerState.Sprint);
            }
        }

        public override void Exit()
        {
            // Debug.Log("[PlayerFSM] 退出 Walk 状态");
        }

        /// <summary>
        /// 根据输入计算相机相对方向的移动速度向量
        /// </summary>
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
