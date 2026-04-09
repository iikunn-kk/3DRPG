// 文件名: DragVisualPrefab.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 拖拽视觉预制体
/// 用于在拖拽操作期间显示跟随鼠标的视觉元素
/// </summary>
public class DragVisualPrefab : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private Image bkImage; // 背景用于显示品质颜色
    [SerializeField] private RectTransform _rectTransform;
    private Canvas _parentCanvas;
    
    private void Update()
    {
        // 每帧更新位置以跟随鼠标
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (_rectTransform == null) return; // 没有 RectTransform 无法定位

        // 如果尚未设置父 Canvas，则在层级中查找
        if (_parentCanvas == null)
        {
            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas == null) return;
        }

        Vector2 localPoint;
        // 根据父 Canvas 的类型选择合适的摄像机（ScreenSpaceOverlay 不需要摄像机）
        var canvasRect = _parentCanvas.transform as RectTransform;
        var cam = _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _parentCanvas.worldCamera;

        // 获取屏幕位置
        Vector2 screenPoint = Mouse.current.position.ReadValue();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPoint,
            cam,
            out localPoint
        );

        // 使用 anchoredPosition 来在 Canvas 的 RectTransform 中正确放置 UI 元素
        _rectTransform.anchoredPosition = localPoint;
    }

    /// <summary>
    /// 设置精灵图像
    /// </summary>
    public void SetSprite(Sprite sprite)
    {
        if (_image != null && sprite != null)
        {
            _image.sprite = sprite;
            _image.enabled = true; // 设置了图片后再显示
        }
    }

    /// <summary>
    /// 设置品质背景颜色
    /// </summary>
    public void SetBackgroundColor(Color color)
    {
        if (bkImage != null)
        {
            bkImage.color = color;
        }
    }

    /// <summary>
    /// 对外初始化方法，用于显式设置该视觉元素应使用的父 Canvas。
    /// 在 Instantiate 之后立即调用以确保坐标系一致。
    /// </summary>
    public void Initialize(Canvas parentCanvas)
    {
        if (parentCanvas != null)
            _parentCanvas = parentCanvas;

        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();

        if (_image == null)
            _image = GetComponent<Image>();

        if (_image != null)
            _image.raycastTarget = false;
    }
}