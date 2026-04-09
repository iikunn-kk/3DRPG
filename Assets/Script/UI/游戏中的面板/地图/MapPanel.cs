using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class MapPanel : MonoBehaviour
{
    [Header("数据配置")][SerializeField] private MapDataSO mapData;
    [Header("图标父节点(直接放置好的 MapRegionIcon 们)")][SerializeField] private RectTransform iconContainer;
    [Header("连接线端点(按顺序放置 MapRegionIcon)")][SerializeField] private List<MapRegionIcon> connectionPoints = new List<MapRegionIcon>();
    [Header("Ui相机")][SerializeField] private Camera uiCamera;
    [SerializeField] private Button closeButton;

    private readonly List<MapRegionIcon> _icons = new List<MapRegionIcon>();
    private MapRegionEntry _selected;
    private MapRegionIcon _selectedIcon;
    private string _currentScene;

    private void OnEnable()
    {
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(Close);
        var mainCameraData = Camera.main.GetUniversalAdditionalCameraData();
        if (mainCameraData)
        {
            mainCameraData.cameraStack.Add(uiCamera);
        }
    }

    protected void OnDisable()
    {
        var mainCameraData = Camera.main?.GetUniversalAdditionalCameraData();
        if (mainCameraData)
        {
            mainCameraData.cameraStack.Remove(uiCamera);
        }
    }

    /// <summary>
    /// 外部打开后调用一次初始化（只构建一次，除非显式再次调用 Init）。
    /// </summary>
    public void Init()
    {
        gameObject.transform.SetParent(null);
        UIManager.Instance.GetMainCanvas().OnMapPanelShow();
        _currentScene = SceneManager.GetActiveScene().name;
        BuildIcons();
        RefreshIconStates();
        AudioManager.Instance.PlayUISound(UISoundType.打开地图);
    }

    private void BuildIcons()
    {
        _icons.Clear();
        if (mapData == null || mapData.regions == null) return;
        if (connectionPoints == null || connectionPoints.Count == 0)
        {
            Debug.LogWarning("[MapPanel] connectionPoints 为空，无法按顺序初始化图标。");
            return;
        }
        int count = Mathf.Min(connectionPoints.Count, mapData.regions.Count);
        if (connectionPoints.Count != mapData.regions.Count)
        {
            Debug.LogWarning($"[MapPanel] connectionPoints 数量({connectionPoints.Count}) 与 regions 数量({mapData.regions.Count}) 不匹配，仅初始化前 {count} 个。");
        }
        _currentScene = SceneManager.GetActiveScene().name;
        for (int i = 0; i < count; i++)
        {
            var icon = connectionPoints[i]; // 预制图标位置
            var entry = mapData.regions[i]; // 从MapDataSO获取配置
            if (icon == null || entry == null)
            {
                Debug.LogWarning($"[MapPanel] 第 {i} 个 icon 或 entry 为空，跳过。");
                continue;
            }
            _icons.Add(icon);
            bool isTracked = false;
            if (TaskTrackingService.Instance != null)
            {
                // 检查是否有任务追踪
                TaskTrackingService.Instance.SceneHasAnyObjective(entry.sceneName, out var hasTrackedObj);
                isTracked = hasTrackedObj; // 仅显示被追踪场景
            }
            // 是否当前所在区域
            bool isCurrent = entry.sceneName == _currentScene;
            // 设置图标数据
            icon.SetData(entry, isTracked, OnRegionClicked, isCurrent);
        }
    }

    //刷新Icon状态
    private void RefreshIconStates()
    {
        if (mapData == null || mapData.regions == null) return;
        _currentScene = SceneManager.GetActiveScene().name; // 刷新时再获取一次
        int count = Mathf.Min(_icons.Count, mapData.regions.Count);
        for (int i = 0; i < count; i++)
        {
            var icon = _icons[i];
            var entry = mapData.regions[i];
            if (icon == null || entry == null) continue;
            bool isTracked = false;
            if (TaskTrackingService.Instance != null)
            {
                TaskTrackingService.Instance.SceneHasAnyObjective(entry.sceneName, out var hasTrackedObj);
                isTracked = hasTrackedObj;
            }
            icon.UpdateTracked(isTracked);
            icon.SetCurrent(entry.sceneName == _currentScene);
        }
    }


    // New signature: isDoubleClick == true indicates a double click
    private void OnRegionClicked(MapRegionEntry entry, MapRegionIcon icon, bool isDoubleClick)
    {
        if (entry == null) return;

        if (isDoubleClick)
        {
            if (entry.sceneName == _currentScene)
            {
                UIManager.Instance?.ShowToast("已经在该区域");
                return;
            }
            UIManager.Instance.ClosePanel<MapPanel>();
            SceneLoadManager.Instance?.TeleportToScene(entry.sceneName);
            return;
        }
        // Single click toggle
        if (_selectedIcon == icon && _selectedIcon != null && _selectedIcon.Selected)
        {
            _selectedIcon.SetSelected(false);
            _selectedIcon = null;
            _selected = null;
            return;
        }
        if (_selectedIcon != null) _selectedIcon.SetSelected(false);
        _selectedIcon = icon;
        if (_selectedIcon != null) _selectedIcon.SetSelected(true);
        _selected = entry;
    }

    public void Close()
    {
        UIManager.Instance.ClosePanel<MapPanel>();
        UIManager.Instance.GetMainCanvas().OnMapPanelHide();
    }

}
