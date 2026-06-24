using UnityEngine;

namespace PlayerFSM
{
    /// <summary>
    /// 蹲伏状态：按住 Crouch，速度减半。
    /// 物理行为：加速到蹲伏速度（减半）+ 面向摄像机前方。
    /// 可切换到：Idle（松开 Crouch）。
    /// 蹲伏中不允许翻滚。
    /// </summary>
    public class PlayerCrouchState : PlayerStateBase
    {
        private const float CrouchSpeedMultiplier = 0.5f;

        public override PlayerState StateType => PlayerState.Crouch;

        public override void Enter()
        {
            // Debug.Log("[PlayerFSM] 进入 Crouch 状态");
        }

        public override void Update() { }

        /// <summary>
        /// 物理更新：加速到蹲伏速度（减半）
        /// </summary>
        public override void FixedUpdate()
        {
            float speed = movement.MoveSpeed * CrouchSpeedMultiplier;
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
            if (movement.IsRolling)
            {
                owner.ChangeState(PlayerState.Roll);
                return;
            }

            if (movement != null && !movement.isCrouching)
            {
                owner.ChangeState(PlayerState.Idle);
            }
        }

        public override void Exit()
        {
            // Debug.Log("[PlayerFSM] 退出 Crouch 状态");
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
