using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 拖拽旋转监听器，用于监听RawImage上的拖拽事件
/// </summary>
public class DragRotationListener : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private CreateCharacterPanel characterPanel;

    /// <summary>
    /// 初始化监听器
    /// </summary>
    /// <param name="panel">角色创建面板引用</param>
    public void Initialize(CreateCharacterPanel panel)
    {
        characterPanel = panel;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        characterPanel?.BeginDrag(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        characterPanel?.OnDrag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        characterPanel?.EndDrag(eventData.position);
    }
}