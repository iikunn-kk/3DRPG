using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace PlayerFSM
{
    /// <summary>
    /// 玩家 Buff 状态：播放 Buff 动画，锁定控制。
    /// 动画结束后自动恢复前一状态。
    /// </summary>
    public class PlayerBuffState : PlayerStateBase
    {
        public override PlayerState StateType => PlayerState.Buff;

        private float _defaultDuration = 0.4f;

        private float _duration;
        private CancellationTokenSource _timeoutCts;

        public override void Enter()
        {
            // Debug.Log($"[PlayerFSM] 进入 Buff 状态（阶段二：实际锁定）");

            anim.PlayBuff(_duration);
            movement.LockPlayerControl();
            movement.ForceStandUp();
            movement.CancelRoll();

            _duration = _defaultDuration;

            CancelCts(ref _timeoutCts);
            _timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
                owner.GetCancellationTokenOnDestroy());
            BuffTimeoutAsync(_timeoutCts.Token).Forget();
        }

        public override void Exit()
        {
            CancelCts(ref _timeoutCts);
            movement.UnlockPlayerControl();
            // Debug.Log($"[PlayerFSM] 退出 Buff 状态");
        }

        public void SetDuration(float duration)
        {
            _defaultDuration = duration;
        }

        private async UniTaskVoid BuffTimeoutAsync(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(_duration), ignoreTimeScale: false,
                    cancellationToken: token);
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
