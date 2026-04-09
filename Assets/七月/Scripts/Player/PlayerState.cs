using UnityEngine;

/// <summary>
/// 玩家状态基类，继承ScriptableObject并实现IState接口
/// 所有具体玩家状态都应继承此类
/// </summary>
public class PlayerState : ScriptableObject, IState
{
    [SerializeField] string stateName; // 状态名称，对应动画状态机中的状态名
    [SerializeField, Range(0f, 1f)] float transitionDuration = 0.1f; // 动画过渡时间(0-1秒)

    float stateStartTime; // 状态开始时间
    int stateHash; // 动画状态的哈希值，用于优化性能

    // 以下字段可在子类中访问
    protected float currentSpeed; // 当前移动速度
    protected Animator animator; // 动画控制器引用
    protected PlayerController player; // 玩家控制器引用
    protected PlayerInput input; // 玩家输入系统引用
    protected PlayerStateMachine stateMachine; // 状态机引用

    /// <summary>
    /// 判断当前动画是否播放完成
    /// </summary>
    protected bool IsAnimationFinished => StateDuration >= animator.GetCurrentAnimatorStateInfo(0).length;

    /// <summary>
    /// 获取当前状态已持续时间
    /// </summary>
    protected float StateDuration => Time.time - stateStartTime;



    /// <summary>
    /// ScriptableObject启用时调用，生成动画状态哈希值
    /// </summary>
    void OnEnable()
    {
        stateHash = Animator.StringToHash(stateName);
    }

    /// <summary>
    /// 初始化状态所需组件
    /// </summary>
    public void Initialize(Animator animator, PlayerController player, PlayerInput input, PlayerStateMachine stateMachine)
    {
        this.animator = animator;
        this.player = player;
        this.input = input;
        this.stateMachine = stateMachine;
    }

    /// <summary>
    /// 进入状态时调用
    /// </summary>
    public virtual void Enter()
    {
        animator.CrossFade(stateHash, transitionDuration); // 播放对应动画
        stateStartTime = Time.time; // 记录状态开始时间
    }

    /// <summary>
    /// 退出状态时调用
    /// </summary>
    public virtual void Exit()
    {
        // 可由子类重写实现退出逻辑
    }

    /// <summary>
    /// 每帧逻辑更新
    /// </summary>
    public virtual void LogicUpdate()
    {
        // 可由子类重写实现每帧逻辑
    }

    /// <summary>
    /// 物理更新
    /// </summary>
    public virtual void PhysicUpdate()
    {
        // 可由子类重写实现物理更新
    }
}
