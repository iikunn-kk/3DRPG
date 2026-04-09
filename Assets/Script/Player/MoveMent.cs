using UnityEngine;
using UnityEngine.InputSystem;

public class MoveMent : MonoBehaviour
{
    // 字段
    [Header("移动设置")]
    private float rollDuration = 2f;     // 翻滚持续时间
    private float rollCooldown = 1.5f;   // 翻滚冷却时间
    [Range(0.1f, 1f)] public float turnSpeed = 0.1f; // 转向速度

    [Header("动画入口")]
    [SerializeField] private CharacterAnimationController characterAnimation; // 作为唯一动画入口
    private Vector2 movementInput;
    private bool isCrouching = false;
    private bool isRolling = false;
    private bool isSprinting = false;
    private bool isJumping = false;

    // 地面检测（替代 CharacterController.isGrounded）
    [Header("地面检测")]
    [SerializeField] private LayerMask groundMask = ~0; // 默认所有层
    [SerializeField] private float groundCheckDistance = 0.2f;

    // 翻滚相关变量
    private float rollTimer = 0f;
    private float rollCooldownTimer = 0f; // 翻滚冷却计时器

    // 转向相关变量
    // 无需缓存目标方向，直接使用摄像机朝向

    // 翻滚触发锁，防止重复触发
    private bool rollTriggered = false;

    // 控制锁定相关
    public static bool isControlLocked = false;

    // Alt键状态
    private bool isAltKeyDown = false;

    // 添加MoveMent实例的静态引用
    public static MoveMent Instance { get; private set; }

    // 新增：序列化字段：冲刺倍率
    [Header("移动设置（附加）")]
    [SerializeField] public float sprintMultiplier = 1.6f; // 冲刺时前向速度的倍率
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
        // 设置实例引用
        Instance = this;

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
            else if (IsGrounded())
            {
                isJumping = false;
                // 落地后恢复动画器的移动参数
                if (characterAnimation != null)
                    UpdateAnimatorSpeedsFromInput(movementInput);
            }
        }

        // 处理角色转向
        HandleRotation();
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

    private void HandleRoll()
    {
        // 如果控制被锁定，则不处理翻滚
        if (isControlLocked)
        {
            if (isRolling)
            {
                isRolling = false;
                characterAnimation?.OnRollEnd();
            }
            return;
        }

        // 更新翻滚计时器
        rollTimer -= Time.deltaTime;

        // 检查翻滚是否结束
        if (rollTimer <= 0)
        {
            isRolling = false;
            rollTimer = 0f;
            // 翻滚结束后启动冷却计时器
            rollCooldownTimer = rollCooldown;
            rollTriggered = true; // 设置触发锁
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
            if (Camera.main == null) return;
            Vector3 forward = Camera.main.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Quaternion targetRotation = Quaternion.LookRotation(forward);

            // 平滑转向
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
                    rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
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

    private void Roll()
    {
        // 如果控制被锁定，或者Alt键被按下则不处理翻滚
        if (isControlLocked || isAltKeyDown) return;

        // 双重检查确保不会重复触发翻滚
        if (isRolling || rollTriggered || rollCooldownTimer > 0)
            return;

        isRolling = true;
        rollTimer = rollDuration;
        rollTriggered = true;

        // 通知动画控制器：翻滚开始（启用根运动）并触发翻滚动画
        characterAnimation?.OnRollStart();
        characterAnimation?.TriggerRoll();

        // 翻滚位移由动画 root motion 驱动，无需额外方向矢量
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


    // 应用根运动：优先使用 CharacterController.Move 保持碰撞
    // 若没有 CharacterController 但有刚体，则通过刚体的 MovePosition/MoveRotation 与物理系统协同
    public void ApplyRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
    {

        // 若有刚体，则使用 MovePosition/MoveRotation 与物理系统协同
        if (rb != null)
        {
            rb.MovePosition(rb.position + deltaPosition);
            rb.MoveRotation(deltaRotation);
            return;
        }

        // 回退：若无 CharacterController 和刚体，则直接修改 transform
        transform.position += deltaPosition;
        transform.rotation = deltaRotation;
    }
}
