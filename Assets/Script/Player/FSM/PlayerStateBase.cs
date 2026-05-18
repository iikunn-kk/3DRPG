using UnityEngine;

namespace PlayerFSM
{
    /// <summary>
    /// 玩家状态抽象基类。
    /// 与 MonsterStateBase 同构，提供统一的生命周期接口。
    /// </summary>
    public abstract class PlayerStateBase
    {
        /// <summary>返回此实例对应的状态枚举值</summary>
        public abstract PlayerState StateType { get; }

        // 组件引用（在 Init 中缓存）
        protected PlayerStateMachine owner;
        protected MoveMent movement;
        protected CharacterAnimationController anim;
        protected CharacterState characterState;

        /// <summary>
        /// 初始化，缓存组件引用。
        /// </summary>
        public virtual void Init(PlayerStateMachine owner)
        {
            this.owner = owner;
            movement = owner.GetComponent<MoveMent>();
            anim = owner.GetComponent<CharacterAnimationController>();
            characterState = owner.GetComponent<CharacterState>();
        }

        /// <summary>进入状态</summary>
        public virtual void Enter() { }

        /// <summary>每帧逻辑更新</summary>
        public virtual void Update() { }

        /// <summary>物理更新</summary>
        public virtual void FixedUpdate() { }

        /// <summary>动画/相机后处理</summary>
        public virtual void LateUpdate() { }

        /// <summary>退出状态</summary>
        public virtual void Exit() { }

        /// <summary>检查状态切换条件</summary>
        public virtual void CheckTransitions() { }

        /// <summary>销毁时清理</summary>
        public virtual void UnInit() { }

        // ---- 帮助方法 ----

        /// <summary>是否在地面（通过 MoveMent 检测）</summary>
        protected bool IsGrounded()
        {
            if (movement != null)
                return movement.IsGrounded();
            return true;
        }

        /// <summary>获取当前移动输入向量</summary>
        protected Vector2 GetInputVector()
        {
            if (movement != null)
                return movement.movementInput;
            return Vector2.zero;
        }

        /// <summary>是否按住 Sprint 键</summary>
        protected bool IsSprintHeld()
        {
            if (movement != null)
                return movement.IsSprintingNow();
            return false;
        }
    }
}
