using UnityEngine;

namespace PlayerFSM
{
    /// <summary>
    /// 翻滚状态：物理驱动滚动位移，带冷却。
    /// 物理逻辑由 MoveMent 处理（HandleRoll 驱动位移）。
    /// FSM 负责状态标记和翻滚结束恢复。
    /// </summary>
    public class PlayerRollState : PlayerStateBase
    {
        public override PlayerState StateType => PlayerState.Roll;

        public override void Enter()
        {
            Debug.Log($"[PlayerFSM] 进入 Roll 状态（物理由 MoveMent 驱动）");
        }

        public override void Update()
        {
            // 检测翻滚结束：MoveMent 在翻滚结束时将 isRolling 置为 false
            if (!movement.IsRolling)
            {
                owner.ChangeState(owner.PreviousState);
            }
        }

        public override void Exit()
        {
            Debug.Log($"[PlayerFSM] 退出 Roll 状态");
        }
    }
}
