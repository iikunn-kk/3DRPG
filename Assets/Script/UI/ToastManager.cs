using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 轻量 Toast 系统：在画面顶端中部堆叠显示提示信息，并自动淡出销毁。
/// 通过实例化预制体来创建 Toast，并管理它们的生命周期与布局。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ToastManager : MonoBehaviour
{
    [Header("核心配置")]
    [Tooltip("Toast项的预制体，该预制体上必须挂载 Toast 组件")]
    [SerializeField] private GameObject toastPrefab;

    [Header("布局配置")]
    [Tooltip("每个Toast之间的垂直间距")]
    [SerializeField] private float spacing = 10f;
    [Tooltip("第一个Toast距离顶部的初始偏移")]
    [SerializeField] private float topPadding = 20f;

    [Header("动画与生命周期")]
    [Tooltip("Toast的默认显示时长（秒）")]
    [SerializeField] private float defaultDuration = 2.5f;
    [Tooltip("新Toast的起始位置，相对于其最终位置的偏移")]
    [SerializeField] private Vector2 startOffset = new Vector2(0, -50f);

    [Header("容量限制")]
    [Tooltip("屏幕上最多同时显示的Toast数量")]
    [SerializeField] private int maxToasts = 4;

    // 新增：控制 ToastManager 在屏幕高度上的比例位置（从顶部向下的比例，0.25 = 25%）
    [Header("位置 (相对于屏幕高度)")]
    [Tooltip("将 ToastManager 容器置于屏幕顶部下方的比例位置 (0..1)，例如 0.25 表示从顶部向下 25% 的位置")]
    [SerializeField, Range(0f, 1f)] private float verticalScreenRatio = 0.25f;

    private readonly List<Toast> _activeToasts = new();
    private RectTransform _rect;
    private Canvas _rootCanvas;
    private float _lastCanvasHeight = -1f;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _rootCanvas = GetComponentInParent<Canvas>();

        if (_rect == null)
        {
            Debug.LogWarning("ToastManager requires a RectTransform component.");
            enabled = false;
            return;
        }

        // 确保 Anchor 置于顶部中间，以便作为Toast的布局容器
        _rect.anchorMin = new Vector2(0.5f, 1f);
        _rect.anchorMax = new Vector2(0.5f, 1f);
        _rect.pivot = new Vector2(0.5f, 1f);
        // 将容器放在屏幕顶部下方的 verticalScreenRatio 位置（例如 0.25 = 顶部下方 25%）
        float canvasHeight = GetCanvasHeight();
        // 计算 anchoredPosition 的 Y 值：由于锚点在顶部，向下为负值
        float y = -canvasHeight * Mathf.Clamp01(verticalScreenRatio);
        _rect.anchoredPosition = new Vector2(0f, y);
        _lastCanvasHeight = canvasHeight;

        // 防御性校验：确保数值在合理范围
        if (maxToasts < 1) maxToasts = 1;
        if (spacing < 0f) spacing = 0f;
        if (defaultDuration <= 0f) defaultDuration = 2.5f;
    }

    // 当父级 Canvas 或分辨率发生变化时，自动更新管理器位置
    protected virtual void OnRectTransformDimensionsChange()
    {
        UpdateManagerPositionIfNeeded();
    }

    private void UpdateManagerPositionIfNeeded()
    {
        float canvasHeight = GetCanvasHeight();
        if (!Mathf.Approximately(canvasHeight, _lastCanvasHeight))
        {
            float y = -canvasHeight * Mathf.Clamp01(verticalScreenRatio);
            if (_rect != null)
                _rect.anchoredPosition = new Vector2(0f, y);
            _lastCanvasHeight = canvasHeight;
        }
    }

    private float GetCanvasHeight()
    {
        // 优先使用根 Canvas 的 RectTransform 大小（以 Canvas 单位计）
        if (_rootCanvas != null)
        {
            var rootRect = _rootCanvas.rootCanvas != null ? _rootCanvas.rootCanvas.GetComponent<RectTransform>() : _rootCanvas.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                return Mathf.Abs(rootRect.rect.height);
            }
        }

        // 回退：使用屏幕像素高度（这在大多数 Screen Space Canvas 中相当接近）
        return Screen.height;
    }

    /// <summary>
    /// 显示一条 Toast。
    /// </summary>
    /// <param name="message">要显示的消息</param>
    /// <param name="icon">可选的图标</param>
    /// <param name="duration">显示时长（秒），-1表示使用默认时长</param>
    public void ShowToast(string message, Sprite icon = null, float duration = -1f)
    {
        if (toastPrefab == null)
        {
            toastPrefab = AddressableCache.Load<GameObject>("Toast");
            if (toastPrefab == null)
            {
                Debug.LogWarning("ToastManager: toastPrefab 未设置！");
                return;
            }
        }
        if (string.IsNullOrEmpty(message)) return;

        // 如果达到容量上限，强制移除最旧的一个
        if (_activeToasts.Count >= maxToasts)
        {
            var oldestToast = _activeToasts[0];
            if (oldestToast != null)
            {
                try
                {
                    oldestToast.ForceDismiss(); // ForceDismiss 会触发 OnDismissed 回调，从而从列表中移除
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"ToastManager: ForceDismiss 出现异常: {ex.Message}");
                    // 尝试直接移除并销毁以释放位置
                    _activeToasts.RemoveAt(0);
                    if (oldestToast != null && oldestToast.gameObject != null)
                        Destroy(oldestToast.gameObject);
                }
            }
        }

        // 实例化并设置Toast
        GameObject go = null;
        Toast newToast = null;
        try
        {
            go = Instantiate(toastPrefab, transform);
            if (go == null)
            {
                Debug.LogWarning("ToastManager: 实例化 toastPrefab 返回 null");
                return;
            }

            newToast = go.GetComponent<Toast>();
            if (newToast == null)
            {
                Debug.LogWarning("ToastManager: toastPrefab 上未找到 Toast 组件，销毁实例");
                Destroy(go);
                return;
            }

            newToast.Setup(message, icon, duration > 0 ? duration : defaultDuration);
            newToast.OnDismissed += OnToastDismissed;
            _activeToasts.Add(newToast);

            // 重新计算所有Toast的位置并播放动画
            RepositionToasts();
        }
        catch (Exception ex)
        {
            Debug.LogError($"ToastManager: ShowToast 异常: {ex.Message}");
            if (newToast != null && newToast.gameObject != null) Destroy(newToast.gameObject);
            if (go != null) Destroy(go);
        }
    }

    private void OnToastDismissed(Toast toast)
    {
        if (toast != null)
        {
            toast.OnDismissed -= OnToastDismissed;
            _activeToasts.Remove(toast);
        }
        // 移除一个后，重新排列剩余的
        RepositionToasts();
    }

    /// <summary>
    /// 重新计算并更新所有活动Toast的位置。
    /// </summary>
    private void RepositionToasts()
    {
        float currentY = -topPadding;
        
        // 从上到下遍历所有活动的Toast
        for (int i = 0; i < _activeToasts.Count; i++)
        {
            Toast toast = _activeToasts[i];
            if (toast == null) continue;

            RectTransform toastRect = toast.GetComponent<RectTransform>();
            if (toastRect == null) continue;

            Vector2 targetPos = new Vector2(0, currentY);

            // 如果是最新添加的Toast，则从一个偏移位置开始播��“Init”动画
            if (i == _activeToasts.Count - 1 && toast.IsNew)
            {
                toast.Show(targetPos + startOffset, targetPos);
            }
            else // 否则，只是平滑移动到新的目标位置
            {
                toast.MoveTo(targetPos);
            }

            // 为下一个Toast累加当前Toast的高度和间距
            currentY -= toastRect.rect.height + spacing;
        }

    }
}
