using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace PlayerFSM
{
    /// <summary>
    /// 玩家技能状态：播放技能动画，锁定控制。
    /// 动画结束后自动恢复前一状态。
    /// </summary>
    public class PlayerSkillState : PlayerStateBase
    {
        public override PlayerState StateType => PlayerState.Skill;

        private float _defaultDuration = 0.6f;

        private float _elapsed;
        private float _duration;
        private CancellationTokenSource _timeoutCts;

        public override void Enter()
        {
            Debug.Log($"[PlayerFSM] 进入 Skill 状态（阶段二：实际锁定）");

            anim.PlaySkill(_duration);
            movement.LockPlayerControl();
            movement.ForceStandUp();
            movement.CancelRoll();

            _elapsed = 0f;
            _duration = _defaultDuration;

            CancelCts(ref _timeoutCts);
            _timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
                owner.GetCancellationTokenOnDestroy());
            SkillTimeoutAsync(_timeoutCts.Token).Forget();
        }

        public override void Exit()
        {
            CancelCts(ref _timeoutCts);
            movement.UnlockPlayerControl();
            Debug.Log($"[PlayerFSM] 退出 Skill 状态");
        }

        public void SetDuration(float duration)
        {
            _defaultDuration = duration;
        }

        private async UniTaskVoid SkillTimeoutAsync(CancellationToken token)
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
