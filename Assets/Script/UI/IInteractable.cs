using UnityEngine;

public interface IInteractable
{
    /// <summary>
    /// 获取交互提示文本（由 PlayerInteraction 的中央提示显示）
    /// </summary>
    string GetInteractionPrompt();

    /// <summary>
    /// 获取用于将提示附着的锚点（可为 null）
    /// </summary>
    Transform GetPromptAnchor();

    /// <summary>
    /// 执行交互逻辑
    /// </summary>
    /// <param name="playerInteraction">玩家交互组件</param>
    void Interact(PlayerInteraction playerInteraction);
}