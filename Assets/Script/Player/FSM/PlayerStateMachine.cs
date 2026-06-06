using System.Collections.Generic;
using UnityEngine;

namespace PlayerFSM
{

    /// <summary>
    /// 玩家状态机主控制器。
    /// 阶段三：接管跳跃/翻滚/死亡状态管理。
    /// 挂载在玩家 GameObject 上，自动发现依赖组件。
    /// </summary>
    public class PlayerStateMachine : MonoBehaviour
    {
        [Header("运行时状态")]
        [SerializeField] private PlayerState _currentState = PlayerState.Idle;
        [SerializeField] private PlayerState _previousState = PlayerState.Idle;

        /// <summary>当前状态枚举（Inspector 可读）</summary>
        public PlayerState CurrentState => _currentState;

        /// <summary>前一状态枚举</summary>
        public PlayerState PreviousState => _previousState;

        private Dictionary<PlayerState, PlayerStateBase> _stateDict;
        private PlayerStateBase _currentStateInstance;
        private bool _hasEnteredInitialState;

        // 观察模式（阶段一调试用，阶段二关闭）
        [Header("调试")]
        [SerializeField] private bool _observeOnly = false;

        private void Awake()
        {
            InitializeStates();
            ForceEnterInitialState(PlayerState.Idle);
            // Debug.Log($"[PlayerFSM] 阶段三激活，当前状态: {_currentState}");
        }

        private void Update()
        {
            if (_currentStateInstance == null) return;
            UpdateStateMachine();
        }

        private void FixedUpdate()
        {
            if (_currentStateInstance == null) return;
            FixedUpdateStateMachine();
        }

        private void LateUpdate()
        {
            if (_currentStateInstance == null) return;
            _currentStateInstance.LateUpdate();
        }

        private void OnDestroy()
        {
            foreach (var kvp in _stateDict)
            {
                kvp.Value?.UnInit();
            }
        }

        // ---- 初始化 ----

        private void InitializeStates()
        {
            _stateDict = new Dictionary<PlayerState, PlayerStateBase>(12);

            // 阶段一：Locomotion 状态
            RegisterState(new PlayerIdleState());
            RegisterState(new PlayerWalkState());
            RegisterState(new PlayerSprintState());
            RegisterState(new PlayerCrouchState());

            // 阶段二：Action 状态
            RegisterState(new PlayerAttackState());
            RegisterState(new PlayerSkillState());
            RegisterState(new PlayerBuffState());
            RegisterState(new PlayerChannelAttackState());

            // 阶段三：Jump, Roll, Death
            RegisterState(new PlayerJumpState());
            RegisterState(new PlayerRollState());
            RegisterState(new PlayerDeathState());
        }

        private void RegisterState(PlayerStateBase state)
        {
            state.Init(this);
            _stateDict[state.StateType] = state;
        }

        private void ForceEnterInitialState(PlayerState state)
        {
            if (_stateDict.TryGetValue(state, out var instance))
            {
                _currentStateInstance = instance;
                _currentState = state;
                _previousState = state;
                _currentStateInstance.Enter();
                _hasEnteredInitialState = true;
            }
        }

        // ---- 外部调用入口 ----

        /// <summary>
        /// 供 SkillController 等外部系统调用：请求进入动作状态。
        /// 仅在当前为基础移动状态（Idle/Walk/Sprint/Crouch）时才接受转换。
        /// </summary>
        public void RequestAction(PlayerState actionState)
        {
            if (_observeOnly) return;

            // 验证是否为有效的动作状态
            if (actionState != PlayerState.Attack && actionState != PlayerState.Skill &&
                actionState != PlayerState.Buff && actionState != PlayerState.ChannelAttack)
            {
                // Debug.LogWarning($"[PlayerFSM] RequestAction 收到非法状态: {actionState}");
                return;
            }

            // 仅允许从移动类状态进入动作
            if (IsInLocomotionState())
            {
                ChangeStateInternal(actionState);
            }
        }

        /// <summary>
        /// 当前是否处于基础移动状态（允许接收动作输入）。
        /// </summary>
        public bool IsInLocomotionState()
        {
            return _currentState == PlayerState.Idle ||
                   _currentState == PlayerState.Walk ||
                   _currentState == PlayerState.Sprint ||
                   _currentState == PlayerState.Crouch;
        }

        /// <summary>
        /// 供 NormalAttackController 调用：结束通道攻击（鼠标松开时触发）。
        /// </summary>
        public void RequestEndChannel()
        {
            if (_currentState == PlayerState.ChannelAttack &&
                _stateDict.TryGetValue(PlayerState.ChannelAttack, out var state))
            {
                var channelState = state as PlayerChannelAttackState;
                channelState?.EndChannel();
            }
        }

        // ---- 状态切换 ----

        public void ChangeState(PlayerState newState)
        {
            if (_observeOnly)
            {
                // Debug.Log($"[PlayerFSM] [OBSERVE] 请求切换: {_currentState} -> {newState}");
                return;
            }
            ChangeStateInternal(newState);
        }

        private void ChangeStateInternal(PlayerState newState)
        {
            if (_hasEnteredInitialState && _currentState == newState) return;

            // Debug.Log($"[PlayerFSM] 切换: {_currentState} -> {newState}");

            _currentStateInstance?.Exit();
            _previousState = _currentState;
            _currentState = newState;

            if (_stateDict.TryGetValue(newState, out var instance))
            {
                _currentStateInstance = instance;
                _currentStateInstance.Enter();
            }
        }

        // ---- 每帧更新 ----

        private void UpdateStateMachine()
        {
            // 全局死亡检测（最高优先级）
            var charState = GetComponent<CharacterState>();
            if (charState != null)
            {
                // 进入死亡
                if (charState.IsDead && _currentState != PlayerState.Death)
                {
                    ChangeStateInternal(PlayerState.Death);
                    return;
                }
                // 退出死亡 → 复活
                if (!charState.IsDead && _currentState == PlayerState.Death)
                {
                    ChangeStateInternal(PlayerState.Idle);
                    return;
                }
            }

            _currentStateInstance.Update();

            if (_currentState != PlayerState.Death)
            {
                _currentStateInstance.CheckTransitions();
            }
        }

        private void FixedUpdateStateMachine()
        {
            _currentStateInstance.FixedUpdate();
        }
    }
}
