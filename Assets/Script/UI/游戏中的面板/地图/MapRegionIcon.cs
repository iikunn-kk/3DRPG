using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

public class MapRegionIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    #region 序列化字段
    [Header("图标图像")]
    [SerializeField] private Image iconImage;
    [Header("名称文本")]
    [SerializeField] private TMP_Text nameText;
    [Header("追踪标记")]
    [SerializeField] private GameObject trackedMarker; // 仅显示当前追踪的区域
    [Header("按钮")]
    [SerializeField] private Button button; // 点击按钮
    [Header("悬停高亮")]
    [SerializeField] private GameObject hoverHighlight; // 鼠标划过高亮
    [Header("选中高亮")]
    [SerializeField] private GameObject selectHighlight; // 选中高亮
    [Header("当前场景标记")]
    [SerializeField] private GameObject currentMarker; // 当前所在场景高亮
    #endregion

    #region 私有字段
    private MapRegionEntry _data;
    private System.Action<MapRegionEntry, MapRegionIcon, bool> _onClick; // (entry, icon, isDoubleClick)
    private bool _isTracked;
    private bool _selected;
    private bool _isCurrent;
    private Coroutine _clickRoutine;
    private const float DoubleClickThreshold = 0.35f; // 双击判定时间
    #endregion

    #region 公共属性
    public MapRegionEntry Data => _data;
    public bool Selected => _selected;
    public bool IsCurrent => _isCurrent;
    #endregion

    #region 公共方法
    // 初始化：仅需要是否追踪、是否当前场景
    public void SetData(MapRegionEntry entry, bool isTracked, System.Action<MapRegionEntry, MapRegionIcon, bool> onClick, bool isCurrent = false)
    {
        _data = entry;
        _onClick = onClick;
        _isTracked = isTracked;
        _selected = false;
        _isCurrent = isCurrent;

        if (iconImage != null) iconImage.sprite = entry != null ? entry.icon : null;
        if (nameText != null) nameText.text = entry != null ? entry.displayName : string.Empty;
        if (trackedMarker != null) trackedMarker.SetActive(_isTracked);
        if (currentMarker != null) currentMarker.SetActive(_isCurrent);

        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClicked);
            button.onClick.AddListener(OnButtonClicked);
        }
        if (hoverHighlight) hoverHighlight.SetActive(false);
        if (selectHighlight) selectHighlight.SetActive(false);
    }

    public void UpdateTracked(bool isTracked)
    {
        _isTracked = isTracked;
        if (trackedMarker != null) trackedMarker.SetActive(_isTracked);
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (selectHighlight) selectHighlight.SetActive(selected);
        if (!selected && hoverHighlight) hoverHighlight.SetActive(false);
    }

    public void SetCurrent(bool isCurrent)
    {
        _isCurrent = isCurrent;
        if (currentMarker) currentMarker.SetActive(isCurrent);
    }
    #endregion

    #region 私有方法
    private void OnButtonClicked()
    {
        if (_clickRoutine != null)
        {
            StopCoroutine(_clickRoutine);
            _clickRoutine = null;
            _onClick?.Invoke(_data, this, true); // 双击
        }
        else
        {
            _clickRoutine = StartCoroutine(ClickRoutine()); // 等待可能的第二次点击
        }
    }

    private IEnumerator ClickRoutine()
    {
        yield return new WaitForSeconds(DoubleClickThreshold);
        _clickRoutine = null;
        _onClick?.Invoke(_data, this, false); // 确认单击
    }
    #endregion

    #region 事件处理程序
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverHighlight && !_selected) hoverHighlight.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverHighlight && !_selected) hoverHighlight.SetActive(false);
    }

    private void OnDisable()
    {
        if (_clickRoutine != null)
        {
            StopCoroutine(_clickRoutine);
            _clickRoutine = null;
        }
    }
    #endregion
}