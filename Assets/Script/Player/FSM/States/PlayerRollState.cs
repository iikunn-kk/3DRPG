using UnityEngine;

namespace PlayerFSM
{
    /// <summary>
    /// 翻滚状态：物理驱动滚动位移，带冷却。
    /// Enter 时由 MoveMent.Roll() 完成初始化（方向/计时器/动画）。
    /// FixedUpdate 接管完整的翻滚物理（曲线位移 + 旋转 + 结束判定）。
    /// </summary>
    public class PlayerRollState : PlayerStateBase
    {
        public override PlayerState StateType => PlayerState.Roll;

        public override void Enter()
        {
            // Debug.Log($"[PlayerFSM] 进入 Roll 状态（FSM 接管翻滚物理）");
        }

        public override void Update()
        {
            // 翻滚结束由 FixedUpdate 中的物理逻辑触发状态切换
        }

        /// <summary>
        /// 物理更新：翻滚曲线位移 + 面向翻滚方向 + 结束判定
        /// 源自 MoveMent.HandleRoll() 的全部逻辑
        /// </summary>
        public override void FixedUpdate()
        {
            if (!movement.IsRolling) return;

            float dt = Time.fixedDeltaTime;

            // 递减计时器
            movement.RollTimer -= dt;

            // ========== 实时计算翻滚方向（边翻滚边转向） ==========
            Vector2 input = GetInputVector();
            Vector3 dir = movement.RollDirection;

            if (input.sqrMagnitude > 0.01f && MainCamera != null)
            {
                dir = new Vector3(input.x, 0, input.y);
                dir = MainCamera.transform.TransformDirection(dir);
                dir.y = 0;
                dir.Normalize();
                movement.RollDirection = dir;
            }

            // ========== 核心翻滚逻辑 ==========
            float progress = 1f - (movement.RollTimer / movement.RollDuration);
            float speedMultiplier = movement.RollSpeedCurve.Evaluate(progress);
            float maxSpeed = (movement.rollDistance / movement.RollDuration) * 1.5f;
            float currentSpeed = maxSpeed * speedMultiplier;

            movement.RollDistanceTraveled += currentSpeed * dt;

            // ========== 应用位移和旋转 ==========
            if (Rb != null)
            {
                Rb.linearVelocity = dir * currentSpeed;

                if (dir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(dir);
                    Rb.MoveRotation(Quaternion.Slerp(Rb.rotation, targetRotation,
                        movement.TurnSpeed * 3f));
                }
            }
            else
            {
                owner.transform.position += dir * currentSpeed * dt;

                if (dir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(dir);
                    owner.transform.rotation = Quaternion.Slerp(owner.transform.rotation,
                        targetRotation, movement.TurnSpeed * 3f);
                }
            }

            // ========== 结束判定 ==========
            bool distanceReached = movement.RollDistanceTraveled >= movement.rollDistance;
            bool timeEnded = movement.RollTimer <= 0f;

            if (distanceReached || timeEnded)
            {
                // 置回翻滚标志（通过 MoveMent 字段）
                // 因为 isRolling 是私有字段只能通过 CancelRoll 或反射设置
                // 这里通过反射方式不太好，所以用 CancelRoll 替代
                // 但 CancelRoll 会设置冷却，这本来就是需要的
                movement.CancelRoll();

                // 回到前一状态
                // 注意：Exit() 不能在这里调用——由 ChangeStateInternal 处理
                owner.ChangeState(owner.PreviousState);
            }
        }

        public override void Exit()
        {
            // Debug.Log($"[PlayerFSM] 退出 Roll 状态");
        }
    }
}
