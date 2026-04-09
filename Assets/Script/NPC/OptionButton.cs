using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 选项按钮组件
/// 管理选项按钮的基本属性和行为
/// </summary>
public class OptionButton : MonoBehaviour
{
    [Header("UI元素")]
    [Tooltip("按钮组件本身")]
    public Button button;         // 按钮组件
    
    [Tooltip("按钮文本（TextMeshPro）")]
    public TMP_Text buttonText;   // 按钮文本（使用TMP）

    [Header("颜色设置")]
    [Tooltip("默认状态下的文本颜色")]
    public Color defaultColor = Color.white; // 默认颜色
    
    [Tooltip("高亮状态下的文本颜色")]
    public Color highlightColor = Color.yellow; // 高亮颜色

    private System.Action _clickCallback; // 按钮点击回调

    /// <summary>
    /// 初始化按钮
    /// </summary>
    /// <param name="type">选项类型</param>
    /// <param name="text">按钮显示的文本</param>
    /// <param name="callback">按钮点击回调</param>
    public void Initialize(OptionType type, string text, System.Action callback)
    {
        // 设置按钮文本
        if (buttonText != null)
        {
            buttonText.text = text;
            buttonText.color = defaultColor;
        }

        // 保存回调
        _clickCallback = callback;

        // 添加点击事件监听
        if (button != null)
        {
            button.onClick.AddListener(OnClick);
        }

        // 根据类型设置颜色（可选）
        SetColorByType(type);
    }

    /// <summary>
    /// 按钮点击事件处理
    /// </summary>
    private void OnClick()
    {
        _clickCallback?.Invoke();
    }

    /// <summary>
    /// 鼠标进入事件处理（高亮效果）
    /// </summary>
    public void OnMouseEnter()
    {
        if (buttonText != null)
        {
            buttonText.color = highlightColor;
        }
    }

    /// <summary>
    /// 鼠标退出事件处理（恢复默认颜色）
    /// </summary>
    public void OnMouseExit()
    {
        if (buttonText != null)
        {
            buttonText.color = defaultColor;
        }
    }

    /// <summary>
    /// 根据选项类型设置按钮颜色
    /// </summary>
    /// <param name="type">选项类型</param>
    private void SetColorByType(OptionType type)
    {
        // Use the centralized OptionType (Quest/Shop/Close). Map colors accordingly.
        if (buttonText == null) return;
        switch (type)
        {
            case OptionType.Quest:
                buttonText.color = Color.yellow;
                break;
            case OptionType.Shop:
                buttonText.color = Color.green;
                break;
            case OptionType.Close:
                buttonText.color = Color.gray;
                break;
            default:
                buttonText.color = defaultColor;
                break;
        }
    }
}