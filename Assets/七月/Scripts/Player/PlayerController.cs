using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 玩家角色控制器，负责处理玩家角色的核心逻辑
/// </summary>
public class PlayerController : MonoBehaviour
{
    // [SerializeField] VoidEventChannel levelClearedEventChannel; // 关卡通关事件通道
    PlayerGroundDetector groundDetector; // 地面检测组件
    PlayerInput input; // 玩家输入组件
    Rigidbody rigidBody; // 刚体组件

    // 属性定义
    public AudioSource VoicePlayer { get; private set; } // 语音播放器
    public bool CanAirJump { get; set; } // 是否可以进行空中跳跃
    public bool Victory { get; private set; } // 是否胜利状态
    public bool IsGrounded => groundDetector.IsGrounded; // 是否在地面上
    public bool IsFalling => rigidBody.linearVelocity.y < 0 && !IsGrounded; // 是否处于下落状态
    public float MoveSpeed => Mathf.Abs(rigidBody.linearVelocity.x); // 当前移动速度(X轴绝对值)

    /// <summary>
    /// 初始化组件引用
    /// </summary>
    void Awake()
    {
        groundDetector = GetComponentInChildren<PlayerGroundDetector>();
        input = GetComponent<PlayerInput>();
        rigidBody = GetComponent<Rigidbody>();
        VoicePlayer = GetComponentInChildren<AudioSource>();
    }

    /// <summary>
    /// 注册关卡通关事件监听
    /// </summary>
    void OnEnable()
    {
        // levelClearedEventChannel.AddListener(OnLevelCleared);
    }

    /// <summary>
    /// 取消关卡通关事件监听
    /// </summary>
    void OnDisable()
    {
        // levelClearedEventChannel.RemoveListener(OnLevelCleared);
    }

    /// <summary>
    /// 关卡通关事件处理
    /// </summary>
    void OnLevelCleared()
    {
        Victory = true; // 设置胜利状态
    }

    /// <summary>
    /// 玩家被击败时的处理
    /// </summary>
    public void OnDefeated()
    {
        input.DisableGameplayInputs(); // 禁用输入
        rigidBody.linearVelocity = Vector3.zero; // 重置速度
        rigidBody.useGravity = false; // 禁用重力
        rigidBody.detectCollisions = false; // 禁用碰撞
        GetComponent<StateMachine>().SwitchState(typeof(PlayerState_Defeated)); // 切换到被击败状态
    }

    /// <summary>
    /// 游戏开始时启用输入
    /// </summary>
    void Start()
    {
        input.EnableGameplayInputs();
    }

    /// <summary>
    /// 移动玩家角色
    /// </summary>
    /// <param name="speed">移动速度</param>
    public void Move(float speed)
    {
        if (input.Move)
        {
            transform.localScale = new Vector3(input.AxisX, 1f, 1f); // 根据输入方向翻转角色
        }
        SetVelocityX(speed * input.AxisX); // 设置X轴速度
    }

    /// <summary>
    /// 设置刚体速度
    /// </summary>
    /// <param name="veloctiy">目标速度</param>
    public void SetVelocity(Vector3 veloctiy)
    {
        rigidBody.linearVelocity = veloctiy;
    }

    /// <summary>
    /// 设置X轴速度
    /// </summary>
    /// <param name="velocityX">X轴速度值</param>
    public void SetVelocityX(float velocityX)
    {
        rigidBody.linearVelocity = new Vector3(velocityX, rigidBody.linearVelocity.y);
    }

    /// <summary>
    /// 设置Y轴速度
    /// </summary>
    /// <param name="velocityY">Y轴速度值</param>
    public void SetVelocityY(float velocityY)
    {
        rigidBody.linearVelocity = new Vector3(rigidBody.linearVelocity.x, velocityY);
    }

    /// <summary>
    /// 设置是否使用重力
    /// </summary>
    /// <param name="value">是否启用重力</param>
    public void SetUseGravity(bool value)
    {
        rigidBody.useGravity = value;
    }
}

