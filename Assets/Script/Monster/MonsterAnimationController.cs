using UnityEngine;

/// <summary>
/// 小型的怪物动画管理器（怪物用）：
/// - 触发 Hit/Attack/Death/Alert/Celebrate 等一次性动作；
/// - 现行项目：移动状态完全由 Blend Tree 的 VSpeed/HSpeed 控制；
/// - 一次性动作前会将 V/H 速度置 0，回到 Idle；
/// - 不再依赖 run/move 的 bool 参数。
/// </summary>
public class MonsterAnimationController : MonoBehaviour
{
    [Header("Animator 引用")]
    [SerializeField] private Animator animator;

    [Header("Blend Tree 移动参数（现行使用）")]
    [Tooltip("Blend Tree 垂直速度参数名（前进为正，后退为负）")]
    [SerializeField] private string verticalSpeedParam = "VSpeed";
    [Tooltip("Blend Tree 水平速度参数名（右移为正，左移为负）")]
    [SerializeField] private string horizontalSpeedParam = "HSpeed";

    [Header("Trigger 参数（一次性动作）")]
    [SerializeField] private string hitTrigger = "Hit";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string deathTrigger = "Death";
    [SerializeField] private string idleTrigger = "Idle"; // 可选
    [SerializeField] private string celebrateTrigger = "Celebrate";
    [SerializeField] private string alertTrigger = "Alert"; // 可选

    [Header("行为设置")]
    [Tooltip("播放一次性动作（Hit/Attack/Death/Alert）前，是否先将 V/H Speed 置 0 回 Idle")]
    [SerializeField] private bool stopMovementOnOneShot = true;

    [Header("攻击随机动画")]
    [Tooltip("攻击动画索引参数名（用于随机选择攻击动画）")]
    [SerializeField] private string attackIndexParam = "AttackIndex";
    [Tooltip("攻击动画数量")]
    [SerializeField] private int attackAnimationCount = 3;

    private int _hitTriggerHash;
    private int _attackTriggerHash;
    private int _deathTriggerHash;
    private int _idleTriggerHash;
    private int _celebrateTriggerHash;
    private int _alertTriggerHash;
    private int _vSpeedHash;
    private int _hSpeedHash;
    private int _attackIndexHash;

    private void Awake()
    {
        CacheHashes();
    }

    private void CacheHashes()
    {
        _hitTriggerHash = string.IsNullOrEmpty(hitTrigger) ? 0 : Animator.StringToHash(hitTrigger);
        _attackTriggerHash = string.IsNullOrEmpty(attackTrigger) ? 0 : Animator.StringToHash(attackTrigger);
        _deathTriggerHash = string.IsNullOrEmpty(deathTrigger) ? 0 : Animator.StringToHash(deathTrigger);
        _idleTriggerHash = string.IsNullOrEmpty(idleTrigger) ? 0 : Animator.StringToHash(idleTrigger);
        _celebrateTriggerHash = string.IsNullOrEmpty(celebrateTrigger) ? 0 : Animator.StringToHash(celebrateTrigger);
        _alertTriggerHash = string.IsNullOrEmpty(alertTrigger) ? 0 : Animator.StringToHash(alertTrigger);
        _vSpeedHash = string.IsNullOrEmpty(verticalSpeedParam) ? 0 : Animator.StringToHash(verticalSpeedParam);
        _hSpeedHash = string.IsNullOrEmpty(horizontalSpeedParam) ? 0 : Animator.StringToHash(horizontalSpeedParam);
        _attackIndexHash = string.IsNullOrEmpty(attackIndexParam) ? 0 : Animator.StringToHash(attackIndexParam);
    }

    // 置零 V/H 速度，确保回 Idle
    private void ZeroSpeeds()
    {
        if (animator == null) return;
        if (_vSpeedHash != 0) animator.SetFloat(_vSpeedHash, 0f);
        if (_hSpeedHash != 0) animator.SetFloat(_hSpeedHash, 0f);
    }

    // 播放受击
    public void PlayHit()
    {
        if (animator == null) return;
        if (stopMovementOnOneShot) ZeroSpeeds();
        if (_hitTriggerHash != 0)
        {
            animator.ResetTrigger(_hitTriggerHash);
            animator.SetTrigger(_hitTriggerHash);
        }
    }

    // 播放攻击（随机选择攻击动画）
    public void PlayAttack()
    {
        if (animator == null) return;
        if (stopMovementOnOneShot) ZeroSpeeds();
        // 随机选择攻击动画索引 0～attackAnimationCount-1
        if (_attackIndexHash != 0 && attackAnimationCount > 1)
        {
            animator.SetInteger(_attackIndexHash, Random.Range(0, attackAnimationCount));
        }
        if (_attackTriggerHash != 0)
        {
            animator.ResetTrigger(_attackTriggerHash);
            animator.SetTrigger(_attackTriggerHash);
        }
    }

    // 播放死亡
    public void PlayDeath()
    {
        if (animator == null) return;
        if (stopMovementOnOneShot) ZeroSpeeds();
        if (_deathTriggerHash != 0)
        {
            animator.ResetTrigger(_deathTriggerHash);
            animator.SetTrigger(_deathTriggerHash);
        }
    }

    // 播放待机（可选触发 Idle Trigger；V/H=0 即是 Idle）
    public void PlayIdle()
    {
        if (animator == null) return;
        ZeroSpeeds();
        if (_idleTriggerHash != 0)
        {
            animator.SetTrigger(_idleTriggerHash);
        }
    }

    public void PlayCelebrate()
    {
        if (animator == null) return;
        if (stopMovementOnOneShot) ZeroSpeeds();
        if (_celebrateTriggerHash != 0)
        {
            animator.ResetTrigger(_celebrateTriggerHash);
            animator.SetTrigger(_celebrateTriggerHash);
        }
    }

    public void PlayAlert()
    {
        if (animator == null) return;
        if (stopMovementOnOneShot) ZeroSpeeds();
        if (_alertTriggerHash != 0)
        {
            animator.ResetTrigger(_alertTriggerHash);
            animator.SetTrigger(_alertTriggerHash);
        }
        else
        {
            PlayIdle();
        }
    }

    public void SetAnimator(Animator a)
    {
        animator = a;
        CacheHashes();
    }
}
