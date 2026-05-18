namespace PlayerFSM
{
    /// <summary>
    /// 玩家所有状态的枚举定义。
    /// 共 12 个状态，分 5 类：移动类、物理类、动作类、叠加类、终结态。
    /// </summary>
    public enum PlayerState
    {
        /// <summary>待机：无移动输入</summary>
        Idle,
        /// <summary>行走：有移动输入，非冲刺非蹲伏</summary>
        Walk,
        /// <summary>冲刺：按住 Sprint + 前向输入</summary>
        Sprint,
        /// <summary>蹲伏：按住 Crouch，速度减半</summary>
        Crouch,
        /// <summary>跳跃：空中物理状态</summary>
        Jump,
        /// <summary>翻滚：物理驱动滚动位移，带冷却</summary>
        Roll,
        /// <summary>攻击：播放攻击动画，锁定控制</summary>
        Attack,
        /// <summary>技能：播放技能动画，锁定控制</summary>
        Skill,
        /// <summary>Buff：播放 Buff 动画，锁定控制</summary>
        Buff,
        /// <summary>通道攻击：Pre-Loop-End 三段</summary>
        ChannelAttack,
        /// <summary>受击：Layer overlay，不改变基础状态</summary>
        Hurt,
        /// <summary>死亡：永久锁定，等待复活</summary>
        Death,
    }
}
