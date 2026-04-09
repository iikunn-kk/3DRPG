using UnityEngine;

/// <summary>
/// 可交互对象的示例实现
/// 演示如何实现IInteractable接口来创建自定义交互逻辑
/// </summary>
public class InteractableExample : MonoBehaviour, IInteractable
{
    [Header("交互设置")]
    [SerializeField] private Transform uiParent;

    // 中央化提示由 PlayerInteraction 管理
    public string GetInteractionPrompt()
    {
        return "交互";
    }

    public Transform GetPromptAnchor()
    {
        return uiParent != null ? uiParent : transform;
    }

    /// <summary>
    /// 执行交互逻辑
    /// </summary>
    /// <param name="playerInteraction">玩家交互组件</param>
    public void Interact(PlayerInteraction playerInteraction)
    {
        // 中央提示由 PlayerInteraction 管理
        // 实现自定义交互逻辑
        Debug.Log("与对象交互: " + gameObject.name);
        // 示例：在控制台输出一条消息
        Debug.Log("执行了自定义交互逻辑！");
    }
}