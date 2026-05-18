using UnityEngine;

namespace PlayerFSM
{
    /// <summary>
    /// 蹲伏状态：按住 Crouch，速度减半。
    /// 可切换到：Idle（松开 Crouch）、Jump(蹲伏中不跳)、Roll。
    /// </summary>
    public class PlayerCrouchState : PlayerStateBase
    {
        public override PlayerState StateType => PlayerState.Crouch;

        public override void Enter()
        {
            Debug.Log("[PlayerFSM] 进入 Crouch 状态");
        }

        public override void Update() { }

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
            Debug.Log("[PlayerFSM] 退出 Crouch 状态");
        }
    }
}
