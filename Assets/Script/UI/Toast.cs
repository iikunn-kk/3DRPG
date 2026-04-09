using UnityEngine;
using UnityEngine.UI;
using System;
using DG.Tweening;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// 控制单个 Toast 的显示、移动与淡入淡出动画（使用 DOTween）。
/// 支持根据内容自适应尺寸，并受最小/最大宽度限制，布局为左图标右文字。
/// 需要一个 CanvasGroup、RectTransform、TextMeshProUGUI 与 Image（icon）。
/// 支持点击关闭（实现 IPointerClickHandler）。
/// </summary>
public class Toast : MonoBehaviour, IPointerClickHandler
{
    [Header("组件引用")]
    public TextMeshProUGUI messageText;
    public Image iconImage;

    [Header("尺寸与布局")]
    [Tooltip("Toast的最小宽度")]
    [SerializeField] private float minWidth = 300f;
    [Tooltip("Toast的最大宽度")]
    [SerializeField] private float maxWidth = 800f;
    [Tooltip("内容与背景左右边缘的总边距")]
    [SerializeField] private float horizontalPadding = 60f;
    [Tooltip("内容与背景上下边缘的总边距")]
    [SerializeField] private float verticalPadding = 40f;
    [Tooltip("图标与文字的间距")]
    [SerializeField] private float iconToTextSpacing = 20f;

    [Header("动画与时长")]
    [Tooltip("默认的显示时长")]
    public float defaultShowDuration = 2f;
    [Tooltip("淡入/淡出动画时长")]
    public float fadeDuration = 0.25f;
    [Tooltip("位置移动动画时长")]
    public float moveDuration = 0.35f;

    // 私有组件引用
    private RectTransform _rect;
    private CanvasGroup _canvasGroup;
    
    // 动画序列
    private Sequence _seq;

    // 事件与状态
    public Action<Toast> OnDismissed;
    private float _showDuration;
    public bool IsNew { get; set; } = true;

    private void Awake()
    {
        // 在 Awake 中获取引用，确保组件存在
        _rect = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// 设置Toast内容，并根据内容自动调整尺寸
    /// </summary>
    /// <param name="message">要显示的文本消息</param>
    /// <param name="icon">要显示的图标</param>
    /// <param name="duration">显示时长（-1表示使用默认值）</param>
    public void Setup(string message, Sprite icon, float duration = -1f)
    {
        // 1. 设置文本内容
        if (messageText != null)
        {
            messageText.text = message ?? string.Empty;
        }

        // 2. 设置图标
        bool hasIcon = iconImage != null && icon != null;
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = hasIcon;
        }

        // --- 核心尺寸计算逻辑 ---

        // 3. 计算图标和间距贡献的宽度
        float iconWidth = hasIcon ? iconImage.rectTransform.rect.width : 0f;
        float spacing = hasIcon ? iconToTextSpacing : 0f;
        float nonTextWidth = horizontalPadding + iconWidth + spacing;

        // 4. 计算文本的首选宽度
        float textPreferredWidth = messageText.GetPreferredValues(message).x;

        // 5. 计算并限制最终宽度
        float desiredWidth = textPreferredWidth + nonTextWidth;
        float finalWidth = Mathf.Clamp(desiredWidth, minWidth, maxWidth);

        // 6. 应用最终宽度
        _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, finalWidth);

        // 7. 基于最终宽度，计算文本换行后的高度
        float textAvailableWidth = finalWidth - nonTextWidth;
        float textPreferredHeight = messageText.GetPreferredValues(message, textAvailableWidth, 0).y;

        // 8. 计算并应用最终高度 (高度由文本和图标中较高的一个决定)
        float iconHeight = hasIcon ? iconImage.rectTransform.rect.height : 0f;
        float contentHeight = Mathf.Max(textPreferredHeight, iconHeight);
        float finalHeight = contentHeight + verticalPadding;
        _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, finalHeight);
        
        // -------------------------

        _showDuration = duration > 0 ? duration : defaultShowDuration;
        IsNew = true;
    }
    
    /// <summary>
    /// 播放显示动画
    /// </summary>
    /// <param name="fromPos">起始位置</param>
    /// <param name="toPos">目标位置</param>
    public void Show(Vector2 fromPos, Vector2 toPos)
    {
        _seq?.Kill();
        _canvasGroup.alpha = 0f;
        _rect.anchoredPosition = fromPos;
        IsNew = false;

        _seq = DOTween.Sequence();
        _seq.Append(_rect.DOAnchorPos(toPos, moveDuration).SetEase(Ease.OutCubic));
        _seq.Join(_canvasGroup.DOFade(1f, fadeDuration));
        _seq.AppendInterval(_showDuration);
        _seq.Append(_canvasGroup.DOFade(0f, fadeDuration));
        _seq.Join(_rect.DOAnchorPos(toPos + new Vector2(0, 20f), 0.25f).SetEase(Ease.InCubic));
        _seq.OnComplete(() =>
        {
            OnDismissed?.Invoke(this);
            Destroy(gameObject);
        });
    }

    /// <summary>
    /// (由管理器调用)将Toast移动到新的位置，用于重新排序
    /// </summary>
    /// <param name="target">新的目标位置</param>
    public void MoveTo(Vector2 target)
    {
        _rect.DOAnchorPos(target, moveDuration).SetEase(Ease.OutCubic);
    }

    /// <summary>
    /// 强制立即开始消失动画
    /// </summary>
    public void ForceDismiss()
    {
        _seq?.Kill();
        _seq = DOTween.Sequence();
        _seq.Append(_canvasGroup.DOFade(0f, fadeDuration));
        _seq.Join(_rect.DOAnchorPos(_rect.anchoredPosition + new Vector2(0, 20f), fadeDuration).SetEase(Ease.InCubic));
        _seq.OnComplete(() =>
        {
            OnDismissed?.Invoke(this);
            Destroy(gameObject);
        });
    }

    /// <summary>
    /// 实现IPointerClickHandler接口，允许用户点击Toast来关闭它
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        ForceDismiss();
    }

    private void OnDisable()
    {
        transform.DOKill();
        // 在对象销毁时，确保杀死所有相关的DOTween动画，防止内存泄漏
        _seq?.Kill();
    }
}