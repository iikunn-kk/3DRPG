using UnityEngine;

namespace PlayerFSM
{
    /// <summary>
    /// 跳跃状态：空中物理状态。
    /// 跳跃冲量由 MoveMent.OnJump 施加（保持即时响应），
    /// FSM 管理滞空中的水平速度保持、朝向平滑跟随相机、落地恢复。
    /// 
    /// 朝向策略：实时跟随相机前方，与地面移动状态一致。
    /// LateUpdate 中用 Slerp 平滑过渡，鼠标旋转视角时角色空中同步转向。
    /// </summary>
    public class PlayerJumpState : PlayerStateBase
    {
        public override PlayerState StateType => PlayerState.Jump;

        private float _horizontalSpeed; // 跳跃时保存的水平速度模长，用于滞空中保持

        public override void Enter()
        {
            // Debug.Log($"[PlayerFSM] 进入 Jump 状态（空中朝向实时跟随相机）");

            // 保存跳跃瞬间的水平速度模长，用于滞空中抵抗物理阻力
            if (Rb != null)
            {
                Vector3 vel = Rb.linearVelocity;
                _horizontalSpeed = new Vector3(vel.x, 0, vel.z).magnitude;
            }

            // 冻结刚体角速度，防止冲量/碰撞在物理层引入旋转。
            // 旋转完全由 LateUpdate 的 Slerp 控制，不与物理角速度竞争。
            if (Rb != null)
            {
                Rb.angularVelocity = Vector3.zero;
            }
        }

        public override void Update()
        {
            // 检测落地：MoveMent 在接地时将 isJumping 置为 false
            if (!movement.IsJumping)
            {
                // 恢复到进入 Jump 前的状态（Idle/Walk/Sprint）
                owner.ChangeState(owner.PreviousState);
            }
        }

        /// <summary>
        /// 物理更新：保持滞空水平速度，防止空气阻力减速。
        /// 不操作旋转 — 旋转移到 LateUpdate 由动画后处理接管。
        /// </summary>
        public override void FixedUpdate()
        {
            if (Rb == null || !movement.IsJumping) return;

            // 每帧清除刚体角速度，确保 LateUpdate 的 Slerp 不会与物理旋转竞争
            Rb.angularVelocity = Vector3.zero;

            // 保持滞空水平速度
            if (!IsGrounded())
            {
                Vector3 vel = Rb.linearVelocity;
                Vector3 horizontal = new Vector3(vel.x, 0, vel.z);
                if (horizontal.magnitude < _horizontalSpeed && _horizontalSpeed > 0.01f)
                {
                    horizontal = horizontal.normalized * _horizontalSpeed;
                    Rb.linearVelocity = new Vector3(horizontal.x, vel.y, horizontal.z);
                }
            }
        }

        /// <summary>
        /// 动画后处理：面向摄像机前方平滑旋转。
        /// 在 Animator 更新之后执行，保证每帧渲染的最终朝向与相机一致。
        /// 鼠标控制相机旋转 → 角色在空中实时跟随转向，提供流畅操控手感。
        /// </summary>
        public override void LateUpdate()
        {
            RotateTowardCameraForward();
        }

        public override void Exit()
        {
            // Debug.Log($"[PlayerFSM] 退出 Jump 状态");
        }
    }
}
