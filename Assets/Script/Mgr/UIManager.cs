using UnityEngine;
using System.Collections.Generic;

public class UIManager : Singleton<UIManager>
{
    private Transform _mainCanvasMakePanel;
    private MainCanvas _mainCanvas;

    // 存储已打开的面板实例
    private Dictionary<string, Object> _openedPanels = new Dictionary<string, Object>();
    [SerializeField] private BoolEventSO cameraRotationActiveEvent;

    // Toast 管理器引用
    private ToastManager _toastManager;
    public bool isOpenedPanel { get; private set; }

    /// <summary>
    /// 设置主画布引用，并同步内部制作面板 Transform 与吐司提示管理器的引用。
    /// </summary>
    /// <param name="mainCanvas">主画布对象，需包含制作面板 Transform 与吐司管理器。</param>
    /// <remarks>
    /// 调用后将更新内部对主画布、制作面板与吐司管理器的缓存引用；
    /// 在使用依赖主画布的 UI 功能前应先调用此方法完成初始化。
    /// </remarks>
    public void SetMainCanvas(MainCanvas mainCanvas)
    {
        _mainCanvas = mainCanvas;
        _mainCanvasMakePanel = _mainCanvas.MakePanelTransform;
        _toastManager = _mainCanvas.toastManager;
    }
    public T OpenPanel<T>(out bool isOpen) where T : Object
    {
        string panelName = typeof(T).Name;

        // 检查面板是否已经打开
        if (_openedPanels.ContainsKey(panelName))
        {
            // 如果面板已经打开，检查对象是否仍然有效
            T existingPanel = _openedPanels[panelName] as T;
            if (existingPanel != null)
            {
                // 如果面板仍然存在，直接返回已存在的实例
                isOpen = false;
                return existingPanel;
            }
            else
            {
                // 面板已被销毁，从字典中移除
                _openedPanels.Remove(panelName);
            }
        }

        // 加载并实例化面板
        var panel = AddressableCache.Load<T>(panelName);
        if (panel == null)
        {
            Debug.LogError($"Panel {panelName} not found in Addressables");
            isOpen = false;
            return null;
        }

        T instantiatedPanel = Instantiate(panel, _mainCanvasMakePanel);

        // 将新打开的面板添加到字典中
        _openedPanels.Add(panelName, instantiatedPanel);
        isOpen = true;
        cameraRotationActiveEvent.RaiseEvent(false, true);
        isOpenedPanel = true;
        return instantiatedPanel;
    }

    // 关闭面板时调用此方法从已打开面板列表中移除
    public void ClosePanel<T>() where T : Object
    {
        string panelName = typeof(T).Name;
        _openedPanels.Remove(panelName);
        // 更新总状态：还有其它面板则保持 true
        isOpenedPanel = _openedPanels.Count > 0;
        if (!isOpenedPanel)
        {
            // 仅当全部关闭时才恢复摄像机旋转
            cameraRotationActiveEvent.RaiseEvent(true, true);
        }
    }

    // 检查面板是否已经打开
    public bool IsPanelOpen<T>() where T : Object
    {
        return _openedPanels.ContainsKey(typeof(T).Name);
    }

    // 通过 UIManager 暴露的便捷 API：显示 Toast
    public void ShowToast(string message, Sprite icon = null, float duration = -1f)
    {
        if (_toastManager == null)
        {
            // 尝试再次查找或创建（以防 Awake 时未找到 MainCanvas）
            if (_mainCanvasMakePanel != null)
            {
                _toastManager = _mainCanvasMakePanel.GetComponentInChildren<ToastManager>();
                if (_toastManager == null)
                {
                    GameObject go = new GameObject("ToastManager");
                    go.transform.SetParent(_mainCanvasMakePanel, false);
                    var rt = go.AddComponent<RectTransform>();
                    // 放在顶部中间，向上叠加（用于中上方出现并向上移动）
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(400f, 400f);
                    _toastManager = go.AddComponent<ToastManager>();
                }
            }
            else
            {
                Debug.LogWarning("MainCanvasMakePanel is null, cannot show toast.");
                return;
            }
        }

        _toastManager.ShowToast(message, icon, duration);
    }

    // ============ 技能专用 Toast API（通过MainCanvas间接调用）===========
    public void ShowSkillToast(string message, float duration = -1f)
    {
        if (_mainCanvas != null)
        {
            _mainCanvas.ShowSkillToast(message, duration);
        }
        else
        {
            Debug.LogWarning("MainCanvas is null, cannot show skill toast.");
        }
    }

    // 便捷重载：仅传 message（便于 UnityEvent<string> 直接绑定）
    public void ShowSkillToast(string message)
    {
        ShowSkillToast(message, -1f);
    }

    public void HideSkillToast()
    {
        if (_mainCanvas != null)
        {
            _mainCanvas.HideSkillToast();
        }
    }
    public MainCanvas GetMainCanvas()
    {
        if (_mainCanvas == null)
        {
            _mainCanvas = FindFirstObjectByType<MainCanvas>();
            if (_mainCanvas == null)
            {
                var go = GameObject.FindGameObjectWithTag("MainCanvas");
                if (go) _mainCanvas = go.GetComponent<MainCanvas>();
            }
        }
        return _mainCanvas;
    }
}