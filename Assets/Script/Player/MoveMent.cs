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
    [HideInInspector] public Vector2 movementInput;
    [HideInInspector] public bool isCrouching = false;
    private bool isRolling = false;

    /// <summary>供 FSM 查询翻滚状态</summary>
    public bool IsRolling => isRolling;
    private bool isSprinting = false;
    private bool isJumping = false;

    /// <summary>供 FSM 查询跳跃状态</summary>
    public bool IsJumping => isJumping;

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

    /// <summary>供外部查询 Alt 键状态（FSM 通道检测使用）</summary>
    public bool IsAltKeyPressed() => isAltKeyDown;

    // 缓存 Camera.main 引用，避免每帧 Find
    private Camera _cachedMainCamera;

    // ---- FSM 物理访问属性 ----

    /// <summary>刚体引用（供 FSM 状态读取）</summary>
    public Rigidbody PlayerRigidbody => rb;

    /// <summary>主摄像机缓存（供 FSM 状态读取）</summary>
    public Camera CachedCamera => _cachedMainCamera;

    /// <summary>翻滚计时器（供 FSM RollState 读写）</summary>
    public float RollTimer { get => rollTimer; set => rollTimer = value; }

    /// <summary>翻滚方向（供 FSM RollState 读写）</summary>
    public Vector3 RollDirection { get => rollDirection; set => rollDirection = value; }

    /// <summary>已翻滚距离（供 FSM RollState 读写）</summary>
    public float RollDistanceTraveled { get => rollDistanceTraveled; set => rollDistanceTraveled = value; }

    /// <summary>翻滚持续时间（供 FSM RollState 读取）</summary>
    public float RollDuration => rollDuration;

    /// <summary>翻滚速度曲线（供 FSM RollState 读取）</summary>
    public AnimationCurve RollSpeedCurve => rollSpeedCurve;

    /// <summary>翻滚冷却时间（供 FSM RollState 读取）</summary>
    public float RollCooldown => rollCooldown;

    /// <summary>翻滚冷却结束标记（供 FSM RollState 读写）</summary>
    public bool RollTriggered { get => rollTriggered; set => rollTriggered = value; }

    /// <summary>运动参数（供 FSM 状态读取）</summary>
    public float MoveSpeed => moveSpeed;
    public float MaxSpeedMultiplier => maxSpeedMultiplier;
    public float MovementAcceleration => movementAcceleration;
    public float MovementDeceleration => movementDeceleration;
    public float JumpForce => jumpForce;
    public float TurnSpeed => turnSpeed;

    [Header("动画速度设置")]
    [Tooltip("冲刺时的动画播放速度倍率")]
    public float sprintMultiplier = 1.6f;
    [Header("物理设置")]
    // 用于物理驱动跳跃的刚体（可选，会在 Start 时自动绑定）
    [SerializeField] private Rigidbody rb;
    [SerializeField] private float jumpForce = 5f; // 跳跃冲量强度
    [Tooltip("跳跃后最短滞空时间（秒），用于避免按下瞬间被判定为已经着地）")]
    [SerializeField] private float minJumpAirTime = 0.12f;

    /// <summary>供 FSM 查询最小滞空时间</summary>
    public float MinJumpAirTime => minJumpAirTime;
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

        playerInput.Player.Crouch.started += OnCrouch;
        playerInput.Player.Crouch.performed += OnCrouch;
        playerInput.Player.Crouch.canceled += OnCrouch;
    }

    void OnDisable()
    {
        playerInput.Player.Move.performed -= OnMove;
        playerInput.Player.Move.canceled -= OnMove;

        playerInput.Player.Jump.started -= OnJump;

        playerInput.Player.Sprint.performed -= OnSprinting;
        playerInput.Player.Sprint.canceled -= OnSprinting;

        playerInput.Player.Roll.started -= OnRoll;

        playerInput.Player.Crouch.started -= OnCrouch;
        playerInput.Player.Crouch.performed -= OnCrouch;
        playerInput.Player.Crouch.canceled -= OnCrouch;
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

        // 如果当前处于由刚体驱动的跳跃中，检测落地并重置状态
        if (isJumping)
        {
            if (jumpAirTimer > 0f)
            {
                jumpAirTimer -= Time.deltaTime;
            }
            else if (IsGrounded())
            {
                isJumping = false;
                UpdateAnimatorSpeedsFromInput(movementInput);
            }
        }

        // 物理移动和转向已迁移至各 FSM 状态的 FixedUpdate()
    }

    /// <summary>
    /// 物理更新 — 由 FSM 各状态的 FixedUpdate() 接管。
    /// 移动/翻滚/跳跃物理已迁移至 FSM 状态类。
    /// 此方法保留为空，仅作为组件生命周期占位。
    /// </summary>
    void FixedUpdate()
    {
        // 物理移动已迁移至各 FSM 状态类的 FixedUpdate()
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





    // HandleRoll 已迁移至 PlayerRollState.FixedUpdate()
    // HandleRotation 已迁移至各移动状态类的 FixedUpdate()

    /// <summary>
    /// 应用带速度的物理移动（供 FSM 状态调用）。
    /// targetVelocity: 目标速度向量
    /// acceleration: 加速系数
    /// </summary>
    public void ApplyMovement(Vector3 targetVelocity, float acceleration)
    {
        if (rb == null || isControlLocked || isAltKeyDown) return;
        float t = acceleration * Time.fixedDeltaTime;
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, t);
    }

    /// <summary>
    /// 减速到停止（供 FSM 状态调用）。
    /// </summary>
    public void Decelerate(float deceleration)
    {
        if (rb == null || isControlLocked || isAltKeyDown) return;
        float t = deceleration * Time.fixedDeltaTime;
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, t);
    }

    /// <summary>
    /// 平滑转向面向摄像机前方（供 FSM 状态调用）。
    /// </summary>
    public void RotateTowardCameraForward(float turnSpeed)
    {
        if (_cachedMainCamera == null || isControlLocked) return;
        Vector3 forward = _cachedMainCamera.transform.forward;
        forward.y = 0f;
        forward.Normalize();
        if (forward.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(forward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed);
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
        // Debug.Log("OnMove " + isControlLocked + " " + isAltKeyDown + " " + isRolling + " " + characterAnimation);

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
        if (isControlLocked || isAltKeyDown) return;
        if (isRolling) return;
        isCrouching = value.ReadValueAsButton();
        isSprinting = false;
        characterAnimation?.SetCrouch(isCrouching);
        UpdateAnimatorSpeedsFromInput(movementInput);
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

        // 蹲伏/跳跃/翻滚中/冷却期间不触发
        if (value.started && !isRolling && !isCrouching && IsGrounded() && rollCooldownTimer <= 0 && !rollTriggered)
        {
            Roll();
        }

        // 当按键释放时重置触发锁
        if (value.canceled)
        {
            rollTriggered = false;
        }
    }


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
    /// <summary>供 FSM 查询着地状态</summary>
    public bool IsGrounded()
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




}
