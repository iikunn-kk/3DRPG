using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using PlayerFSM;

/*
普通攻击（奥术射线）输入转发（前摇-持续-后摇版本）：
- 按下：NormalAttackController 仅做使用校验和朝向处理，然后调用 CharacterAnimationController.BeginChannelAttack() 进入前摇；
- 前摇末尾：由动画事件调用 CharacterAnimationController.OnAttackPrecastComplete()，触发 AttackPrecastComplete 事件；本脚本监听该事件并在仍按住时激活 ArcaneRaySkill；
- 持续阶段：玩家持续按住鼠标，射线持续生效；
- 松开：停止射线并调用 CharacterAnimationController.EndChannelAttackRequest()，驱动动画进入后摇；
- 后摇末尾：在 End 动画最后一帧调用 CharacterAnimationController.OnActionAnimationEnd() 解锁移动与根运动恢复。

注意：
- 现在动画完全通过 CharacterAnimationController 入口进行；NormalAttackController 不再直接操作 Animator；
- 如果松开发生在前摇期间，则不激活射线，但仍会进入后摇（更自然的取消反馈）。
*/
public class NormalAttackController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SkillController skillController; // 获取技能快照
    [SerializeField] private GlobalCooldownController globalCooldown; // 公共冷却引用（Player 或 全局）
    [Header("Animation")]
    [SerializeField] private CharacterAnimationController characterAnimationController; // 作为唯一动画入口

    [Header("普通攻击配置")]
    [UnityEngine.Serialization.FormerlySerializedAs("normalAttackSO")]
    [SerializeField] private SkillSO normalAttackSo; // 指向奥术射线的 SkillSO

    [Header("Arcane Ray Prefab (persistent)")]
    [SerializeField] private ArcaneRaySkill arcaneRayPrefab; // 复用的射线 prefab

    [Header("Fire Point on Player")]
    [SerializeField] private Transform firePoint; // 玩家身上的发射点

    [Header("LockOn")]
    [SerializeField] private LockOnController lockOnController; // 可在 Inspector 指定，也会在 Awake 时尝试 GetComponent

    [Header("使用校验")]
    [SerializeField] private float facingAngleThreshold = 12f; // 面对目标的角度阈值
    [Header("开始攻击和结束攻击的事件")]
    [SerializeField] private BoolEventSO attackStartedEvent;
    private ArcaneRaySkill _rayInstance;
    private ArcaneRaySkill _activeRay;

    // 输入与状态
    private bool _isHolding;            // 是否仍按住攻击键
    private bool _hasStartedRay;        // 是否已经启动射线
    private bool _isRaySoundPlaying;    // 是否已经开始播放射线循环音效

    // cached UI pointer state to avoid calling EventSystem from input callbacks
    private bool _isPointerOverUICached;

    // 对外状态
    public bool IsChanneling => _activeRay != null && _activeRay.IsActive;
    private InputSystem_Actions playerInput;
    private PlayerStateMachine _playerFsm;

    // 延迟获取 FSM（Awake 时组件可能尚未添加）
    private PlayerStateMachine GetPlayerFsm()
    {
        if (_playerFsm == null)
            _playerFsm = GetComponent<PlayerStateMachine>();
        return _playerFsm;
    }

    private void Awake()
    {
        //新输入系统的配置
        playerInput = new InputSystem_Actions();
        playerInput.Player.Enable();
        // 注意：_playerFsm 不在 Awake 中获取——PlayerStateMachine 组件此时可能还未添加

        if (lockOnController == null)
        {
            lockOnController = GetComponent<LockOnController>();
        }
        if (characterAnimationController == null)
        {
            characterAnimationController = GetComponent<CharacterAnimationController>();
        }
    }


    private void Update()
    {
        // Update cached UI state each frame. Calling EventSystem.current.IsPointerOverGameObject()
        // from Update is safe and avoids Unity's warning about querying UI state during input
        // event processing (InputSystem callbacks).
        if (EventSystem.current == null)
        {
            _isPointerOverUICached = false;
        }
        else
        {
            // Combine pointer-over and selected object checks as indication of UI interaction.
            _isPointerOverUICached = EventSystem.current.IsPointerOverGameObject() || EventSystem.current.currentSelectedGameObject != null;
        }
    }

    private void OnEnable()
    {
        //新输入系统的配置
        playerInput.Player.Left.started += OnNormalAttack;
        playerInput.Player.Left.canceled += OnNormalAttack;

        if (characterAnimationController != null)
        {
            characterAnimationController.AttackPrecastComplete += HandleAttackPrecastComplete;
        }
    }

    private void OnDisable()
    {
        //新输入系统内核
        playerInput.Player.Left.started -= OnNormalAttack;
        playerInput.Player.Left.canceled -= OnNormalAttack;

        if (characterAnimationController != null)
        {
            {
                characterAnimationController.AttackPrecastComplete -= HandleAttackPrecastComplete;
            }
            if (_activeRay != null)
            {
                _activeRay.Deactivate();
                _activeRay = null;
            }
            _isHolding = false;
            _hasStartedRay = false;
            if (_isRaySoundPlaying && AudioManager.Instance != null)
            {
                AudioManager.Instance.StopWeaponSound(SkillSoundType.奥术射线发射);
                _isRaySoundPlaying = false;
            }
            characterAnimationController?.ForceEndActionImmediate();
        }
    }

    // PlayerInput 绑定入口
    public void OnNormalAttack(InputAction.CallbackContext ctx)
    {

        if (ctx.started)
        {
            // 如果玩家当前在与 UI 交互（例如点击了 UI 按钮、输入框等），不应触发普通攻击
            // 使用缓存的 UI 状态（由 Update 每帧刷新），避免在 InputAction 回调中调用 EventSystem 的查询方法
            if (IsPointerOverUI())
            {
                return;
            }

            OnLeftMouseDown();
        }
        else if (ctx.canceled)
        {
            OnLeftMouseUp();
        }
    }

    // 检测玩家是否正在与 UI 交互（中文注释）
    // 返回 true 时表示当前输入应当被 UI 消耗，游戏内行为（如攻击）不应触发
    private bool IsPointerOverUI()
    {
        // Use the cached value updated in Update() to avoid querying EventSystem during input processing.
        return _isPointerOverUICached;
    }

    private void OnLeftMouseDown()
    {
        if (skillController == null || normalAttackSo == null || arcaneRayPrefab == null || characterAnimationController == null) return;

        // 从 SkillController 获取运行时 PlayerSkill 快照
        var snap = skillController.GetAllSkillsSnapshot();
        if (!snap.TryGetValue(normalAttackSo.SkillID, out var playerSkill))
        {
            // 未找到 PlayerSkill（保险处理：尝试用默认等级实例化）
            playerSkill = new PlayerSkill(normalAttackSo, 1);
        }

        // 使用 SkillManager 获取该技能在当前等级下的冷却，并判断是否属于 GCD 范围
        float cd = 0f;
        if (SkillManager.Instance != null)
        {
            cd = SkillManager.Instance.GetCooldownAtLevel(normalAttackSo, playerSkill.Level);
        }
        bool gcdEligible = (globalCooldown != null) && cd > 0f && cd <= globalCooldown.DefaultDuration;

        // 若当前处于公共冷却且本技能属于 GCD 范围则阻止启动
        if (globalCooldown != null && globalCooldown.IsOnGCD && gcdEligible)
        {
            return;
        }

        // 先从 LockOnController 获取当前锁定目标（如果有）
        Transform targetTransform = null;
        if (lockOnController != null)
        {
            var monster = lockOnController.GetCurrentTarget();
            if (monster != null) targetTransform = monster.transform;
        }

        // 如果有目标则做距离与朝向校验；若不满足则提示并不启动技能
        if (targetTransform != null)
        {
            float range = (playerSkill != null && playerSkill.SkillSO != null) ? playerSkill.SkillSO.castRange : 20f;

            // 距离校验
            float distSqr = (targetTransform.position - transform.position).sqrMagnitude;
            if (distSqr > range * range)
            {
                UIManager.Instance.ShowSkillToast("目标距离过远，无法使用技能");
                return;
            }

            // 朝向校验（只比较水平面）
            Vector3 toTarget = targetTransform.position - transform.position;
            toTarget.y = 0f;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            float angle = Vector3.Angle(forward.normalized, toTarget.normalized);
            if (angle > facingAngleThreshold)
            {
                UIManager.Instance.ShowSkillToast("请面对目标才能使用技能");
                return;
            }

            // 朝向目标（立刻旋转玩家面向目标，保证视觉一致）
            Vector3 lookDir = toTarget;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            }
        }

        // 启动 GCD（如果适用）——与原有行为一致
        if (gcdEligible && globalCooldown != null)
        {
            globalCooldown.StartGCD();
        }

        // 标记输入状态
        _isHolding = true;
        _hasStartedRay = false;

        // 准备持久化实例
        if (_rayInstance == null)
        {
            var go = Instantiate(arcaneRayPrefab.gameObject, firePoint);
            _rayInstance = go.GetComponent<ArcaneRaySkill>();
            _rayInstance.gameObject.SetActive(false);
        }

        // 通知 FSM 进入通道攻击状态（由状态类统一处理动画播放和锁定）
        GetPlayerFsm()?.RequestAction(PlayerFSM.PlayerState.ChannelAttack);

        // MMO: 立即通知远程播放攻击动画（在按下的第一帧触发，不等 DealDamageTo）
        if (GameModeConfig.IsMmoMode)
        {
            var nm = NetworkManager.Instance;
            if (nm != null && nm.IsConnected) nm.SendPlayerAtk();
        }

        // 目标缓存到本次激活流程（在前摇回调中取用）
        _cachedPlayerSkill = playerSkill;
        _cachedTarget = targetTransform;

        attackStartedEvent.RaiseEvent(true, this);
    }

    private PlayerSkill _cachedPlayerSkill;
    private Transform _cachedTarget;

    // 来自动画事件的回调（通过 CharacterAnimationController.AttackPrecastComplete 事件转发）
    private void HandleAttackPrecastComplete()
    {
        if (!_isHolding) // 前摇完成时已松手：不启动射线，直接请求进入后摇
        {
            GetPlayerFsm()?.RequestEndChannel();
            characterAnimationController.EndChannelAttackRequest();
            return;
        }

        if (_rayInstance == null || _cachedPlayerSkill == null)
        {
            return;
        }

        // 激活（进入持续阶段）
        Transform caster = transform;
        _rayInstance.Activate(caster, _cachedPlayerSkill, firePoint, _cachedTarget);
        _activeRay = _rayInstance;
        _hasStartedRay = true;

        // MMO: 通知远程播放技能特效（射线 VFX）
        if (GameModeConfig.IsMmoMode)
        {
            var nm = NetworkManager.Instance;
            if (nm != null && nm.IsConnected && normalAttackSo != null)
            {
                Vector3 targetPos = _cachedTarget != null ? _cachedTarget.position : transform.position + transform.forward * 5f;
                nm.SendSkillCast(normalAttackSo.SkillID, targetPos);
            }
        }

        // 只有真正开始射线后才播放循环音效
        if (!_isRaySoundPlaying && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayLoopingWeaponSound(SkillSoundType.奥术射线发射);
            _isRaySoundPlaying = true;
        }
    }

    private void OnLeftMouseUp()
    {
        _isHolding = false;

        // 如果已经开始持续，立刻停射线
        if (_hasStartedRay && _activeRay != null)
        {
            _activeRay.Deactivate();
            _activeRay = null;
            _hasStartedRay = false;
        }
        // 停止循环音效
        if (_isRaySoundPlaying && AudioManager.Instance != null)
        {
            AudioManager.Instance.StopWeaponSound(SkillSoundType.奥术射线发射);
            _isRaySoundPlaying = false;
        }

        // 请求动画从 Pre/Loop 进入 End（后摇）；通知 FSM 释放通道锁定
        if (characterAnimationController != null)
        {
            GetPlayerFsm()?.RequestEndChannel();
            characterAnimationController.EndChannelAttackRequest();
            characterAnimationController.ForceEndActionImmediate();
        }
        else
        {
            GetPlayerFsm()?.RequestEndChannel();
        }
        attackStartedEvent.RaiseEvent(false, this);
    }
    // 兼容旧接口：被 SkillController 或其他系统调用以打断普攻
    public void InterruptChannel()
    {
        _isHolding = false;
        if (_activeRay != null)
        {
            _activeRay.RequestStopFromExternal();
            _activeRay = null;
            _hasStartedRay = false;
        }
        else if (_rayInstance != null && _rayInstance.IsActive)
        {
            _rayInstance.RequestStopFromExternal();
            _hasStartedRay = false;
        }
        if (_isRaySoundPlaying && AudioManager.Instance != null)
        {
            AudioManager.Instance.StopWeaponSound(SkillSoundType.奥术射线发射);
            _isRaySoundPlaying = false;
        }
        // 请求后摇（如果当前仍在前摇或持续）
        if (characterAnimationController != null)
        {
            GetPlayerFsm()?.RequestEndChannel();
            characterAnimationController.EndChannelAttackRequest();
            // immediately force end action to ensure movement is unlocked when interrupted
            characterAnimationController.ForceEndActionImmediate();
        }
    }
}
