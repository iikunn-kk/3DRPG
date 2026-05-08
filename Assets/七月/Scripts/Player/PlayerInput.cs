// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.InputSystem;

// // 玩家输入控制类，处理所有玩家输入事件
// public class PlayerInput : MonoBehaviour
// {
//     //输入缓冲时间
//     [SerializeField] float jumpInputBufferTime = 0.5f;
//     WaitForSeconds waitJumpInputBufferTime;
//     // Unity新输入系统的输入动作实例
//     PlayerInputActions playerInputActions;

//     // 获取二维输入轴向值（如WASD/摇杆输入）
//     Vector2 axes => playerInputActions.Gameplay.Axes.ReadValue<Vector2>();

//     // 跳跃输入缓冲标记（用于实现缓冲跳跃）
//     public bool HasJUmpInputBuffer { get; set; }

//     // 当前帧是否按下跳跃键
//     public bool Jump => playerInputActions.Gameplay.Jump.WasPressedThisFrame();

//     // 当前帧是否松开跳跃键  
//     public bool StopJump => playerInputActions.Gameplay.Jump.WasReleasedThisFrame();

//     // 是否有移动输入（X轴不为0）
//     public bool Move => AxisX != 0f;

//     // 获取X轴输入值（-1到1）
//     public float AxisX => axes.x;

//     // 初始化输入系统
//     void Awake()
//     {
//         playerInputActions = new PlayerInputActions();
//         waitJumpInputBufferTime = new WaitForSeconds(jumpInputBufferTime);
//     }

//     // 组件启用时注册输入事件
//     void OnEnable()
//     {
//         // 监听跳跃键释放事件
//         playerInputActions.Gameplay.Jump.canceled += delegate
//         {
//             HasJUmpInputBuffer = false; // 清除跳跃缓冲标记
//         };
//     }

//     public void EnableGameplayInputs()
//     {
//         playerInputActions.Gameplay.Enable(); // 激活Gameplay输入映射
//         Cursor.lockState = CursorLockMode.Locked; // 锁定鼠标光标
//     }

//     public void DisableGameplayInputs()
//     {
//         playerInputActions.Gameplay.Disable();
//     }
//     /// <summary>
//     /// 设置跳跃输入缓冲计时器
//     /// 当玩家按下跳跃键时调用此方法
//     /// </summary>
//     public void SetJumpInputBufferTimer()
//     {
//         // 先停止任何正在运行的跳跃缓冲协程
//         // 防止多次跳跃输入导致协程重复运行
//         StopCoroutine(nameof(JumpInputBufferCouroutine));

//         // 启动新的跳跃缓冲协程
//         // 开始计时跳跃输入的缓冲时间
//         StartCoroutine(nameof(JumpInputBufferCouroutine));
//     }
//     /// <summary>
//     /// 跳跃输入缓冲协程
//     /// 实现跳跃指令的缓冲机制，允许玩家在落地前提前按下跳跃键
//     /// </summary>
//     IEnumerator JumpInputBufferCouroutine()
//     {
//         // 设置跳跃缓冲标记为true，表示有跳跃输入待处理
//         HasJUmpInputBuffer = true;

//         // 等待预设的缓冲时间（jumpInputBufferTime秒）
//         yield return waitJumpInputBufferTime;

//         // 缓冲时间结束后，清除跳跃缓冲标记
//         HasJUmpInputBuffer = false;
//     }


// }