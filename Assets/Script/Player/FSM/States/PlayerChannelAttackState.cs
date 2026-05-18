using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace PlayerFSM
{
    /// <summary>
    /// 通道攻击状态：Pre-Loop-End 三段式。
    /// - Enter: BeginChannelAttack(锁定+IsChanneling=true)
    /// - 通道中: 维持锁定，等待外部（NormalAttackController）通过 FSM.RequestEndChannel() 释放
    /// - 释放: EndChannelAttackRequest + 后摇等待 → 恢复前一状态
    /// </summary>
    public class PlayerChannelAttackState : PlayerStateBase
    {
        public override PlayerState StateType => PlayerState.ChannelAttack;

        private float _endAnimDuration = 0.3f; // End 动画时长（后摇）

        private bool _isChanneling;
        private CancellationTokenSource _endAnimCts;

        public override void Enter()
        {
            Debug.Log($"[PlayerFSM] 进入 ChannelAttack 状态（阶段二：实际锁定）");

            // 开始通道（锁定 + IsChanneling=true）
            anim.BeginChannelAttack();
            movement.LockPlayerControl();
            movement.ForceStandUp();
            movement.CancelRoll();

            _isChanneling = true;
        }

        public override void Update()
        {
            // 由外部（NormalAttackController）通过 RequestEndChannel 触发结束
        }

        public override void Exit()
        {
            CancelCts(ref _endAnimCts);
            movement.UnlockPlayerControl();
            _isChanneling = false;
            Debug.Log($"[PlayerFSM] 退出 ChannelAttack 状态");
        }

        /// <summary>
        /// 外部调用：结束通道（由 NormalAttackController 在鼠标松开或打断时调用）。
        /// </summary>
        public void EndChannel()
        {
            if (!_isChanneling) return;
            _isChanneling = false;

            // 请求结束通道动画
            anim.EndChannelAttackRequest();

            // 等待后摇结束后恢复
            CancelCts(ref _endAnimCts);
            _endAnimCts = CancellationTokenSource.CreateLinkedTokenSource(
                owner.GetCancellationTokenOnDestroy());
            EndAnimTimeoutAsync(_endAnimCts.Token).Forget();
        }

        private async UniTaskVoid EndAnimTimeoutAsync(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(_endAnimDuration),
                    ignoreTimeScale: false, cancellationToken: token);
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
