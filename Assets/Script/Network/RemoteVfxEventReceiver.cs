using UnityEngine;

/// <summary>
/// 空壳组件：附加到远程玩家 / VFX GameObject 上接收 AnimationEvent 回调。
/// 远程玩家的 CharacterAnimationController 被 StripGameplayComponents 销毁，
/// 但 Animator 播放的动画片段仍会触发 AnimationEvent（CrossFadeInFixedTime 驱动）。
/// 此组件提供同名空方法避免 Unity 报 "has no receiver"。
/// </summary>
public class RemoteVfxEventReceiver : MonoBehaviour
{
    void OnSkillCastPoint() { }
    void OnActionAnimationEnd() { }
    void OnAttackPrecastComplete() { }
}
