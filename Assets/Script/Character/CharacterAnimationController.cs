using System.Collections;
using UnityEngine;
using System;

/// <summary>
/// 通用的玩家/角色动画控制器：
/// - 安全地调用 Animator（如果缺失不报错）
/// - 暴露 PlayAttack/PlaySkill/PlayBuff 等方法供外部调用
/// - 在触发一次性动作时会临时清除移动相关 bool，避免移动动画与动作冲突
/// - 如果需要，可以通过 Animation Event 或外部逻辑来恢复移动状态
/// </summary>
public class CharacterAnimationController : MonoBehaviour
{
    [Header("Animator 引用")]
    [SerializeField] private Animator animator;

    [Header("Bool 参数（持续状态）")]
    // [SerializeField] private string runBool = "isRunning";
    [SerializeField] private string channelingBool = "IsChanneling"; // 新增：通道型攻击/技能在Loop阶段保持为true

    [Header("Trigger 参数（一次性动作）")]
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string skillTrigger = "Skill";
    [SerializeField] private string buffTrigger = "Buff"; // 用于 Buff/持续技能

    [Header("扩展 Trigger 参数（伤害/死亡）")]
    [SerializeField] private string hurtTrigger = "Hurt";
    [SerializeField] private string deathTrigger = "Death";

    [Header("Move Speed Float 参数（可选）")]
    [SerializeField] private string horizontalSpeedParam = "HSpeed"; // 水平速度（与项目一致）
    [SerializeField] private string verticalSpeedParam = "VSpeed";   // 垂直速度（与项目一致）

    [Header("其他动画参数（可选）")]
    [SerializeField] private string jumpTrigger = "Jump";
    [SerializeField] private string rollTrigger = "Roll";
    [SerializeField] private int crouchLayerIndex = 1;

    [Header("受伤叠加层（Layer）设置")]
    [Tooltip("受伤动画所在的层索引。Unity 层为 0 基，下标 2 表示第三个层。")]
    [SerializeField] private int hurtLayerIndex = 2; // 第三个 Layer（0 基）
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

    // 缓存 hash
    private int _attackTriggerHash;
    private int _skillTriggerHash;
    private int _buffTriggerHash;
    private int _deathTriggerHash;
    private int _hSpeedHash;
    private int _vSpeedHash;
    private int _jumpTriggerHash;
    private int _rollTriggerHash;
    private int _channelingBoolHash;

    // Allow inspector override for MoveMent, else try to find on self or parent
    [Header("Movement (optional)")]
    [Tooltip("可选：如果移动脚本不在同一对象上，可手动拖入 MoveMent 引用")]
    [SerializeField] private MoveMent movementOverride;

    // 连接 MoveMent（如果有）以便在动作期间锁定/解锁玩家控制
    private MoveMent _movement;

    // coroutine handle for auto-unlock（动作类）
    private Coroutine _unlockCoroutine;

    // 受伤层淡入淡出协程句柄
    private Coroutine _hurtLayerCoroutine;
    // 仅用于受伤临时锁的协程（不触发动画状态复位）
    private Coroutine _hurtUnlockCoroutine;

    // 事件：通道类攻击的前摇结束（进入Loop的逻辑窗口）
    public event Action AttackPrecastComplete; // 由动画事件 OnAttackPrecastComplete 调用

    // 事件：法术施法点（在单个施法动画的关键帧触发）
    public event Action SkillCastPointReached;

    // 内部死亡标记，避免通过错误的 Trigger Hash 比较
    private bool _isDead = false;

    private void Awake()
    {
        CacheHashes();
        // Resolve movement reference: prefer inspector override, otherwise try to find on self/parent
        if (movementOverride != null)
        {
            _movement = movementOverride;
        }
        else
        {
            _movement = GetComponent<MoveMent>() ?? GetComponentInParent<MoveMent>();
        }
    }

    private void CacheHashes()
    {
        _attackTriggerHash = string.IsNullOrEmpty(attackTrigger) ? 0 : Animator.StringToHash(attackTrigger);
        _skillTriggerHash = string.IsNullOrEmpty(skillTrigger) ? 0 : Animator.StringToHash(skillTrigger);
        _buffTriggerHash = string.IsNullOrEmpty(buffTrigger) ? 0 : Animator.StringToHash(buffTrigger);
        _deathTriggerHash = string.IsNullOrEmpty(deathTrigger) ? 0 : Animator.StringToHash(deathTrigger);
        _hSpeedHash = string.IsNullOrEmpty(horizontalSpeedParam) ? 0 : Animator.StringToHash(horizontalSpeedParam);
        _vSpeedHash = string.IsNullOrEmpty(verticalSpeedParam) ? 0 : Animator.StringToHash(verticalSpeedParam);
        _jumpTriggerHash = string.IsNullOrEmpty(jumpTrigger) ? 0 : Animator.StringToHash(jumpTrigger);
        _rollTriggerHash = string.IsNullOrEmpty(rollTrigger) ? 0 : Animator.StringToHash(rollTrigger);
        _channelingBoolHash = string.IsNullOrEmpty(channelingBool) ? 0 : Animator.StringToHash(channelingBool);
    }

    // 允许外部设置 Animator
    public void SetAnimator(Animator a)
    {
        animator = a;
        CacheHashes();
    }

    // 播放普通攻击：触发攻击 Trigger，同时清除移动 speed 参数以避免冲突（blend tree 恢复到 idle）
    public void PlayAttack()
    {
        PlayAttackInternal(0f, false);
    }

    // 可指定锁定时长（秒），>0 会在时长后自动解除锁定
    public void PlayAttack(float lockDuration)
    {
        PlayAttackInternal(lockDuration, true);
    }

    private void PlayAttackInternal(float lockDuration, bool useTimeout)
    {
        if (animator == null) return;
        // 清除移动/奔跑状态 -> since blend tree uses V/H speeds, set speeds to zero to return to idle
        SetMoveSpeeds(0f, 0f);
        if (_attackTriggerHash != 0) animator.SetTrigger(_attackTriggerHash);

        // 强制站立、取消翻滚
        _movement?.ForceStandUp();
        _movement?.CancelRoll();

        // 锁定控制
        if (_movement != null)
        {
            _movement.LockPlayerControl();
        }

        if (useTimeout && lockDuration > 0f)
        {
            if (_unlockCoroutine != null) StopCoroutine(_unlockCoroutine);
            _unlockCoroutine = StartCoroutine(UnlockAfterDelay(lockDuration));
        }
    }

    public void PlaySkill(float lockDuration)
    {
        PlaySkillInternal(lockDuration, true);
    }

    private void PlaySkillInternal(float lockDuration, bool useTimeout)
    {
        if (animator == null) return;
        SetMoveSpeeds(0f, 0f);
        if (_skillTriggerHash != 0) animator.SetTrigger(_skillTriggerHash);

        _movement?.ForceStandUp();
        _movement?.CancelRoll();

        // 锁定控制
        if (_movement != null)
        {
            _movement.LockPlayerControl();
        }

        if (useTimeout && lockDuration > 0f)
        {
            if (_unlockCoroutine != null) StopCoroutine(_unlockCoroutine);
            _unlockCoroutine = StartCoroutine(UnlockAfterDelay(lockDuration));
        }
    }

    public void PlayBuff(float lockDuration)
    {
        PlayBuffInternal(lockDuration, true);
    }

    private void PlayBuffInternal(float lockDuration, bool useTimeout)
    {
        if (animator == null) return;
        SetMoveSpeeds(0f, 0f);
        if (_buffTriggerHash != 0) animator.SetTrigger(_buffTriggerHash);

        _movement?.ForceStandUp();
        _movement?.CancelRoll();

        // 锁定控制
        if (_movement != null)
        {
            _movement.LockPlayerControl();
        }

        if (useTimeout && lockDuration > 0f)
        {
            if (_unlockCoroutine != null) StopCoroutine(_unlockCoroutine);
            _unlockCoroutine = StartCoroutine(UnlockAfterDelay(lockDuration));
        }
    }

    public void PlayHurt(float lockDuration = 0.25f)
    {
        // 使用受伤叠加层播放受伤效果，不再改变基础层状态或触发器
        if (animator == null || _isDead) return;

        // 是否处于通道型攻击中
        bool isChanneling = _channelingBoolHash != 0 && animator.GetBool(_channelingBoolHash);

        // 启动/重启受伤层叠加
        StartHurtOverlay();

        // 锁定控制：仅当未通道时才短暂锁定；且不要调用 OnActionAnimationEnd，避免误复位通道状态
        if (!isChanneling && lockControlOnHurtWhenNotChanneling && _movement != null)
        {
            _movement.LockPlayerControl();
            if (_hurtUnlockCoroutine != null) StopCoroutine(_hurtUnlockCoroutine);
            float duration = lockDuration > 0f ? lockDuration : hurtLockDuration;
            if (duration > 0f)
            {
                _hurtUnlockCoroutine = StartCoroutine(UnlockOnlyAfterDelay(duration));
            }
            else
            {
                // 若为 0，则立即解锁（保持行为与以往一致）
                _movement.UnlockPlayerControl();
            }
        }
    }

    public void PlayDeath()
    {
        if (animator == null) return;
        _isDead = true;
        // 受伤层立即归零
        if (_hurtLayerCoroutine != null) { StopCoroutine(_hurtLayerCoroutine); _hurtLayerCoroutine = null; }
        if (_hurtUnlockCoroutine != null) { StopCoroutine(_hurtUnlockCoroutine); _hurtUnlockCoroutine = null; }
        if (hurtLayerIndex >= 0 && hurtLayerIndex < animator.layerCount) animator.SetLayerWeight(hurtLayerIndex, 0f);

        SetMoveSpeeds(0f, 0f);
        if (_deathTriggerHash != 0) { animator.ResetTrigger(_deathTriggerHash); animator.SetTrigger(_deathTriggerHash); }
    }

    private void HandlePlayerDeath()
    {
        // 1) 禁用 PlayerInteraction（若存在）
        var interaction = GetComponent<PlayerInteraction>();
        if (interaction != null) interaction.enabled = false;

        // 2) Force full lock/stop movement
        ForceLockAfterDeath();
        // 尝试停止物理运动
        var rb = GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    public void ForceLockAfterDeath()
    {
        // 供外部（死亡逻辑）调用，确保彻底锁定并停止任何自动解锁协程
        if (_unlockCoroutine != null) { StopCoroutine(_unlockCoroutine); _unlockCoroutine = null; }
        if (_hurtUnlockCoroutine != null) { StopCoroutine(_hurtUnlockCoroutine); _hurtUnlockCoroutine = null; }
        _movement?.LockPlayerControl();
    }

    // ========= 通道型普攻（前摇-持续-后摇）入口 =========
    // 约定：Animator 中 Attack 子状态机包含 Pre/Loop/End 三段；
    // Pre -> Loop 条件：HasExitTime 且 IsChanneling == true；
    // Pre -> End 条件：HasExitTime 且 IsChanneling == false；
    // Loop -> End 条件：IsChanneling == false；End 结束后回到 Locomotion。

    public void BeginChannelAttack()
    {
        if (animator == null) return;
        // 清除移动/奔跑状态
        SetMoveSpeeds(0f, 0f);

        // 通知 Animator 进入 Attack-Pre；并声明意图：希望进入Loop
        if (_channelingBoolHash != 0) animator.SetBool(_channelingBoolHash, true);
        if (_attackTriggerHash != 0) animator.SetTrigger(_attackTriggerHash);

        // 强制站立与取消翻滚
        _movement?.ForceStandUp();
        _movement?.CancelRoll();

        // 锁定控制
        if (_movement != null)
        {
            _movement.LockPlayerControl();
        }
    }

    // 松开按键或被打断时调用：将 IsChanneling 置为 false，驱动动画从 Pre 或 Loop 进入 End
    public void EndChannelAttackRequest()
    {
        if (animator == null) return;
        if (_channelingBoolHash != 0) animator.SetBool(_channelingBoolHash, false);
        // 真正的解锁在 End 动画的最后通过 OnActionAnimationEnd 完成
    }

    // 供动画事件调用：在 Attack-Pre 动画的最后一帧发出，用于开始真正的技能效果（如发射射线）
    public void OnAttackPrecastComplete()
    {
        AttackPrecastComplete?.Invoke();
    }

    // 供动画事件调用：当施法动画播放到发射点时调用
    public void OnSkillCastPoint()
    {
        SkillCastPointReached?.Invoke();
    }

    // coroutine for auto-unlock
    private IEnumerator UnlockAfterDelay(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        OnActionAnimationEnd();
        _unlockCoroutine = null;
    }

    // 仅用于受伤短锁的解锁，不影响通道等动画参数
    private IEnumerator UnlockOnlyAfterDelay(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        _movement?.UnlockPlayerControl();
        _hurtUnlockCoroutine = null;
    }

    // 启动受伤叠加层（淡入-保持-淡出）
    private void StartHurtOverlay()
    {
        if (animator == null) return;
        if (hurtLayerIndex < 0 || hurtLayerIndex >= animator.layerCount) return;
        if (_hurtLayerCoroutine != null) StopCoroutine(_hurtLayerCoroutine);
        _hurtLayerCoroutine = StartCoroutine(HurtLayerRoutine());
    }

    private IEnumerator HurtLayerRoutine()
    {
        // 淡入
        yield return FadeLayerWeight(hurtLayerIndex, 1f, Mathf.Max(0f, hurtFadeIn));
        // 保持
        float hold = Mathf.Max(0f, hurtHold);
        if (hold > 0f) yield return new WaitForSecondsRealtime(hold);
        // 淡出
        yield return FadeLayerWeight(hurtLayerIndex, 0f, Mathf.Max(0f, hurtFadeOut));
        _hurtLayerCoroutine = null;
    }

    private IEnumerator FadeLayerWeight(int layer, float target, float duration)
    {
        float start = animator.GetLayerWeight(layer);
        if (Mathf.Approximately(duration, 0f))
        {
            animator.SetLayerWeight(layer, target);
            yield break;
        }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float w = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
            animator.SetLayerWeight(layer, w);
            yield return null;
        }
        animator.SetLayerWeight(layer, target);
    }

    // 可选：在动画结束时由 Animation Event 或外部逻辑调用以恢复移动状态
    public void RestoreMovingState(bool moving, bool running = false)
    {
        // We no longer use a separate move bool; movement is driven by VSpeed/HSpeed.
        // Restore running state and leave speed params to MoveMent/SetMoveSpeeds caller.
        // SetRunning(running);
    }

    // Animation Event / 回调：在动作动画（攻击/法术/Buff）播放结束时调用
    // 推荐在动画剪辑最后帧添加此 Event，确保移动与控制正确恢复
    public void OnActionAnimationEnd()
    {
        // 如果在自动解锁协程中，停止它
        if (_unlockCoroutine != null)
        {
            StopCoroutine(_unlockCoroutine);
            _unlockCoroutine = null;
        }

        // 结束时确保通道标志复位
        if (animator != null && _channelingBoolHash != 0)
        {
            animator.SetBool(_channelingBoolHash, false);
        }

        // 恢复 Animator 的移动/奔跑参数为当前输入状态（如果移动脚本可用）
        if (_movement != null)
        {
            // Do not set move bool; restore speeds and running flag
            float h = 0f, v = 0f; // rely on MoveMent to push speeds next frame
            SetMoveSpeeds(h, v);
        }

        // 解除控制锁，使移动脚本重新接管（注意：移动脚本的 OnMove 会在下一帧更新 Animator 参数）
        _movement?.UnlockPlayerControl();
    }

    // 强制结束当前动作并立即解锁控制（用于外部打断时调用）
    public void ForceEndActionImmediate()
    {
        // Stop any pending auto-unlock
        if (_unlockCoroutine != null)
        {
            StopCoroutine(_unlockCoroutine);
            _unlockCoroutine = null;
        }

        // 结束时确保通道标志复位
        if (animator != null && _channelingBoolHash != 0)
        {
            animator.SetBool(_channelingBoolHash, false);
        }

        // 恢复 Animator 的移动/奔跑参数为当前输入状态（如果移动脚本可用）
        if (_movement != null)
        {
            float h = 0f, v = 0f; // rely on MoveMent cached inputs; SetMoveSpeeds will be updated in UnlockPlayerControl
            SetMoveSpeeds(h, v);
        }

        // 立即解除控制锁
        _movement?.UnlockPlayerControl();
    }

    // 触发任意 Trigger（仅触发，不处理解锁定时器）
    public void TriggerByName(string triggerName)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName)) return;
        // 清除移动/奔跑状态 -> since blend tree uses V/H speeds, set speeds to zero
        SetMoveSpeeds(0f, 0f);
        if (!string.IsNullOrEmpty(triggerName))
        {
            int h = Animator.StringToHash(triggerName);
            animator.SetTrigger(h);
        }
        // 强制站立、取消翻滚
        _movement?.ForceStandUp();
        _movement?.CancelRoll();
    }

    // 显式锁定/解锁玩家控制（供技能流程在外部周期中使用）
    public void LockPlayerControl()
    {
        _movement?.LockPlayerControl();
    }
    public void UnlockPlayerControl()
    {
        _movement?.UnlockPlayerControl();
    }


    // 仅由移动系统调用：更新移动速度参数
    public void SetMoveSpeeds(float horizontal, float vertical)
    {
        if (animator == null) return;
        // 如果处于动作锁定阶段，忽略移动速度更新（避免打断动作）

        if (_movement != null && MoveMent.isControlLocked) return;
        // Debug.Log(horizontal + " " + vertical);
        if (_hSpeedHash != 0) animator.SetFloat(_vSpeedHash, horizontal);
        if (_vSpeedHash != 0) animator.SetFloat(_hSpeedHash, vertical);
        // if (_hSpeedHash != 0) animator.SetFloat(_hSpeedHash, horizontal);
        // if (_vSpeedHash != 0) animator.SetFloat(_vSpeedHash, vertical);

    }

    // 设置蹲伏状态（仅动画层权重；业务状态由 MoveMent 维护）
    public void SetCrouch(bool crouch)
    {
        if (animator == null) return;
        if (crouchLayerIndex >= 0)
        {
            animator.SetLayerWeight(crouchLayerIndex, crouch ? 1f : 0f);
        }
    }

    // 触发跳跃动画
    public void TriggerJump()
    {
        if (animator == null) return;
        if (_jumpTriggerHash != 0) animator.SetTrigger(_jumpTriggerHash);
    }

    // 触发翻滚动画（翻滚的 root motion 由 MoveMent.isRolling 控制 OnAnimatorMove 生效）
    public void TriggerRoll()
    {
        if (animator == null) return;
        if (_rollTriggerHash != 0) animator.SetTrigger(_rollTriggerHash);
    }

    // 供移动脚本调用：翻滚开始/结束时，原先用于切换 root motion 的逻辑已移除
    public void OnRollStart()
    {
        // 保持方法以便移动系统调用；不再在这里修改 Animator.applyRootMotion
    }

    public void OnRollEnd()
    {
        // 保持方法以便移动系统调用；不再在这里修改 Animator.applyRootMotion
    }

    // ========== 根运动已禁用 - 纯代码驱动移动 ==========
    // 【纯代码驱动】动画只负责视觉表现，移动完全由 MoveMent.FixedUpdate() 处理
    // private void OnAnimatorMove()
    // {
    //     if (!useRootMotion) return;
    //     if (animator == null) return;
    //     if (_movement == null) return;
    //     if (!animator.applyRootMotion) return;
    //
    //     Vector3 deltaPos = animator.deltaPosition;
    //     Quaternion rootRot = animator.rootRotation;
    //
    //     if (deltaPos.sqrMagnitude > 0.000001f || rootRot != Quaternion.identity)
    //     {
    //         _movement.ApplyPhysicsMotion(deltaPos, rootRot);
    //     }
    // }

    private void OnDisable()
    {
        // Ensure no stale lock remains if the controller gets disabled
        ForceEndActionImmediate();
        // 停止并归零受伤层
        if (_hurtLayerCoroutine != null) { StopCoroutine(_hurtLayerCoroutine); _hurtLayerCoroutine = null; }
        if (animator != null && hurtLayerIndex >= 0 && hurtLayerIndex < animator.layerCount) animator.SetLayerWeight(hurtLayerIndex, 0f);
    }

    private void OnDestroy()
    {
        // Ensure cleanup on destroy as well
        ForceEndActionImmediate();
        if (_hurtLayerCoroutine != null) { StopCoroutine(_hurtLayerCoroutine); _hurtLayerCoroutine = null; }
    }
}
