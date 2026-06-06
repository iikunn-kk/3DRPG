using UnityEngine;

namespace PlayerFSM
{
    /// <summary>
    /// 死亡状态：终态，最高优先级。
    /// Enter: 播放死亡动画 + 切碰撞层 + 锁定移动
    /// Exit:  恢复碰撞层 + 解锁移动
    /// </summary>
    public class PlayerDeathState : PlayerStateBase
    {
        public override PlayerState StateType => PlayerState.Death;

        public override void Enter()
        {
            // 动画表现
            anim?.PlayDeath();
            anim?.ForceLockAfterDeath();
            // 物理层 + 交互禁用 + 锁定移动（一次性由 CharacterState 处理）
            characterState?.ApplyDeadLayerAndDisableInteraction();
        }

        public override void Exit()
        {
            // 复活时恢复动画（层和交互已在 PerformRuntimeRespawn 恢复）
            anim?.ResetFromDeath();
        }

        public override void Update()
        {
            // 终态，无帧逻辑
        }

        public override void CheckTransitions()
        {
            // 由 OnRespawn() 外部触发复活
        }

        // /// <summary>
        // /// 外部调用：标记复活（供 CharacterState 复活后调用）
        // /// </summary>
        // public void OnRespawn()
        // {
        //     if (owner.CurrentState == PlayerState.Death)
        //         owner.ChangeState(PlayerState.Idle);
        // }
    }
}
