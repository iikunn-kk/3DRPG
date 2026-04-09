using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 怪物生成点类，用于在玩家靠近时生成怪物
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    [Header("生成设置")]
    [Tooltip("关联的怪物数据")]
    public MonsterData monsterData;
    
    [Tooltip("生成怪物的数量")]
    public int monsterCount = 3;
    
    [Tooltip("最大生成的怪物数量")]
    public int maxMonsters = 5;
    
    [Tooltip("生成范围半径")]
    public float spawnRadius = 5f;
    
    [Header("刷新设置")]
    [Tooltip("怪物刷新间隔（秒）")]
    public float respawnTime = 30f;
    
    [Tooltip("玩家触发生成的距离")]
    public float playerTriggerDistance = 10f;
    
    [Header("生成间隔设置")]
    [Tooltip("每个怪物生成之间的间隔时间（秒）")]
    public float spawnInterval = 3f;
    
    [Header("移除设置")]
    [Tooltip("玩家离开区域后延迟移除怪物的时间（秒）")]
    public float removeDelay = 5f;
    
    [Tooltip("移除怪物的间隔时间（秒）")]
    public float removeInterval = 3f;
    
    [Header("生成点设置")]
    [Tooltip("是否在游戏开始时生成怪物")]
    public bool spawnOnStart = false;

    [Header("玩家死亡设置")]
    [Tooltip("玩家死亡后，使周围怪物播放胜利动作的半径（单位：米）")]
    public float playerDeathAffectRadius = 10f;

    // 私有变量
    private List<MonsterBase> spawnedMonsters=new();
    private Transform player;
    private bool isPlayerInRange;
    private float playerCheckTimer;
    private float respawnTimer;
    private bool isSpawning;
    private float playerLeaveTimer;
    private bool isPlayerLeft;
    private Coroutine removeCoroutine;
    private Coroutine spawnCoroutine;
    
    // 缓存的触发距离平方，避免每帧计算开方
    private float playerTriggerDistanceSqr;
    
    // 上次清理列表的时间
    private float lastCleanupTime;
    
    // 清理间隔（秒）
    private const float CLEANUP_INTERVAL = 5f;

    /// <summary>
    /// 对外公开的初始化方法（由 MapManager 在玩家生成后调用）。
    /// 原先 Start 中的逻辑迁移到这里，以避免重复或时序问题。
    /// </summary>
    public void Init(CharacterState playerObj)
    {
        // 重置计时器/状态
        playerCheckTimer = 0f;
        respawnTimer = 0f;
        isSpawning = false;
        isPlayerLeft = false;
        player = playerObj.transform;
        playerTriggerDistanceSqr = playerTriggerDistance * playerTriggerDistance;
        // 立即检测玩家是否在触发范围内（CheckPlayerInRange 内部已做 null 检查）
        CheckPlayerInRange();

        // 如果配置为在游戏开始时生成并且玩家已在范围内，则开始生成
        if (spawnOnStart && isPlayerInRange)
        {
            StartSpawnMonsters();
        }
    }
    
    void Update()
    {
        // 定时检查玩家是否在范围内
        playerCheckTimer += Time.deltaTime;
        if (playerCheckTimer >= 1f) // 每秒检查一次
        {
            playerCheckTimer = 0f;
            CheckPlayerInRange();
        }
        
        // 处理怪物生成和刷新
        HandleMonsterSpawning();
        
        // 处理玩家离开区域后的怪物移除
        HandlePlayerLeaveRegion();
        
        // 定期更新已生成怪物列表，移除已被销毁的怪物
        UpdateSpawnedMonstersListPeriodically();
    }
    
    /// <summary>
    /// 检查玩家是否在触发范围内
    /// </summary>
    private void CheckPlayerInRange()
    {
        if (player != null)
        {
            // 使用距离的平方避免开方运算，提高性能
            float distanceToPlayerSqr = Vector3.SqrMagnitude(transform.position - player.position);
            bool wasPlayerInRange = isPlayerInRange;
            isPlayerInRange = distanceToPlayerSqr <= playerTriggerDistanceSqr;
            
            // 检测玩家是否离开了区域
            if (wasPlayerInRange && !isPlayerInRange)
            {
                isPlayerLeft = true;
                playerLeaveTimer = 0f;
                
                // 停止生成协程
                if (spawnCoroutine != null)
                {
                    StopCoroutine(spawnCoroutine);
                    spawnCoroutine = null;
                    isSpawning = false;
                }
            }
            // 玩家重新进入区域
            else if (!wasPlayerInRange && isPlayerInRange)
            {
                isPlayerLeft = false;
                // 停止移除协程
                if (removeCoroutine != null)
                {
                    StopCoroutine(removeCoroutine);
                    removeCoroutine = null;
                }
            }
        }
    }
    
    /// <summary>
    /// 是否在生成范围内（忽略高度）
    /// </summary>
    public bool IsWithinSpawnBounds(Vector3 pos)
    {
        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = pos; b.y = 0f;
        return (a - b).sqrMagnitude <= (spawnRadius * spawnRadius);
    }

    /// <summary>
    /// 处理玩家离开区域后的逻辑
    /// </summary>
    private void HandlePlayerLeaveRegion()
    {
        if (isPlayerLeft && spawnedMonsters.Count > 0)
        {
            playerLeaveTimer += Time.deltaTime;
            
            // 延迟时间到后开始移除怪物
            if (playerLeaveTimer >= removeDelay)
            {
                // 启动移除协程
                if (removeCoroutine == null)
                {
                    removeCoroutine = StartCoroutine(RemoveMonstersOverTime());
                }
            }
        }
    }
    
    /// <summary>
    /// 随时间逐个移除怪物（软移除：优先让其回到出生点，避免战斗中硬移除）
    /// </summary>
    private IEnumerator RemoveMonstersOverTime()
    {
        while (spawnedMonsters.Count > 0 && isPlayerLeft)
        {
            // 找到第一个适合被移除的怪物：非空、未交战、未死亡、且靠近出生点
            MonsterBase candidate = null;
            MonsterBase needsReturn = null;

            // 用 for 避免 foreach 分配
            for (int i = 0; i < spawnedMonsters.Count; i++)
            {
                var m = spawnedMonsters[i];
                if (m == null) continue;

                var combat = m.GetComponent<MonsterCombat>();
                if (combat != null && combat.IsDead)
                {
                    // 死亡尸体的清理交给自身流程，Spawner 不硬删
                    continue;
                }

                var sm = m.GetComponent<MonsterStateMachine>();
                if (sm != null)
                {
                    // 交战中（Alert/Chase/Attack）的怪不删除
                    if (sm.IsEngaged)
                    {
                        continue;
                    }
                    // 若尚未回到出生点，先强制回程，稍后再删
                    if (!sm.IsNearSpawn)
                    {
                        needsReturn = m;
                        break;
                    }
                    // 合格：未交战、已在出生点附近
                    candidate = m;
                    break;
                }
                else
                {
                    // 没有状态机，退而求其次：按距离 spawner 判断
                    if (IsWithinSpawnBounds(m.transform.position))
                    {
                        candidate = m;
                        break;
                    }
                    else
                    {
                        needsReturn = m;
                        break;
                    }
                }
            }

            if (needsReturn != null)
            {
                var sm = needsReturn.GetComponent<MonsterStateMachine>();
                if (sm != null)
                {
                    sm.ForceReturnToSpawn();
                }
                // 等待一会儿再尝试
                yield return new WaitForSeconds(removeInterval);
                continue;
            }

            if (candidate != null)
            {
                spawnedMonsters.Remove(candidate);
                Destroy(candidate.gameObject);
                yield return new WaitForSeconds(removeInterval);
                continue;
            }

            // 没有可删对象，稍后重试
            yield return new WaitForSeconds(removeInterval);
        }
        
        removeCoroutine = null;
    }
    
    /// <summary>
    /// 处理怪物生成和刷新逻辑
    /// </summary>
    private void HandleMonsterSpawning()
    {
        // 如果玩家在范围内且未在生成中
        if (isPlayerInRange && !isSpawning)
        {
            // 检查是否需要生成怪物
            if (spawnedMonsters.Count < maxMonsters)
            {
                // 检查刷新计时器
                respawnTimer += Time.deltaTime;
                if (respawnTimer >= respawnTime)
                {
                    respawnTimer = 0f;
                    StartSpawnMonsters();
                }
            }
        }
    }
    
    /// <summary>
    /// 开始生成怪物
    /// </summary>
    private void StartSpawnMonsters()
    {
        if (spawnCoroutine == null)
        {
            spawnCoroutine = StartCoroutine(SpawnMonstersOverTime());
        }
    }
    
    /// <summary>
    /// 随时间逐个生成怪物（使用 NavMesh.SamplePosition 保证落在可行走区域）
    /// </summary>
    private IEnumerator SpawnMonstersOverTime()
    {
        isSpawning = true;
        
        int monstersToSpawn = Mathf.Min(monsterCount, maxMonsters - spawnedMonsters.Count);
        
        for (int i = 0; i < monstersToSpawn; i++)
        {
            // 检查是否仍然满足生成条件
            if (!isPlayerInRange || spawnedMonsters.Count >= maxMonsters)
            {
                break;
            }
            
            // 在生成点半径范围内找一个 NavMesh 合法点
            Vector3 spawnPos = GetRandomPointInBounds();
            
            // 生成怪物
            GameObject monsterObj = Instantiate(monsterData.monsterModel, spawnPos, Quaternion.identity);
            MonsterBase monster = monsterObj.GetComponent<MonsterBase>();
            
            if (monster != null)
            {
                // 初始化怪物数据
                monster.Init(monsterData, player,this);
                spawnedMonsters.Add(monster);
            }
            
            // 等待下一次生成
            if (i < monstersToSpawn - 1) // 如果不是最后一个怪物，则等待
            {
                yield return new WaitForSeconds(spawnInterval);
            }
        }
        
        spawnCoroutine = null;
        isSpawning = false;
    }
    
    /// <summary>
    /// 定期更新已生成怪物列表，移除已被销毁的怪物
    /// </summary>
    private void UpdateSpawnedMonstersListPeriodically()
    {
        // 每隔一定时间清理一次列表，而不是每帧都清理
        if (Time.time - lastCleanupTime > CLEANUP_INTERVAL)
        {
            lastCleanupTime = Time.time;
            spawnedMonsters.RemoveAll(monster => monster == null);
        }
    }
    
    /// <summary>
    /// 手动刷新怪物（可用于外部调用）
    /// </summary>
    public void ManualRespawn()
    {
        respawnTimer = 0f;
        StartSpawnMonsters();
    }
    
    /// <summary>
    /// 清理所有生成的怪物
    /// </summary>
    public void ClearSpawnedMonsters()
    {
        // 停止移除协程
        if (removeCoroutine != null)
        {
            StopCoroutine(removeCoroutine);
            removeCoroutine = null;
        }
        
        // 停止生成协程
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
            isSpawning = false;
        }
        
        // 使用for循环而不是foreach避免enumerator分配
        for (int i = spawnedMonsters.Count - 1; i >= 0; i--)
        {
            if (spawnedMonsters[i] != null)
            {
                Destroy(spawnedMonsters[i].gameObject);
            }
        }
        spawnedMonsters.Clear();
    }
    
    /// <summary>
    /// 获取当前生成的怪物数量
    /// </summary>
    /// <returns>当前存活的怪物数量</returns>
    public int GetAliveMonsterCount()
    {
        UpdateSpawnedMonstersListPeriodically();
        return spawnedMonsters.Count;
    }
    
    /// <summary>
    /// 获取最大可生成的怪物数量
    /// </summary>
    /// <returns>最大怪物数量</returns>
    public int GetMaxMonsters()
    {
        return maxMonsters;
    }
    
    /// <summary>
    /// 设置最大怪物数量
    /// </summary>
    /// <param name="newMax">新的最大怪物数量</param>
    public void SetMaxMonsters(int newMax)
    {
        maxMonsters = Mathf.Max(0, newMax);
    }
    
    /// <summary>
    /// 在生成范围内获取一个随机点（保证在 NavMesh 上）
    /// </summary>
    /// <returns>NavMesh上的一个随机点</returns>
    public Vector3 GetRandomPointInBounds()
    {
        // 多次尝试采样，提升命中率
        for (int i = 0; i < 8; i++)
        {
            Vector3 randomPoint = transform.position + Random.insideUnitSphere * spawnRadius;
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }
        // 兜底：返回生成点自身（若不在 NavMesh 上，则再尝试半径内采样）
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit2, spawnRadius, NavMesh.AllAreas))
        {
            return hit2.position;
        }
        return transform.position; // 最终兜底
    }
    
    /// <summary>
    /// 当一个怪物死亡时由怪物实例调用
    /// </summary>
    /// <param name="monster">死亡的怪物</param>
    public void ReportMonsterDeath(MonsterBase monster)
    {
        if (spawnedMonsters.Contains(monster))
        {
            spawnedMonsters.Remove(monster);
        }
        // 可以选择在这里触发刷新逻辑，或者依赖原有的刷新计时器
    }
    public void OnPlayerDeath(GameObject playerObj)
    {
        // 停止生成协程
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
            isSpawning = false;
        }

        // 停止移除协程
        if (removeCoroutine != null)
        {
            StopCoroutine(removeCoroutine);
            removeCoroutine = null;
        }

        // 禁用所有已生成怪物的攻击/AI，距离玩家一定范围内的怪物播放胜利/庆祝动画
        if (spawnedMonsters != null)
        {
            // 复制以便在循环中安全修改原列表（不过我们不会在此删除）
            var listCopy = new List<MonsterBase>(spawnedMonsters);
            foreach (var monster in listCopy)
            {
                if (monster == null) continue;

                var combat = monster.GetComponent<MonsterCombat>();
                if (combat != null) combat.enabled = false;

                var stateMachine = monster.GetComponent<MonsterStateMachine>();
                if (stateMachine != null) stateMachine.enabled = false;

                var detection = monster.GetComponent<MonsterDetection>();
                if (detection != null) detection.enabled = false;

                var agent = monster.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }

                if (playerObj != null)
                {
                    float sqrDist = (monster.transform.position - playerObj.transform.position).sqrMagnitude;
                    if (sqrDist <= playerDeathAffectRadius * playerDeathAffectRadius)
                    {
                        var anim = monster.GetComponent<MonsterAnimationController>();
                        if (anim != null)
                        {
                            anim.PlayCelebrate();
                        }
                    }
                    else
                    {
                        var anim = monster.GetComponent<MonsterAnimationController>();
                        if (anim != null)
                        {
                            anim.PlayIdle();
                        }
                    }
                }
                else
                {
                    var anim = monster.GetComponent<MonsterAnimationController>();
                    if (anim != null)
                    {
                        anim.PlayIdle();
                    }
                }
            }
        }
    }
    public void OnPlayerBeginRespawn(GameObject playerObj)
    {
        ClearSpawnedMonsters();
    }
    public void OnPlayerRespawned(GameObject playerObj)
    {
        Init(playerObj.GetComponent<CharacterState>());
    }
    /// <summary>
    /// 在Scene视图中绘制生成范围 Gizmos
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 绘制触发范围
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, playerTriggerDistance);
        
        // 绘制生成范围
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
