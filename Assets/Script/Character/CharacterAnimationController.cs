using UnityEngine;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// 通用的玩家/角色动画控制器（代码直接驱动版）：
/// - 所有动画切换使用 CrossFadeInFixedTime()，不再依赖 Animator Trigger 连接
/// - 混合树移动仍使用 SetFloat(HSpeed/VSpeed)
/// - 通道攻击使用 SetBool(IsChanneling)
/// - 受伤使用叠加层（Layer Weight）
/// </summary>
public class CharacterAnimationController : MonoBehaviour
{
    [Header("Animator 引用")]
    [SerializeField] private Animator animator;

    [Header("Bool 参数（持续状态）")]
    [SerializeField] private string channelingBool = "IsChanneling";

    // ───── 状态名称（代码直接驱动 CrossFadeInFixedTime）─────
    [Header("动画状态名称")]
    [SerializeField] private string locomotionState = "Movement";     // Base Layer 默认状态（混合树）
    [SerializeField] private string deathState = "Death";
    [SerializeField] private string attackState = "Attack";
    [SerializeField] private string skillState = "Skill";
    [SerializeField] private string buffState = "Buff";
    [SerializeField] private string jumpState = "Jump";
    [SerializeField] private string rollState = "Roll";

    [Header("移动速度 Float 参数")]
    [SerializeField] private string horizontalSpeedParam = "HSpeed";
    [SerializeField] private string verticalSpeedParam = "VSpeed";

    [Header("其他动画参数")]
    [SerializeField] private int crouchLayerIndex = 1;

    [Header("受伤叠加层设置")]
    [Tooltip("受伤动画所在的层索引。")]
    [SerializeField] private int hurtLayerIndex = 2;
    [Tooltip("受伤层权重淡入时长（秒）")]
    [SerializeField] private float hurtFadeIn = 0.08f;
    [Tooltip("受伤层保持满权重的时长（秒）")]
    [SerializeField] private float hurtHold = 0.18f;
    [Tooltip("受伤层权重淡出时长（秒）")]
    [SerializeField] private float hurtFadeOut = 0.12f;
    [Tooltip("当未处于通道攻击时，受伤是否短暂锁定移动控制")]
    [SerializeField] private bool lockControlOnHurtWhenNotChanneling = true;
    [Tooltip("受伤时的短暂锁定时长（秒，仅在未通道时生效）")]
    [SerializeField] private float hurtLockDuration = 0.25f;

    [Header("动画过渡时间")]
    [Tooltip("死亡过渡时间")]
    [SerializeField] private float deathCrossFade = 0.15f;
    [Tooltip("复活回到移动过渡时间")]
    [SerializeField] private float respawnCrossFade = 0.15f;
    [Tooltip("攻击过渡时间")]
    [SerializeField] private float attackCrossFade = 0.1f;
    [Tooltip("技能过渡时间")]
    [SerializeField] private float skillCrossFade = 0.1f;
    [Tooltip("Buff过渡时间")]
    [SerializeField] private float buffCrossFade = 0.1f;
    [Tooltip("跳跃过渡时间")]
    [SerializeField] private float jumpCrossFade = 0.1f;
    [Tooltip("翻滚过渡时间")]
    [SerializeField] private float rollCrossFade = 0.08f;

    // ───── 缓存 hash（仅 Float/Bool + 攻击 Trigger）─────
    private int _hSpeedHash;
    private int _vSpeedHash;
    private int _channelingBoolHash;
    private int _attackTriggerHash; // Attack 是子状态机，只能通过 Trigger 进入

    // Allow inspector override for MoveMent, else try to find on self or parent
    [Header("Movement (optional)")]
    [SerializeField] private MoveMent movementOverride;

    private MoveMent _movement;
    private Rigidbody _rb;  // 死亡时冻结物理旋转
    private CancellationTokenSource _hurtLayerCts;
    private CancellationTokenSource _hurtUnlockCts;
    private bool _isDead = false;

    public event Action AttackPrecastComplete;
    public event Action SkillCastPointReached;

    private void Awake()
    {
        CacheHashes();
        if (movementOverride != null)
            _movement = movementOverride;
        else
            _movement = GetComponent<MoveMent>() ?? GetComponentInParent<MoveMent>();
        _rb = GetComponentInChildren<Rigidbody>();
    }

    private void CacheHashes()
    {
        _hSpeedHash = string.IsNullOrEmpty(horizontalSpeedParam) ? 0 : Animator.StringToHash(horizontalSpeedParam);
        _vSpeedHash = string.IsNullOrEmpty(verticalSpeedParam) ? 0 : Animator.StringToHash(verticalSpeedParam);
        _channelingBoolHash = string.IsNullOrEmpty(channelingBool) ? 0 : Animator.StringToHash(channelingBool);
        _attackTriggerHash = string.IsNullOrEmpty(attackState) ? 0 : Animator.StringToHash(attackState);
    }

    public void SetAnimator(Animator a)
    {
        animator = a;
        CacheHashes();
    }

    // ═════════════════════════════════════════════
    //  通用辅助
    // ═════════════════════════════════════════════

    /// <summary>安全调用 CrossFadeInFixedTime</summary>
    private void SafeCrossFade(string stateName, float duration, int layer = 0)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;
        animator.CrossFadeInFixedTime(stateName, duration, layer);
    }

    // ═════════════════════════════════════════════
    //  死亡 / 复活（代码直接驱动）
    // ═════════════════════════════════════════════

    public void PlayDeath()
    {
        if (animator == null) return;
        _isDead = true;

        CancelCts(ref _hurtLayerCts);
        CancelCts(ref _hurtUnlockCts);
        if (hurtLayerIndex >= 0 && hurtLayerIndex < animator.layerCount)
            animator.SetLayerWeight(hurtLayerIndex, 0f);

        // 冻结物理旋转，防止死后角色原地转圈
        if (_rb != null)
        {
            _rb.angularVelocity = Vector3.zero;
            _rb.linearVelocity = Vector3.zero;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        SetMoveSpeeds(0f, 0f);
        SafeCrossFade(deathState, deathCrossFade);
    }

    /// <summary>复活时重置动画（由 PerformRuntimeRespawn → DeathState.Exit 调用）</summary>
    public void ResetFromDeath()
    {
        _isDead = false;
        if (animator == null) return;

        // 清理通道标志
        if (_channelingBoolHash != 0)
            animator.SetBool(_channelingBoolHash, false);

        // 解冻物理旋转
        if (_rb != null)
            _rb.constraints = RigidbodyConstraints.None;

        // 直跳回 Locomotion 混合树
        SafeCrossFade(locomotionState, respawnCrossFade);
        SetMoveSpeeds(0f, 0f);
    }

    // ═════════════════════════════════════════════
    //  攻击（单次 + 通道）
    // ═════════════════════════════════════════════

    public void PlayAttack()
    {
        if (animator == null) return;
        SetMoveSpeeds(0f, 0f);
        // Attack 是子状态机 → 只能用 Trigger 通过 Any State 连线进入
        if (_attackTriggerHash != 0) animator.SetTrigger(_attackTriggerHash);
        _movement?.ForceStandUp();
        _movement?.CancelRoll();
    }

    public void PlayAttack(float lockDuration)
    {
        PlayAttack();
    }

    public void BeginChannelAttack()
    {
        if (animator == null) return;
        SetMoveSpeeds(0f, 0f);
        if (_channelingBoolHash != 0) animator.SetBool(_channelingBoolHash, true);
        // Attack 是子状态机 → 只能用 Trigger
        if (_attackTriggerHash != 0) animator.SetTrigger(_attackTriggerHash);
        _movement?.ForceStandUp();
        _movement?.CancelRoll();
    }

    public void EndChannelAttackRequest()
    {
        if (animator == null) return;
        if (_channelingBoolHash != 0) animator.SetBool(_channelingBoolHash, false);
    }

    // ═════════════════════════════════════════════
    //  技能
    // ═════════════════════════════════════════════

    public void PlaySkill(float lockDuration)
    {
        if (animator == null) return;
        SetMoveSpeeds(0f, 0f);
        SafeCrossFade(skillState, skillCrossFade);

        _movement?.ForceStandUp();
        _movement?.CancelRoll();
    }

    // ═════════════════════════════════════════════
    //  Buff
    // ═════════════════════════════════════════════

    public void PlayBuff(float lockDuration)
    {
        if (animator == null) return;
        SetMoveSpeeds(0f, 0f);
        SafeCrossFade(buffState, buffCrossFade);

        _movement?.ForceStandUp();
        _movement?.CancelRoll();
    }

    // ═════════════════════════════════════════════
    //  受伤（叠加层，不需改）
    // ═════════════════════════════════════════════

    public void PlayHurt(float lockDuration = 0.25f)
    {
        if (animator == null || _isDead) return;

        bool isChanneling = _channelingBoolHash != 0 && animator.GetBool(_channelingBoolHash);
        StartHurtOverlay();

        if (!isChanneling && lockControlOnHurtWhenNotChanneling && _movement != null)
        {
            _movement.LockPlayerControl();
            CancelCts(ref _hurtUnlockCts);
            float duration = lockDuration > 0f ? lockDuration : hurtLockDuration;
            if (duration > 0f)
            {
                _hurtUnlockCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
                UnlockOnlyAfterDelayAsync(duration, _hurtUnlockCts.Token).Forget();
            }
            else
            {
                _movement.UnlockPlayerControl();
            }
        }
    }

    // ═════════════════════════════════════════════
    //  跳跃 / 翻滚
    // ═════════════════════════════════════════════

    public void TriggerJump()
    {
        if (animator == null) return;
        SafeCrossFade(jumpState, jumpCrossFade);
    }

    public void TriggerRoll()
    {
        if (animator == null) return;
        SafeCrossFade(rollState, rollCrossFade);
    }

    // ═════════════════════════════════════════════
    //  通用触发（供外部通过名称触发）
    // ═════════════════════════════════════════════

    public void TriggerByName(string name)
    {
        if (animator == null || string.IsNullOrEmpty(name)) return;
        SetMoveSpeeds(0f, 0f);

        int hash = Animator.StringToHash(name);
        // 优先 CrossFade（状态名如 ComboKick1），失败回退 SetTrigger（触发器如 ChainKick1）
        if (animator.HasState(0, hash))
            animator.CrossFadeInFixedTime(name, 0.1f, 0);
        else
            animator.SetTrigger(hash);

        _movement?.ForceStandUp();
        _movement?.CancelRoll();
    }

    // ═════════════════════════════════════════════
    //  移动速度（混合树）
    // ═════════════════════════════════════════════

    public void SetMoveSpeeds(float horizontal, float vertical)
    {
        if (animator == null) return;
        if (_movement != null && _movement.IsControlLocked()) return;
        if (_hSpeedHash != 0) animator.SetFloat(_vSpeedHash, horizontal);
        if (_vSpeedHash != 0) animator.SetFloat(_hSpeedHash, vertical);
    }

    public void SetCrouch(bool crouch)
    {
        if (animator == null) return;
        if (crouchLayerIndex >= 0)
            animator.SetLayerWeight(crouchLayerIndex, crouch ? 1f : 0f);
    }

    // ═════════════════════════════════════════════
    //  锁定/解锁（供 FSM 状态类调用）
    // ═════════════════════════════════════════════

    public void LockPlayerControl()
        => _movement?.LockPlayerControl();

    public void UnlockPlayerControl()
        => _movement?.UnlockPlayerControl();

    public void ForceLockAfterDeath()
    {
        CancelCts(ref _hurtUnlockCts);
        _movement?.LockPlayerControl();
    }

    // ═════════════════════════════════════════════
    //  动画事件回调
    // ═════════════════════════════════════════════

    public void OnActionAnimationEnd()
    {
        if (animator != null && _channelingBoolHash != 0)
            animator.SetBool(_channelingBoolHash, false);

        if (_movement != null)
            SetMoveSpeeds(0f, 0f);
    }

    public void ForceEndActionImmediate()
    {
        if (animator != null && _channelingBoolHash != 0)
            animator.SetBool(_channelingBoolHash, false);

        if (_movement != null)
            SetMoveSpeeds(0f, 0f);
    }

    public void OnAttackPrecastComplete()
        => AttackPrecastComplete?.Invoke();

    public void OnSkillCastPoint()
        => SkillCastPointReached?.Invoke();

    public void RestoreMovingState(bool moving, bool running = false) { }
    public void OnRollStart() { }
    public void OnRollEnd() { }

    // ═════════════════════════════════════════════
    //  私有：受伤叠加层
    // ═════════════════════════════════════════════

    private void StartHurtOverlay()
    {
        if (animator == null) return;
        if (hurtLayerIndex < 0 || hurtLayerIndex >= animator.layerCount) return;
        CancelCts(ref _hurtLayerCts);
        _hurtLayerCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        HurtLayerRoutineAsync(_hurtLayerCts.Token).Forget();
    }

    private async UniTaskVoid HurtLayerRoutineAsync(CancellationToken token)
    {
        try
        {
            await FadeLayerWeightAsync(hurtLayerIndex, 1f, Mathf.Max(0f, hurtFadeIn), token);
            float hold = Mathf.Max(0f, hurtHold);
            if (hold > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(hold), ignoreTimeScale: true, cancellationToken: token);
            await FadeLayerWeightAsync(hurtLayerIndex, 0f, Mathf.Max(0f, hurtFadeOut), token);
        }
        catch (OperationCanceledException) { }
    }

    private async UniTask FadeLayerWeightAsync(int layer, float target, float duration, CancellationToken token)
    {
        float start = animator.GetLayerWeight(layer);
        if (Mathf.Approximately(duration, 0f))
        {
            animator.SetLayerWeight(layer, target);
            return;
        }
        float t = 0f;
        while (t < duration)
        {
            token.ThrowIfCancellationRequested();
            t += Time.unscaledDeltaTime;
            animator.SetLayerWeight(layer, Mathf.Lerp(start, target, Mathf.Clamp01(t / duration)));
            await UniTask.Yield(token);
        }
        animator.SetLayerWeight(layer, target);
    }

    private async UniTaskVoid UnlockOnlyAfterDelayAsync(float seconds, CancellationToken token)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: true, cancellationToken: token);
            _movement?.UnlockPlayerControl();
        }
        catch (OperationCanceledException) { }
    }

    // ═════════════════════════════════════════════
    //  生命周期
    // ═════════════════════════════════════════════

    private void OnDisable()
    {
        ForceEndActionImmediate();
        CancelCts(ref _hurtLayerCts);
        if (animator != null && hurtLayerIndex >= 0 && hurtLayerIndex < animator.layerCount)
            animator.SetLayerWeight(hurtLayerIndex, 0f);
    }

    private void OnDestroy()
    {
        ForceEndActionImmediate();
        CancelCts(ref _hurtLayerCts);
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
