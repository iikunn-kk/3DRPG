using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace PlayerFSM
{
    /// <summary>
    /// 玩家攻击状态：播放攻击动画，锁定控制。
    /// 动画结束后自动恢复前一状态（Idle/Walk/Sprint/Crouch）。
    /// </summary>
    public class PlayerAttackState : PlayerStateBase
    {
        public override PlayerState StateType => PlayerState.Attack;

        private float _defaultDuration = 0.5f;

        private float _elapsed;
        private float _duration;
        private CancellationTokenSource _timeoutCts;

        public override void Enter()
        {
            // Debug.Log($"[PlayerFSM] 进入 Attack 状态（阶段二：实际锁定）");

            // 播放攻击动画（无 Lock/Unlock — FSM 接管）
            anim.PlayAttack();

            // 锁定控制
            movement.LockPlayerControl();

            // 强制站立、取消翻滚
            movement.ForceStandUp();
            movement.CancelRoll();

            // 启动自动超时（攻击动画时长）
            _elapsed = 0f;
            _duration = _defaultDuration;

            CancelCts(ref _timeoutCts);
            _timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
                owner.GetCancellationTokenOnDestroy());
            AttackTimeoutAsync(_timeoutCts.Token).Forget();
        }

        public override void Update()
        {
            // 主循环由 UniTask 驱动的定时器控制，Update 中无需额外逻辑
        }

        public override void Exit()
        {
            CancelCts(ref _timeoutCts);
            movement.UnlockPlayerControl();
            // Debug.Log($"[PlayerFSM] 退出 Attack 状态");
        }

        /// <summary>
        /// 设置本次攻击的持续时间（供外部 SkillController 调用）。
        /// </summary>
        public void SetDuration(float duration)
        {
            _defaultDuration = duration;
        }

        private async UniTaskVoid AttackTimeoutAsync(CancellationToken token)
        {
            try
            {
                // 等待攻击动画时长
                await UniTask.Delay(System.TimeSpan.FromSeconds(_duration), ignoreTimeScale: false,
                    cancellationToken: token);

                // 超时后回到前一状态
                owner.ChangeState(owner.PreviousState);
            }
            catch (System.OperationCanceledException) { }
        }

        private void CancelCts(ref CancellationTokenSource cts)
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }
        }
    }
}
