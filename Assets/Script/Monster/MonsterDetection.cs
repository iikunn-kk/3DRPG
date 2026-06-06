using UnityEngine;

/// <summary>
/// 怪物检测系统，负责检测玩家位置和范围
/// </summary>
public class MonsterDetection : MonoBehaviour
{
    [Tooltip("搜索玩家间隔，避免频繁检测影响性能")]
    public float searchPlayerInterval = 0.5f; // 搜索玩家间隔
    
    [Header("检测参数")]
    [SerializeField] private float chaseRange = 10f;   // 追击范围
    [SerializeField] private float alertRange = 15f;   // 警觉范围
    
    private Transform player;              // 玩家Transform引用
    private CharacterState playerState;    // 检查玩家是否死亡
    private float searchPlayerTimer;       // 搜索玩家计时器
    private float chaseRangeSqr;           // 追击范围的平方（用于优化距离计算）
    private float alertRangeSqr;           // 警觉范围的平方（用于优化距离计算）
    private MonsterStateMachine stateMachine; // 状态机引用
    
    private void Awake()
    {
        stateMachine = GetComponent<MonsterStateMachine>();
        chaseRangeSqr = chaseRange * chaseRange;
        alertRangeSqr = alertRange * alertRange;
    }
    
    public void Initialize(Transform playerTransform, CharacterState playerCharacterState)
    {
        player = playerTransform;
        playerState = playerCharacterState;
    }
    
    public void UpdateDetection()
    {
        // 更新搜索玩家计时器
        searchPlayerTimer += Time.deltaTime;
        
        // 根据设定间隔检测玩家
        if (searchPlayerTimer >= searchPlayerInterval)
        {
            searchPlayerTimer = 0f;
            CheckPlayerInRange();
        }
    }
    
    /// <summary>
    /// 检查玩家是否在范围内（优化版本，使用平方距离比较）
    /// </summary>
    private void CheckPlayerInRange()
    {
        if (player == null) return;

        // 玩家死亡 → 强制不在范围，怪物停止追击
        if (playerState != null && playerState.IsDead)
        {
            stateMachine.SetPlayerInRange(false);
            return;
        }

        // 使用平方距离比较以提高性能，避免开方运算
        float distanceSqr = (transform.position - player.position).sqrMagnitude;
        bool isPlayerInRange = distanceSqr <= chaseRangeSqr;
        stateMachine.SetPlayerInRange(isPlayerInRange);
    }
    
    /// <summary>
    /// 检查玩家是否在警觉范围内
    /// </summary>
    /// <returns></returns>
    public bool IsPlayerInAlertRange()
    {
        if (player != null)
        {
            float distanceSqr = (transform.position - player.position).sqrMagnitude;
            return distanceSqr <= alertRangeSqr;
        }
        return false;
    }
}