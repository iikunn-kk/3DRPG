using UnityEngine;
using TMPro; // 使用TextMeshPro命名空间
using System;

public class InteractionPromptUI : MonoBehaviour
{
    [Tooltip("显示提示文本的TMP组件")] public TMP_Text promptText; // 提示文本（使用TMP）

    private Action _taskButtonCallback;
    private Action _customButtonCallback;

    [Header("跟随设置")] [Tooltip("提示相对跟随目标的位置偏移")] public Vector3 worldOffset = new Vector3(0, 2f, 0);

    // 记录当前跟随的锚点（不再把自身作为其子对象，避免锚点销毁时一起被销毁）
    private Transform _followTarget;
    private Transform _initialParent;

    private void Awake()
    {
        _initialParent = transform.parent; // 记录初始父级（通常是世界空间Canvas或UI根）
    }

    /// <summary>
    /// 显示交互提示
    /// 当玩家靠近可交互对象时调用此方法
    /// </summary>
    /// <param name="text">要显示的提示文本</param>
    public void ShowPrompt(string text)
    {
        // 设置提示文本
        if (promptText != null)
        {
            promptText.text = text;
        }
        // 显示整个UI对象
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 隐藏交互提示
    /// </summary>
    public void HidePrompt()
    {
        gameObject.SetActive(false);
        _followTarget = null; // 清除跟随目标
    }

    /// <summary>
    /// 将提示附着(逻辑跟随)到指定锚点。现在不再改变父级，避免锚点对象销毁时本提示也被销毁。
    /// </summary>
    public void AttachTo(Transform parent)
    {
        if (parent == null) return;
        // 若之前被错误地作为其他物体子级（旧逻辑残留），恢复到初始父级
        if (transform.parent != _initialParent)
        {
            transform.SetParent(_initialParent, true);
        }
        _followTarget = parent;
        // 立即同步一次位置
        transform.position = _followTarget.position + worldOffset;
    }

    private void LateUpdate()
    {
        if (!gameObject.activeSelf) return;
        if (_followTarget == null)
        {
            // 跟随目标已被销毁或失效，自动隐藏提示
            HidePrompt();
            return;
        }
        // 跟随更新
        transform.position = _followTarget.position + worldOffset;
    }
}