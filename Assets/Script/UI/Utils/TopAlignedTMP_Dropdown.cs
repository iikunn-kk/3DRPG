using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 继承自 TMP_Dropdown，并修正其打开时滚动条不在顶部的问题。
/// 使用 OnPointerClick 作为入口点，因为它是一个可被重写的 virtual 方法。
/// </summary>
public class TopAlignedTMPDropdown : TMP_Dropdown
{
    /// <summary>
    /// 重写 OnPointerClick 方法。这是当用户点击UI元素时被调用的方法。
    /// </summary>
    /// <param name="eventData">点击事件数据</param>
    public override void OnPointerClick(PointerEventData eventData)
    {
        // 【关键】首先必须调用基类（TMP_Dropdown）的 OnPointerClick 方法。
        // 这一步会执行原始的逻辑，包括调用 Show() 来把下拉列表显示出来。
        // 如果不调用 base.OnPointerClick，下拉列表将不会弹出。
        base.OnPointerClick(eventData);

        // 在下拉列表被创建后，启动协程来调整滚动位置
        StartCoroutine(ScrollToTopCoroutine());
    }

    private IEnumerator ScrollToTopCoroutine()
    {
        // 等待一帧，确保下拉列表的GameObject已经被创建并完成布局
        yield return new WaitForEndOfFrame();

        // Dropdown List 是作为 Canvas 的子物体被创建的，所以我们从父级 Canvas 开始查找
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            yield break;
        }

        // 在 Canvas 的所有子物体中查找名叫 "Dropdown List" 的那个 ScrollRect
        // 这是比直接在整个场景中 FindObject 更可靠的方式
        ScrollRect[] scrollRects = canvas.GetComponentsInChildren<ScrollRect>(true);
        foreach (var sr in scrollRects)
        {
            // 通过判断父物体的名字来精确定位到我们需要的那个列表
            if (sr.transform.parent != null && sr.transform.parent.name == "Dropdown List")
            {
                // 将垂直滚动位置设置为 1 (顶部)
                sr.verticalNormalizedPosition = 1f;
                // 找到并设置后就可以退出了，避免影响其他可能存在的下拉列表
                break;
            }
        }
    }
}