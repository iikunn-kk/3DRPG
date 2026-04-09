using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// NPC选项按钮组件
/// 管理NPC对话选项按钮的基本属性和行为
/// </summary>
public class NpcOptionButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{

    [Header("UI元素")]
    [Tooltip("按钮组件本身")]
    public Image bkImage;         // 按钮组件
    
    [Tooltip("按钮文本（TextMeshPro）")]
    public TMP_Text buttonText;   // 按钮文本（使用TMP）
    
    [Header("颜色设置")]
    [Tooltip("默认状态下的文本颜色")]
    public Color defaultColor = new Color(0.8f,1,1,1); // 默认颜色
    
    [Tooltip("高亮状态下的文本颜色")]
    public Color highlightColor = Color.cyan; // 高亮颜色

    private Action _clickCallback; // 按钮点击回调
    [SerializeField] private Image icon;
    [SerializeField] private Sprite questIcon;
    [SerializeField] private Sprite shopIcon;
    [SerializeField] private Sprite closeIcon;
    
    /// <summary>
    /// 初始化按钮
    /// </summary>
    /// <param name="type">选项类型</param>
    /// <param name="text">按钮显示的文本</param>
    /// <param name="callback">按钮点击回调</param>
    /// <param name="overrideIcon">可选的自定义图标</param>
    public void Initialize(OptionType type, string text, Action callback, Sprite overrideIcon = null)
    {
        // 设置按钮文本
        if (buttonText != null)
        {
            buttonText.text = text;
            buttonText.gameObject.SetActive(true);
        }
        // 保存回调
        _clickCallback = callback;
        // 自动绑定 Button 组件的点击事件（如果 prefab 上有 Button）
        var btn = GetComponent<Button>() ?? GetComponentInChildren<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnClick);
        }
        // 先按类型设置默认图标
        SetColorByType(type);
        // 如果有自定义图标则覆盖
        if (overrideIcon != null && icon != null)
        {
            icon.sprite = overrideIcon;
            icon.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 按钮点击事件处理
    /// </summary>
    public void OnClick()
    {
        _clickCallback?.Invoke();
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }
    
    /// <summary>
    /// 根据选项类型设置按钮颜色与图标
    /// </summary>
    private void SetColorByType(OptionType type)
    {
        if (icon == null) return;
        switch (type)
        {
            case OptionType.Quest:
                icon.sprite = questIcon;
                break;
            case OptionType.Shop:
                icon.sprite = shopIcon;
                break;
            case OptionType.Close:
                icon.sprite = closeIcon;
                break;
            default:
                icon.sprite = questIcon;
                break;
        }
        if (icon.sprite != null)
            icon.gameObject.SetActive(true);
        else
            icon.gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
       bkImage.color = highlightColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
      bkImage.color = defaultColor;
    }
}
