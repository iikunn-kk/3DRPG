using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/TargetChangeEventSO")]
public class TargetChangeEventSO : BaseEventSO<MonsterBase>
{
    // 使用 BaseEventSO 的 onEventRaised 和 RaiseEvent
}
