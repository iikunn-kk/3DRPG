using UnityEngine;

[CreateAssetMenu(menuName = "Events/TargetHealthEventSO")]
public class TargetHealthEventSO : BaseEventSO<TargetHealthPayload>
{
    // 使用 BaseEventSO 的 onEventRaised 和 RaiseEvent
}
