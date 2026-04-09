using UnityEngine;
using UnityEngine.AI;

// 添加MonsterDetection等类所在的命名空间（如果有的话）
// 这里假设这些类在同一个命名空间或默认命名空间中
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(MonsterDetection), typeof(MonsterCombat), typeof(MonsterAnimationController))]
[RequireComponent(typeof(MonsterStateMachine))]
[RequireComponent(typeof(MonsterLocomotionDriver))]
public class MonsterBase : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] public float patrolSpeed = 2f;
    [SerializeField] public float chaseSpeed = 4f;
    
    [Header("被锁定了以后的白点所在位置")]
    [SerializeField] protected Transform _lockedOnPosition;
    public Transform lockedOnPosition => _lockedOnPosition;

    [Header("锁定状态（由锁定系统控制）")]
    [SerializeField] private bool _isLocked = false;
    public bool IsLocked => _isLocked;

    // 新增：全局当前被锁定的怪物（确保同一时间只有一个怪物显示锁定标志）
    private static MonsterBase s_currentLocked;
    
    private Vector3 spawnPosition;
    private Transform player;
    private NavMeshAgent navMeshAgent;
    private MonsterSpawner monsterSpawner;
    
    private MonsterStateMachine stateMachine;
    private MonsterDetection detection;
    private MonsterCombat combat;
    private MonsterAnimationController _animController;
    
    public MonsterData monsterData { get; private set; }

    // Start is intentionally removed. Initialization must be done via Init(...).
    // All components and references are initialized in Init so this component is usable when created at runtime by a spawner.

    private bool _initialized = false;

    private void Awake()
    {
        // 确保锁定标志默认关闭
        if (_lockedOnPosition != null)
        {
            _lockedOnPosition.gameObject.SetActive(false);
        }
    }

    public void Init(MonsterData data, Transform playerTransform, MonsterSpawner spawner)
    {
        if (_initialized)
        {
            Debug.LogWarning($"MonsterBase on '{gameObject.name}' already initialized.");
            return;
        }

        // Record spawn position right away
        spawnPosition = transform.position;

        // Ensure NavMeshAgent exists
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            navMeshAgent = gameObject.AddComponent<NavMeshAgent>();
        }

        // Cache component references (these components are expected on the same GameObject)
        stateMachine = GetComponent<MonsterStateMachine>();
        detection = GetComponent<MonsterDetection>();
        combat = GetComponent<MonsterCombat>();
        _animController = GetComponent<MonsterAnimationController>();

        // Assign provided values
        monsterData = data;
        player = playerTransform != null ? playerTransform : GameObject.FindGameObjectWithTag("Player")?.transform;
        monsterSpawner = spawner; // spawner must be provided by the creator

        // Basic validation and helpful warnings to catch setup issues early
        if (stateMachine == null)
            Debug.LogWarning($"MonsterBase on '{gameObject.name}' is missing MonsterStateMachine component.");
        if (detection == null)
            Debug.LogWarning($"MonsterBase on '{gameObject.name}' is missing MonsterDetection component.");
        if (combat == null)
            Debug.LogWarning($"MonsterBase on '{gameObject.name}' is missing MonsterCombat component.");
        if (_animController == null)
            Debug.LogWarning($"MonsterBase on '{gameObject.name}' is missing MonsterAnimationController component.");
        if (player == null)
            Debug.LogWarning($"MonsterBase on '{gameObject.name}' could not find a player; pass playerTransform into Init to avoid this.");
        if (monsterSpawner == null)
            Debug.LogWarning($"MonsterBase on '{gameObject.name}' received a null MonsterSpawner; ensure the spawner passes itself into Init.");

        // Collider sanity check on root
        var col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError($"[MonsterBase] {name} 缺少根碰撞体 Collider，请在Prefab根物体上添加！（这会导致无法被锁定/受击）");
        }
        else
        {
            if (!col.enabled)
            {
                Debug.LogWarning($"[MonsterBase] {name} 根碰撞体处于禁用状态，已在初始化时强制启用。");
                col.enabled = true;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string layerName = LayerMask.LayerToName(gameObject.layer);
            Debug.Log($"[MonsterBase.Init] {name} ColliderOK enabled={col.enabled} isTrigger={col.isTrigger} layer={gameObject.layer}('{layerName}')");
#endif
        }

        // Configure nav agent speed from this monster's default patrol speed
        if (navMeshAgent != null)
        {
            navMeshAgent.speed = patrolSpeed;
        }

        // Initialize subsystems which depend on these references
        stateMachine?.Initialize(spawnPosition, player, navMeshAgent, monsterSpawner, _animController);
        detection?.Initialize(player);
        combat?.Initialize(data, monsterSpawner, _animController, navMeshAgent);

        // 新增：将巡逻速度作为动画树的“步行=1.0”的参考速度
        var loco = GetComponent<MonsterLocomotionDriver>();
        if (loco != null)
        {
            loco.SetReferenceWalkSpeed(patrolSpeed);
        }

        // 初始化时关闭锁定标志
        if (_lockedOnPosition != null)
        {
            _lockedOnPosition.gameObject.SetActive(false);
        }

        _initialized = true;
    }

    /// <summary>
    /// 被锁定/解除锁定。仅由锁定系统调用。
    /// - 打开时：确保关闭上一个被锁定怪物的标志，再开启自身的锁定标志。
    /// - 关闭时：如果当前就是全局被锁定对象，则清空全局引用；关闭自身标志。
    /// - 打开时还会立刻推送一次当前血量，便于 UI 即刻显示。
    /// </summary>
    public void SetLocked(bool locked)
    {
        if (_isLocked == locked && locked == (_lockedOnPosition != null && _lockedOnPosition.gameObject.activeSelf))
        {
            // 状态无变化且显示一致，直接返回
            return;
        }

        if (locked)
        {
            // 若已有其他怪物被锁定，先关闭其标志
            if (s_currentLocked != null && s_currentLocked != this)
            {
                s_currentLocked.InternalSetLockIndicator(false);
                s_currentLocked._isLocked = false;
            }

            s_currentLocked = this;
            _isLocked = true;
            InternalSetLockIndicator(true);

            // 如果刚刚被锁定，立即推送一次当前血量，便于 UI 立刻显示
            if (combat != null)
            {
                combat.RaiseHealthSnapshot();
            }
        }
        else
        {
            // 关闭自身锁定
            _isLocked = false;
            InternalSetLockIndicator(false);
            if (s_currentLocked == this)
            {
                s_currentLocked = null;
            }
        }
    }

    // 内部帮助方法：切换锁定标志显示
    private void InternalSetLockIndicator(bool on)
    {
        if (_lockedOnPosition != null)
        {
            _lockedOnPosition.gameObject.SetActive(on);
        }
    }

    private void Update()
    {
        // 死亡后不再执行检测和状态机更新（避免尸体继续朝向/寻路）
        if (combat != null && combat.IsDead)
        {
            return;
        }
        // 每帧更新检测与状态机
        detection?.UpdateDetection();
        stateMachine?.UpdateStateMachine();
    }
    
    /// <summary>
    /// 获取命中特效挂点位置
    /// </summary>
    /// <returns>命中特效挂点的Transform，如果不存在则返回null</returns>
    public Transform GetHitVfxPoint()
    {
        if (combat != null)
        {
            return combat.hitVfxPoint;
        }
        return null;
    }
    
}