using UnityEngine;
using UnityEngine.InputSystem;

public class MoveMent : MonoBehaviour
{
    [Header("翻滚设置")]
    [Tooltip("翻滚距离（单位）")]
    public float rollDistance = 3f;

    [Tooltip("翻滚持续时间（秒）")]
    public float rollDuration = 0.5f;

    [Tooltip("翻滚速度曲线 - 控制先快后慢的节奏")]
    public AnimationCurve rollSpeedCurve = new AnimationCurve(
        new Keyframe(0f, 1.2f),    // 开始：速度 120%
        new Keyframe(0.3f, 0.7f),  // 中间：蜷缩减速 70%
        new Keyframe(0.7f, 0.9f),  // 后期：准备起身 90%
        new Keyframe(1f, 0f)        // 结束：速度 0%
    );

    // 翻滚相关变量（在现有翻滚变量区域添加）
    private float rollTimer = 0f;
    private Vector3 rollDirection;  // 翻滚方向
    private float rollDistanceTraveled = 0f;  // 已翻滚距离

    // 字段
    [Header("物理移动设置")]
    [Tooltip("基础移动速度 (单位/秒)")]
    public float moveSpeed = 5f;

    [Tooltip("冲刺时的物理移动速度倍率")]
    public float maxSpeedMultiplier = 1.6f;

    [Tooltip("移动加速度，越大响应越快")]
    public float movementAcceleration = 20f;

    [Tooltip("移动减速（地面摩擦力相关），越大停止越快")]
    public float movementDeceleration = 20f;


    [Header("移动设置")]
    private float rollCooldown = 1.5f;   // 翻滚冷却时间
    private float rollCooldownTimer = 0f; // 翻滚冷却计时器
    [Range(0.1f, 1f)] public float turnSpeed = 0.1f; // 转向速度

    [Header("动画入口")]
    [SerializeField] private CharacterAnimationController characterAnimation; // 作为唯一动画入口
    // [Tooltip("是否使用动画的根运动控制移动。关闭时使用物理系统移动。")]
    // public bool useRootMotion = true;
    private Vector2 movementInput;
    private bool isCrouching = false;
    private bool isRolling = false;
    private bool isSprinting = false;
    private bool isJumping = false;

    // 地面检测（替代 CharacterController.isGrounded）
    [Header("地面检测")]
    [SerializeField] private LayerMask groundMask = ~0; // 默认所有层
    [SerializeField] private float groundCheckDistance = 0.2f;



    // 转向相关变量
    // 无需缓存目标方向，直接使用摄像机朝向

    // 翻滚触发锁，防止重复触发
    private bool rollTriggered = false;

    // 控制锁定相关
    private bool isControlLocked = false;

    // Alt键状态
    private bool isAltKeyDown = false;

    // 缓存 Camera.main 引用，避免每帧 Find
    private Camera _cachedMainCamera;

    [Header("动画速度设置")]
    [Tooltip("冲刺时的动画播放速度倍率")]
    public float sprintMultiplier = 1.6f;
    [Header("物理设置")]
    // 用于物理驱动跳跃的刚体（可选，会在 Start 时自动绑定）
    [SerializeField] private Rigidbody rb;
    [SerializeField] private float jumpForce = 5f; // 跳跃冲量强度
    [Tooltip("跳跃后最短滞空时间（秒），用于避免按下瞬间被判定为已经着地）")]
    [SerializeField] private float minJumpAirTime = 0.12f;
    // 运行时的滞空计时器
    private float jumpAirTimer = 0f;

    private InputSystem_Actions playerInput;


    void Awake()
    {
        playerInput = new InputSystem_Actions();
        playerInput.Player.Enable();

    }
    void OnEnable()
    {
        playerInput.Player.Move.performed += OnMove;
        playerInput.Player.Move.canceled += OnMove;

        playerInput.Player.Jump.started += OnJump;

        playerInput.Player.Sprint.performed += OnSprinting;
        playerInput.Player.Sprint.canceled += OnSprinting;

        playerInput.Player.Roll.started += OnRoll;
    }

    void OnDisable()
    {
        playerInput.Player.Move.performed -= OnMove;
        playerInput.Player.Move.canceled -= OnMove;

        playerInput.Player.Jump.started -= OnJump;

        playerInput.Player.Sprint.performed -= OnSprinting;
        playerInput.Player.Sprint.canceled -= OnSprinting;

        playerInput.Player.Roll.started -= OnRoll;
    }

    void OnDestroy()
    {

    }

    void Start()
    {
        // 缓存主摄像机引用，避免每帧 Camera.main 查找
        _cachedMainCamera = Camera.main;

        // 保留原有逻辑：绑定 CharacterAnimationController（如 Inspector 没写）
        if (characterAnimation == null)
            characterAnimation = GetComponent<CharacterAnimationController>();

        // 自动绑定刚体（如果存在且未在 Inspector 手动关联）
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

    }

    void Update()
    {
        // 更新翻滚冷却计时器
        if (rollCooldownTimer > 0)
        {
            rollCooldownTimer -= Time.deltaTime;
        }
        else if (rollTriggered)
        {
            // 当冷却结束时，重置触发锁
            rollTriggered = false;
        }

        if (isRolling)
        {
            HandleRoll();
        }

        // 如果当前处于由刚体驱动的跳跃中，检测落地并重置状态
        if (isJumping)
        {
            // 先递减最短滞空时间，防止与跳跃触发在同一帧被判定为落地
            if (jumpAirTimer > 0f)
            {
                jumpAirTimer -= Time.deltaTime;
            }
            // else if (IsGrounded())
            // {
            //     isJumping = false;
            //     // 落地后恢复动画器的移动参数
            //     if (characterAnimation != null)
            //         UpdateAnimatorSpeedsFromInput(movementInput);
            // }
            // 在 Update() 的落地检测附近添加

            else if (IsGrounded())
            {
                isJumping = false;

                // 落地时平滑恢复移动速度
                if (rb != null && movementInput.sqrMagnitude > 0.0001f)
                {
                    // 让物理系统自然过渡到新速度
                }

                UpdateAnimatorSpeedsFromInput(movementInput);
            }
        }

        // 处理角色转向
        HandleRotation();
    }

    /// <summary>
    /// 【纯代码驱动】物理更新 - 处理走路/跑动的移动
    /// 动画只负责视觉表现，不参与物理计算
    /// </summary>
    void FixedUpdate()
    {
        Move();
    }

    // 供外部（CharacterAnimationController）查询当前是否有移动输入
    public bool HasMovementInput()
    {
        return movementInput.sqrMagnitude > 0.0001f;
    }

    // 供外部查询当前是否处于冲刺条件
    public bool IsSprintingNow()
    {
        // 冲刺仅在向前移动（纵向轴 > 0）时有意义
        return isSprinting && movementInput.y > 0;
    }

    //陈子旧翻滚代码
    // private void HandleRoll()
    // {
    //     // 如果控制被锁定，则不处理翻滚
    //     if (isControlLocked)
    //     {
    //         if (isRolling)
    //         {
    //             isRolling = false;
    //             characterAnimation?.OnRollEnd();
    //         }
    //         return;
    //     }

    //     // 更新翻滚计时器
    //     rollTimer -= Time.deltaTime;

    //     // 检查翻滚是否结束
    //     if (rollTimer <= 0)
    //     {
    //         isRolling = false;
    //         rollTimer = 0f;
    //         // 翻滚结束后启动冷却计时器
    //         rollCooldownTimer = rollCooldown;
    //         rollTriggered = true; // 设置触发锁
    //         characterAnimation?.OnRollEnd();
    //     }
    // }



    private void HandleRoll()
    {
        if (isControlLocked)
        {
            isRolling = false;
            rollDistanceTraveled = 0f;
            characterAnimation?.OnRollEnd();
            return;
        }

        if (!isRolling) return;

        // 递减计时器
        rollTimer -= Time.deltaTime;

        // ========== 实时计算翻滚方向（边翻滚边转向） ==========

        // // 根据当前输入实时更新翻滚方向
        if (movementInput.sqrMagnitude > 0.01f)
        {
            // 有移动输入时，根据输入方向翻滚
            rollDirection = new Vector3(movementInput.x, 0, movementInput.y);
            rollDirection = _cachedMainCamera.transform.TransformDirection(rollDirection);
            rollDirection.y = 0;
            rollDirection.Normalize();
        }
        // // 优化：基于角色朝向（更像传统动作游戏）
        // Vector3 inputDir = new Vector3(movementInput.x, 0, movementInput.y);
        // if (inputDir.sqrMagnitude > 0.01f)
        // {
        //     // 角色自己的左右前后方向
        //     rollDirection = transform.TransformDirection(inputDir);
        //     rollDirection.y = 0;
        //     rollDirection.Normalize();
        // }

        // else 保持初始方向（无输入时沿初始方向翻滚）

        // ========== 核心翻滚逻辑 ==========

        // 计算当前进度 (0 → 1)
        float progress = 1f - (rollTimer / rollDuration);

        // 从曲线获取速度倍率
        float speedMultiplier = rollSpeedCurve.Evaluate(progress);

        // 计算最大速度：distance = avgSpeed × duration
        // 用曲线积分近似：maxSpeed ≈ baseSpeed × 2
        float maxSpeed = (rollDistance / rollDuration) * 1.5f;

        // 当前速度
        float currentSpeed = maxSpeed * speedMultiplier;

        // 本帧位移
        float frameDistance = currentSpeed * Time.deltaTime;
        rollDistanceTraveled += frameDistance;

        // ========== 应用位移和旋转 ==========

        if (rb != null)
        {
            // 设置刚体速度
            rb.linearVelocity = rollDirection * currentSpeed;

            // 翻滚过程中面向翻滚方向
            if (rollDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(rollDirection);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, turnSpeed * 3f));
            }
        }
        else
        {
            transform.position += rollDirection * frameDistance;

            // 翻滚过程中面向翻滚方向
            if (rollDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(rollDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * 3f);
            }
        }

        // ========== 结束判定 ==========

        bool distanceReached = rollDistanceTraveled >= rollDistance;
        bool timeEnded = rollTimer <= 0f;

        if (distanceReached || timeEnded)
        {
            isRolling = false;
            rollTimer = 0f;
            rollDistanceTraveled = 0f;
            rollCooldownTimer = rollCooldown;
            rollTriggered = true;

            // 翻滚结束后平滑减速
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }

            characterAnimation?.OnRollEnd();
        }
    }
    private void HandleRotation()
    {
        // 如果控制被锁定，则不处理转向
        if (isControlLocked) return;

        // 角色始终保持面向摄像机前方，不根据移动方向转向
        // 只有在不在翻滚状态时才进行转向
        if (!isRolling)
        {
            // 计算目标旋转（始终面向摄像机前方）
            if (_cachedMainCamera == null) return;
            Vector3 forward = _cachedMainCamera.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Quaternion targetRotation = Quaternion.LookRotation(forward);

            // 平滑转向
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed);
        }
    }

    public void Move()
    {
        // 翻滚/跳跃/控制锁定时不处理普通移动
        if (isRolling || isJumping || isControlLocked || isAltKeyDown) return;

        // 根据输入计算移动方向（纯代码计算）
        if (movementInput.sqrMagnitude > 0.0001f)
        {
            // 从摇杆输入计算方向，并转换到相机视角
            Vector3 moveDir = new Vector3(movementInput.x, 0, movementInput.y);
            if (_cachedMainCamera != null)
            {
                moveDir = _cachedMainCamera.transform.TransformDirection(moveDir);
            }
            moveDir.y = 0;
            moveDir.Normalize();

            // 计算目标速度（代码完全控制）
            float currentSpeed = moveSpeed;
            if (isSprinting && movementInput.y > 0)
                currentSpeed *= maxSpeedMultiplier;
            if (isCrouching)
                currentSpeed *= 0.5f;

            Vector3 targetVelocity = moveDir * currentSpeed;

            if (rb != null)
            {
                // 有输入 → 加速
                float t = movementAcceleration * Time.fixedDeltaTime;
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, t);
            }
        }
        else
        {
            // 无输入时，减速到停止
            if (rb != null)
            {
                float t = movementDeceleration * Time.fixedDeltaTime;
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, t);
            }
        }
    }
    // 输入回调方法
    public void OnMove(InputAction.CallbackContext value)
    {
        // 始终读取并缓存原始移动输入，以便 HasMovementInput() 与实际输入保持一致
        Vector2 input = value.ReadValue<Vector2>();
        movementInput = input;

        // 如果控制被锁定，或者Alt键被按下，则不处理移动动作或动画参数，但仍需要缓存输入
        if (isControlLocked || isAltKeyDown) return;
        Debug.Log("OnMove " + isControlLocked + " " + isAltKeyDown + " " + isRolling + " " + characterAnimation);

        // 冲刺倍率仅在向前移动（纵向轴大于0）时作用
        if (!isRolling)
        {
            // 通过帮助方法更新动画器的速度参数（会应用蹲伏和冲刺修正、阈值处理）
            UpdateAnimatorSpeedsFromInput(movementInput);
        }


        // 更新动画器的移动/奔跑参数，以便在按键松开时立即反映
        if (characterAnimation != null)
        {
            // 立即根据当前缓存输入刷新动画器速度参数
            UpdateAnimatorSpeedsFromInput(movementInput);
        }

    }




    public void OnCrouch(InputAction.CallbackContext value)
    {
        // 如果控制被锁定，或者Alt键被按下则不处理输入
        if (isControlLocked || isAltKeyDown) return;

        // 翻滚时不能蹲下
        if (isRolling) return;
        isCrouching = value.ReadValueAsButton();
        isSprinting = false;
        // 通过动画控制器设置蹲伏层权重
        characterAnimation?.SetCrouch(isCrouching);
    }

    public void OnJump(InputAction.CallbackContext value)
    {
        // 如果控制被锁定，或者Alt键被按下则不处理输入
        if (isControlLocked || isAltKeyDown) return;

        // 翻滚时不能跳跃
        if (isRolling) return;
        // 仅在跳跃按键按下（开始）时响应
        if (value.started)
        {
            // 只有在接触地面且未处于蹲伏状态时才允许跳跃
            if (IsGrounded() && !isCrouching)
            {
                isJumping = true;
                // 启动最短滞空计时，避免立即判定落地
                jumpAirTimer = minJumpAirTime;

                // 如果存在刚体，则施加向上的冲量以实现物理跳跃
                if (rb != null)
                {
                    // // 保存当前水平速度，用于跳跃时保持移动方向
                    // Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

                    rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

                    // // 如果有水平速度，启动协程保持它
                    // if (horizontalVelocity.sqrMagnitude > 0.01f)
                    // {
                    //     StartCoroutine(PreserveHorizontalVelocityDuringJump(horizontalVelocity));
                    // }
                }

                // 通知动画系统触发跳跃
                characterAnimation?.TriggerJump();
            }
        }
    }

    // /// <summary>
    // /// 跳跃过程中保持水平速度，实现抛物线运动
    // /// </summary>
    // private System.Collections.IEnumerator PreserveHorizontalVelocityDuringJump(Vector3 horizontalVel)
    // {
    //     while (isJumping && !IsGrounded())
    //     {
    //         if (rb != null)
    //         {
    //             // 保持 Y 轴速度（由重力控制），固定 XZ 平面速度
    //             rb.linearVelocity = new Vector3(horizontalVel.x, rb.linearVelocity.y, horizontalVel.z);
    //         }
    //         yield return null;
    //     }
    //     // 落地后停止协程，自然过渡到地面移动
    // }

    public void OnSprinting(InputAction.CallbackContext value)
    {

        // 如果控制被锁定，或者Alt键被按下则不处理输入
        if (isControlLocked || isAltKeyDown) return;

        // 翻滚时或跳跃时不能冲刺
        if (isCrouching || isRolling || isJumping)
        {
            return;
        }
        bool sprintPressed = value.ReadValueAsButton();
        isSprinting = sprintPressed;

        // 立即根据当前缓存输入更新动画器速度（当未锁定控制且未翻滚时）
        if (characterAnimation != null && !isControlLocked && !isRolling)
        {
            // 通过帮助方法更新动画器的速度参数（会应用蹲伏和冲刺修正、阈值处理）
            UpdateAnimatorSpeedsFromInput(movementInput);
        }

    }

    public void OnRoll(InputAction.CallbackContext value)
    {
        // 如果控制被锁定，或者Alt键被按下则不处理输入
        if (isControlLocked || isAltKeyDown) return;

        // 只有在按键按下且未在翻滚状态且不在冷却期间时才触发翻滚
        // 使用 rollTriggered 防止重复触发
        if (value.started && !isRolling && IsGrounded() && rollCooldownTimer <= 0 && !rollTriggered)
        {
            Roll();
        }

        // 当按键释放时重置触发锁
        if (value.canceled)
        {
            rollTriggered = false;
        }
    }

    //陈子旧翻滚代码
    // private void Roll()
    // {
    //     // 如果控制被锁定，或者Alt键被按下则不处理翻滚
    //     if (isControlLocked || isAltKeyDown) return;

    //     // 双重检查确保不会重复触发翻滚
    //     if (isRolling || rollTriggered || rollCooldownTimer > 0)
    //         return;

    //     isRolling = true;
    //     rollTimer = rollDuration;
    //     rollTriggered = true;

    //     // 通知动画控制器：翻滚开始（启用根运动）并触发翻滚动画
    //     characterAnimation?.OnRollStart();
    //     characterAnimation?.TriggerRoll();

    //     // 翻滚位移由动画 root motion 驱动，无需额外方向矢量
    // }
    private void Roll()
    {
        // 条件检查（保持原有）
        if (isControlLocked || isAltKeyDown) return;
        if (isRolling || rollTriggered || rollCooldownTimer > 0) return;

        // ========== 始终基于相机朝向翻滚 ==========
        Vector3 cameraForward = _cachedMainCamera.transform.forward;
        Vector3 cameraRight = _cachedMainCamera.transform.right;
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();

        // 根据输入判断翻滚方向（W/S控制前后，A/D控制左右）
        // 优先级：左右 > 前后 > 默认前方
        if (movementInput.x > 0.1f)
        {
            // D键 → 相机右方
            rollDirection = cameraRight;
        }
        else if (movementInput.x < -0.1f)
        {
            // A键 → 相机左方
            rollDirection = -cameraRight;
        }
        else if (movementInput.y > 0.1f)
        {
            // W键 → 相机前方
            rollDirection = cameraForward;
        }
        else if (movementInput.y < -0.1f)
        {
            // S键 → 相机后方
            rollDirection = -cameraForward;
        }
        else
        {
            // 无输入 → 默认向相机前方翻滚
            rollDirection = cameraForward;
        }

        // 启动翻滚
        isRolling = true;
        rollTimer = rollDuration;
        rollTriggered = true;
        rollDistanceTraveled = 0f;

        characterAnimation?.OnRollStart();
        characterAnimation?.TriggerRoll();
    }


    // 供 CharacterAnimationController 调用：强制站起（取消蹲伏的业务状态）
    public void ForceStandUp()
    {
        if (isCrouching)
        {
            isCrouching = false;
            characterAnimation?.SetCrouch(false);
        }
    }

    // 供 CharacterAnimationController 调用：取消翻滚状态（如有）
    public void CancelRoll()
    {
        if (isRolling)
        {
            isRolling = false;
            rollTimer = 0f;
            rollCooldownTimer = rollCooldown; // 进入冷却，避免立刻再次翻滚
            rollTriggered = false;
            characterAnimation?.OnRollEnd();
        }
    }

    /// <summary>
    /// 锁定玩家控制
    /// </summary>
    public void LockPlayerControl()
    {
        isControlLocked = true;
    }

    /// <summary>
    /// 解锁玩家控制
    /// </summary>
    public void UnlockPlayerControl()
    {
        isControlLocked = false;

        // 基于最新缓存输入立即恢复动画器移动参数
        if (characterAnimation != null)
        {
            UpdateAnimatorSpeedsFromInput(movementInput);
        }
    }

    /// <summary>
    /// 检查玩家控制是否被锁定
    /// </summary>
    /// <returns>控制是否被锁定</returns>
    public bool IsControlLocked()
    {
        return isControlLocked;
    }

    /// <summary>
    /// 处理Alt键的输入
    /// </summary>
    /// <param name="value">Alt键的输入值</param>
    public void OnAltKey(InputAction.CallbackContext value)
    {
        if (value.started)
        {
            isAltKeyDown = true;
        }
        else if (value.canceled)
        {
            isAltKeyDown = false;
        }
    }

    /// <summary>
    /// 检查角色是否接触地面（替代 CharacterController.isGrounded）
    /// </summary>
    private bool IsGrounded()
    {
        // 从角色位置向下做一个短射线检测地面
        Vector3 origin = transform.position + Vector3.up * 0.1f; // 稍微抬高起点，避免嵌入地面
        return Physics.Raycast(origin, Vector3.down, groundCheckDistance + 0.05f, groundMask);
    }

    // 帮助方法：统一动画参数更新逻辑，保证调用顺序不影响结果
    private void UpdateAnimatorSpeedsFromInput(Vector2 input)
    {


        if (characterAnimation == null) return;
        // 计算基础输入
        Vector2 dir = input;
        // 应用蹲伏修正
        if (isCrouching)
        {
            dir *= 0.5f;
        }
        // 仅在处于冲刺并且向前移动时应用冲刺倍率
        if (isSprinting && dir.y > 0f)
        {
            dir *= sprintMultiplier;
        }

        // Debug.Log("X移动速度" + dir.x);
        // Debug.Log("Y移动速度" + dir.y);
        // 推送速度到动画器
        characterAnimation.SetMoveSpeeds(dir.x, dir.y);
    }


    // // 应用根运动：优先使用 CharacterController.Move 保持碰撞
    // // 若没有 CharacterController 但有刚体，则通过刚体的 MovePosition/MoveRotation 与物理系统协同
    // public void ApplyRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
    // {

    //     // 若有刚体，则使用 MovePosition/MoveRotation 与物理系统协同
    //     if (rb != null)
    //     {
    //         rb.MovePosition(rb.position + deltaPosition);
    //         rb.MoveRotation(deltaRotation);
    //         return;
    //     }

    //     // 回退：若无 CharacterController 和刚体，则直接修改 transform
    //     transform.position += deltaPosition;
    //     transform.rotation = deltaRotation;
    // }

    // public void ApplyRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
    // {
    //     // 1. 保留转向
    //     if (deltaRotation != Quaternion.identity)
    //     {
    //         // 可以选择是否使用根运动的旋转
    //         // transform.rotation = deltaRotation; // 选项A：直接用根运动旋转
    //         // 或保持你自己的转向逻辑（HandleRotation）已处理
    //     }

    //     // 2. 计算移动方向（从根运动提取）
    //     Vector3 moveDirection = deltaPosition;
    //     moveDirection.y = 0f; // 忽略垂直分量
    //     if (moveDirection.sqrMagnitude > 0.0001f)
    //     {
    //         moveDirection.Normalize();

    //         // 3. 计算目标速度
    //         float currentSpeed = moveSpeed;

    //         // 如果在冲刺，应用倍率
    //         if (IsSprintingNow() && movementInput.y > 0)
    //         {
    //             currentSpeed *= maxSpeedMultiplier;
    //         }

    //         // 如果在蹲伏，降低速度
    //         if (isCrouching)
    //         {
    //             currentSpeed *= 0.5f;
    //         }

    //         Vector3 targetVelocity = moveDirection * currentSpeed;

    //         // 4. 应用到刚体
    //         if (rb != null)
    //         {
    //             // 方法A：平滑加速到目标速度
    //             rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, movementAcceleration * Time.fixedDeltaTime);

    //             // 保持旋转控制
    //             rb.MoveRotation(deltaRotation);
    //             return;
    //         }

    //         // 5. 回退到 CharacterController 或 Transform
    //         // 如果有 CharacterController：
    //         // controller.Move(targetVelocity * Time.deltaTime);

    //         // 如果都没有，直接修改 transform：
    //         // transform.position += targetVelocity * Time.deltaTime;
    //     }
    // }


}
