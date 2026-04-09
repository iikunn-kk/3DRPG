using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class HotkeySettingsPanel : MonoBehaviour
{
    [Header("要重绑定的槽位动作（与快捷栏槽位一一对应，长度=5）")] public InputActionReference[] slotActions;
    [Header("提示/确认弹窗（用于按键捕获）")] public RebindPromptUI promptUI;
    public VoidEventSO onHotkeyChanged;

    [Header("面板UI（右侧5个按钮与其文本）")] [SerializeField] private Button[] slotRebindButtons; // 长度=5
    [SerializeField] private TMP_Text[] slotKeyLabels;   // 长度=5，对应显示当前绑定
    [SerializeField] private string unsetPlaceholder = "未设置";

    [Header("映射与来源")]
    [Tooltip("若已在场景中放置快捷栏，则自动读取其 ReverseBindingOrder 以保持顺序一致")] 
    [SerializeField] private SkillQuickButtonBar quickbar; // 可选：自动探测
    [SerializeField] private bool followQuickbarReverse = true; // 开启后按快捷栏的反向设定进行映射

    [Header("调试")]
    [SerializeField] private bool debugRebindLogs = true;

    private bool _uiWired;

    private void OnEnable()
    {
        // 自动探测快捷栏（若未手动关联）
        if (quickbar == null)
        {
            try
            {
#if UNITY_2023_1_OR_NEWER
                var bars = UnityEngine.Object.FindObjectsByType<SkillQuickButtonBar>(FindObjectsSortMode.None);
#else
                var bars = UnityEngine.Object.FindObjectsOfType<SkillQuickButtonBar>(true);
#endif
                if (bars != null && bars.Length > 0) quickbar = bars[0];
            }
            catch { /* 忽略 */ }
        }

        if (debugRebindLogs)
        {
            string src = quickbar != null ? (quickbar.ReverseBindingOrder ? "反向" : "正常") : "未找到快捷栏(默认正常)";
            Debug.Log($"[热键] 设置面板启用：映射={src}");
        }
        WireUI();
        LoadBindings();
        RefreshQuickBarLabels();
        RefreshPanelLabels();
    }
    private void OnDisable()
    {
        // 若捕获中交由 RebindPromptUI 的 OnDisable 处理
        if (debugRebindLogs) Debug.Log("[热键] 设置面板禁用");
    }

    public void Show() { gameObject.SetActive(true); if (debugRebindLogs) Debug.Log("[热键] 打开设置面板"); RefreshPanelLabels(); }
    public void Hide() { if (debugRebindLogs) Debug.Log("[热键] 关闭设置面板"); gameObject.SetActive(false); }

    private void WireUI()
    {
        if (_uiWired) return;
        if (slotRebindButtons != null)
        {
            for (int i = 0; i < slotRebindButtons.Length; i++)
            {
                int uiIndex = i; // 面板按钮的可视顺序
                var btn = slotRebindButtons[i];
                if (btn == null) continue;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => StartRebindSlot(uiIndex));
            }
        }
        _uiWired = true;
        if (debugRebindLogs) Debug.Log("[热键] 面板UI已布线");
    }

    // === 入口：点击槽位按钮（参数为面板按钮索引，而非动作索引） ===
    public void StartRebindSlot(int panelIndex)
    {
        int actionIndex = PanelIndexToActionIndex(panelIndex);
        if (slotActions == null || actionIndex < 0 || actionIndex >= slotActions.Length) return;
        var action = slotActions[actionIndex]?.action; if (action == null) return;
        // 启动捕获单键
        if (promptUI != null)
        {
            promptUI.StartCaptureSingleKey( path =>
            {
                if (string.IsNullOrEmpty(path)) // 取消或未捕获
                {
                    if (debugRebindLogs) Debug.Log("[热键] 重绑已取消或未捕获按键");
                    return;
                }
                ApplySingleBinding(action, path, panelIndex, actionIndex);
            });
        }
        else
        {
            Debug.LogWarning("[热键] 未分配 RebindPromptUI，无法捕获按键");
        }
    }

    private void ApplySingleBinding(InputAction action, string controlPath, int panelIndex, int actionIndex)
    {
        if (debugRebindLogs) Debug.Log($"[热键] 应用单键绑定 动作={action.name} 路径={controlPath} (面板槽位={panelIndex} -> 动作索引={actionIndex})");
        // 先清空可能存在的组合键覆盖，确保只剩单键
        KeybindingStorage.SetCompositePaths(action, null, null);
        KeybindingStorage.SetSingleBindingPath(action, controlPath);
        SaveBindings();
        RefreshQuickBarLabels(); // 修改后立即刷新快捷栏标签
        RefreshPanelLabels();
    }

    public void ResetBindingSlot(int panelIndex)
    {
        int actionIndex = PanelIndexToActionIndex(panelIndex);
        if (slotActions == null || actionIndex < 0 || actionIndex >= slotActions.Length) return;
        var action = slotActions[actionIndex]?.action; if (action == null) return;
        if (debugRebindLogs) Debug.Log($"[热键] 重置单个绑定 面板槽位={panelIndex} -> 动作索引={actionIndex} 动作={action.name}");
        action.RemoveAllBindingOverrides();
        SaveBindings();
        RefreshQuickBarLabels();
        RefreshPanelLabels();
    }

    public void ResetAllBindings()
    {
        if (debugRebindLogs) Debug.Log("[热键] 重置所有绑定");
        KeybindingStorage.ClearOverrides(slotActions);
        SaveBindings();
        RefreshQuickBarLabels();
        RefreshPanelLabels();
    }

    public void SaveBindings()
    {
        // 保存覆盖到 PlayerPrefs
        KeybindingStorage.SaveOverrides(slotActions);
        if (debugRebindLogs) Debug.Log("[热键] 已保存覆盖到本地");
        // 立即将保存的覆盖同步应用到当前场景中所有 PlayerInput 的运行时实例
        InputBindingRuntimeSync.ApplySavedOverridesToAllPlayersFor(slotActions);
        if (debugRebindLogs) Debug.Log("[热键] 已同步覆盖到所有 PlayerInput");
        // 同步本地引用的资产实例（以便面板/标签显示立刻一致）
        KeybindingStorage.LoadOverrides(slotActions);
    }
    public void LoadBindings() => KeybindingStorage.LoadOverrides(slotActions);

    private void RefreshQuickBarLabels()
    {
        // 方法1: 通过 ScriptableObject 事件广播
        if (onHotkeyChanged != null)
        {
            try
            {
                onHotkeyChanged.Raise(this);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[热键] 事件广播失败: {ex.Message}");
            }
        }
        
        // 方法2: 直接查找并刷新所有快捷栏（兜底机制）
        try
        {
#if UNITY_2023_1_OR_NEWER
            var bars = UnityEngine.Object.FindObjectsByType<SkillQuickButtonBar>(FindObjectsSortMode.None);
#else
            var bars = UnityEngine.Object.FindObjectsOfType<SkillQuickButtonBar>(true);
#endif
            foreach (var bar in bars)
            {
                if (bar == null) continue;
                bar.RefreshHotkeyLabels();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[热键] 刷新快捷栏失败: {ex.Message}");
        }
    }

    private void RefreshPanelLabels()
    {
        if (slotKeyLabels == null || slotActions == null) return;
        int count = Mathf.Min(slotKeyLabels.Length, slotActions.Length);
        for (int i = 0; i < count; i++)
        {
            var label = slotKeyLabels[i]; if (label == null) continue;
            int actionIndex = PanelIndexToActionIndex(i);
            label.text = BuildDisplayString(slotActions[actionIndex]);
        }
        if (debugRebindLogs) Debug.Log("[热键] 设置面板标签已刷新");
    }

    private string BuildDisplayString(InputActionReference actionRef)
    {
        if (actionRef == null || actionRef.action == null) return unsetPlaceholder;
        var cfg = KeybindingStorage.ReadCurrentConfig(actionRef.action);
        var s = cfg.singlePath;
        if (!string.IsNullOrEmpty(s))
        {
            try
            {
                return InputControlPath.ToHumanReadableString(s, InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames);
            }
            catch { return unsetPlaceholder; }
        }
        try { return actionRef.action.GetBindingDisplayString(); } catch { }
        return unsetPlaceholder;
    }

    // === 索引映射：面板按钮索引 -> 动作数组索引 ===
    private int PanelIndexToActionIndex(int panelIndex)
    {
        if (slotActions == null || slotActions.Length == 0) return panelIndex;
        int count = slotActions.Length;
        bool reverse = followQuickbarReverse && quickbar != null ? quickbar.ReverseBindingOrder : false;
        int actionIndex = reverse ? (count - 1 - panelIndex) : panelIndex;
        // 越界保护
        if (actionIndex < 0 || actionIndex >= count) return panelIndex;
        return actionIndex;
    }
}
