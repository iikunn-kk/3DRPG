using UnityEngine;

// 载荷：用于通过 BaseEventSO 广播当前被锁定怪物的血量信息
public struct TargetHealthPayload
{
    public MonsterBase target;
    public int current;
    public int max;
}

