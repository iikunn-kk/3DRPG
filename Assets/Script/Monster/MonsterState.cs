/// <summary>
/// 怪物状态枚举
/// </summary>
public enum MonsterState
{
    /// <summary>
    /// 待机状态
    /// </summary>
    Idle,
    
    /// <summary>
    /// 巡逻状态
    /// </summary>
    Patrol,
    
    /// <summary>
    /// 警觉状态：面向玩家并显示警觉特效
    /// </summary>
    Alert,
    
    /// <summary>
    /// 追击状态
    /// </summary>
    Chase,
    
    /// <summary>
    /// 攻击状态
    /// </summary>
    Attack,
    
    /// <summary>
    /// 返回出生点状态
    /// </summary>
    ReturnToSpawn,
    
    /// <summary>
    /// 死亡状态：播放死亡动画、执行掉落，不再进行任何逻辑，等待清理
    /// </summary>
    Death
}