using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 奥术智慧运行时控制器，挂在玩家对象上。
/// 管理：10级时暴击触发的加倍效果、内部冷却、刷新与过期。
/// 该组件由 ArcaneIntellectSkill 在施放时创建，Buff 到期后会被移除。
/// </summary>
public class ArcaneIntellectRuntime : MonoBehaviour
{
    private string _source;
    private string _doubleSource;
    private int _baseAttackAdd;
    private int _level;
    private float _duration;

    private CharacterState _cs;
    private CharacterBuffs _cb;

    private CancellationTokenSource _lifeCts;
    private bool _icdActive;
    private CancellationTokenSource _icdCts;

    private const float DoubleDurationSeconds = 3f;
    private const float DoubleInternalCooldown = 8f;

    public void Setup(string source, int baseAttackAdd, int level, float duration, CharacterState cs, CharacterBuffs cb)
    {
        _source = source;
        _doubleSource = source + "_double";
        _baseAttackAdd = baseAttackAdd;
        _level = level;
        _duration = duration;
        _cs = cs;
        _cb = cb;

        // 仅在等级 >= 10 时订阅暴击事件
        if (_level >= 10 && _cs != null)
        {
            _cs.OnPlayerCriticalHit += OnPlayerCriticalHit;
        }

        _lifeCts?.Cancel();
        _lifeCts?.Dispose();
        _lifeCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        LifeRoutineAsync(_duration, _lifeCts.Token).Forget();
    }

    public void Refresh(int baseAttackAdd, int level, float duration)
    {
        // 更新参数并重置生存计时
        _baseAttackAdd = baseAttackAdd;
        _level = level;
        _duration = duration;

        _lifeCts?.Cancel();
        _lifeCts?.Dispose();
        _lifeCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        LifeRoutineAsync(_duration, _lifeCts.Token).Forget();

        // 确保事件订阅状态与等级一致
        if (_cs != null)
        {
            if (_level >= 10)
                _cs.OnPlayerCriticalHit -= OnPlayerCriticalHit; // 安全移除再添加，防止重复订阅
            if (_level >= 10)
                _cs.OnPlayerCriticalHit += OnPlayerCriticalHit;
        }
    }

    private async UniTaskVoid LifeRoutineAsync(float dur, CancellationToken token)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, dur)), cancellationToken: token);
            Expire();
        }
        catch (OperationCanceledException) { }
    }

    private void OnPlayerCriticalHit()
    {
        if (_level < 10) return;
        if (_icdActive) return;
        if (_cb == null) return;
        if (_baseAttackAdd <= 0) return;

        // 触发临时翻倍：添加与主加成相同的 AttackFlat，持续3秒
        _cb.ApplyBuff(_doubleSource, CharacterBuffs.BuffType.AttackFlat, _baseAttackAdd, DoubleDurationSeconds);

        // 启动内置冷却（ICD）
        _icdActive = true;
        _icdCts?.Cancel();
        _icdCts?.Dispose();
        _icdCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        ResetIcdAfterAsync(DoubleInternalCooldown, _icdCts.Token).Forget();
    }

    private async UniTaskVoid ResetIcdAfterAsync(float sec, CancellationToken token)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(sec), cancellationToken: token);
            _icdActive = false;
        }
        catch (OperationCanceledException) { }
    }

    private void Expire()
    {
        // 移除此 source 及其翻倍源引入的所有数值 Buff
        if (_cb != null)
        {
            _cb.RemoveBuffBySource(_doubleSource);
            _cb.RemoveBuffBySource(_source);
            // 同时确保视觉效果被清理
            _cb.UnregisterBuffVisual(_source);
        }
        if (_cs != null)
        {
            _cs.OnPlayerCriticalHit -= OnPlayerCriticalHit;
        }
        // 销毁自身（组件）
        Destroy(this);
    }

    private void OnDestroy()
    {
        // 若组件被外部销毁，做些额外清理以防残留
        if (_cb != null)
        {
            _cb.RemoveBuffBySource(_doubleSource);
            // 注意：不在这里无条件移除主 source，以免与 Skill 的主清理冲突
        }
        if (_cs != null)
        {
            _cs.OnPlayerCriticalHit -= OnPlayerCriticalHit;
        }
    }
}
