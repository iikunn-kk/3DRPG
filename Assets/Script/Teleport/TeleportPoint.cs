using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 场景中的“地图交互点”。
/// 新逻辑：用于打开世界地图( MapPanel )，玩家在面板中选择任意区域进行传送。
/// 若仍然存在旧字段 targetSceneName / targetSpawnPointId，只是为了兼容序列化，不再直接使用。
/// </summary>
[DisallowMultipleComponent]
public class TeleportPoint : MonoBehaviour,IInteractable
{

    [Tooltip("玩家进入触发器到真正打开面板的延迟(秒)")] public float openMapDelay = 0.05f;

    [Header("交互UI (可选)")] [SerializeField] private Transform uiParent; // 用于放置 InteractionPromptUI

    // 提示由 PlayerInteraction 的中央实例负责展示
    public string GetInteractionPrompt()
    {
        return "打开地图";
    }

    public Transform GetPromptAnchor()
    {
        return uiParent != null ? uiParent : transform;
    }

    public void Interact(PlayerInteraction playerInteraction)
    {
        // 中央提示由 PlayerInteraction 管理
        var mapPanel = UIManager.Instance.OpenPanel<MapPanel>(out var isOpen);
        if (isOpen)
        {
            mapPanel.GetComponent<MapPanel>().Init();
        }
    }
}
