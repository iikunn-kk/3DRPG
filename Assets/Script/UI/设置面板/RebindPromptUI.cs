using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// 一个简单的确认/提示/按键捕获弹窗。
/// 新增：单键捕获功能（仅键盘）。
/// - StartCaptureSingleKey 会显示提示并开始监听具体按键
/// - 玩家多次按键时以最后一次为准
/// - nowKeyText 实时显示最新捕获的按键（人类可读）
/// - 确认：返回最后捕获的路径；取消或未捕获返回 null
/// </summary>
public class RebindPromptUI : UIPopPanelBase
{
    // 用于显示当前捕获到的按键的文本（例如显示“A”）
    public TMP_Text nowKeyText;

    [Header("按钮")] 
    // 确认按钮（当用户确认当前捕获按键时触发）
    public Button confirmButton; 
    // 取消按钮（取消捕获并关闭弹窗）
    public Button cancelButton;

    // 外部注册的标准确认/取消回调（Init 会设置）
    private Action _onConfirm; 
    private Action _onCancel;

    // 捕获相关私有字段
    // 最后一次捕获到的 control.path，例如 "<Keyboard>/a"，玩家确认时会把这个路径返回
    private string _lastCapturedPath;   // 最后一次捕获的 control.path
    // 捕获流程结束时（确认/取消/隐藏）调用的回调，参数为最终的 control.path（或 null）
    private Action<string> _onCaptureFinished; // 回调：结束时传出路径
    // 标记当前面板是否处于捕获状态；用于避免多次无效触发
    private bool _capturing;

    /// <summary>
    /// 初始化确认/取消按钮行为并显示面板。
    /// - onConfirm/onCancel 是纯 UI 的回调（不含按键捕获返回值），用于普通确认/取消场景
    /// - 注意：若你在 StartCaptureSingleKey 中调用 Init，会将 confirm/cancel 的行为替换为结束捕获并传回结果
    /// </summary>
    public void Init(Action onConfirm, Action onCancel)
    {
        _onConfirm = onConfirm; _onCancel = onCancel;
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() =>
            {
                // 点击确认：隐藏面板并执行传入的确认回调
                Hide(false);
                var cb = _onConfirm; _onConfirm = null; _onCancel = null;
                cb?.Invoke();
            });
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() =>
            {
                // 点击取消：隐藏面板并执行传入的取消回调
                Hide(false);
                var cb = _onCancel; _onConfirm = null; _onCancel = null;
                cb?.Invoke();
            });
        }
        // 最后显示面板（基类负责动画/遮罩等）
        Show();
    }
    
    // ===== 单键捕获 =====
    /// <summary>
    /// 打开弹窗并开始捕获单个按键（仅键盘）。
    /// onFinished 会在捕获流程结束时被调用：参数为按键的 control.path（例如 "<Keyboard>/a"），取消或未捕获时为 null。
    /// 流程：
    /// 1. 停止任何旧的捕获
    /// 2. 初始化 UI（显示未捕获、禁用确认）
    /// 3. 将 confirm/cancel 按钮的行为替换为结束捕获并回传结果
    /// 4. 开始轮询键盘输入
    /// </summary>
    public void StartCaptureSingleKey(Action<string> onFinished)
    {
        // 先停止旧捕获（如果存在），并保证不会重复回调
        StopCaptureInternal(null);
        _capturing = true;
        _lastCapturedPath = null;
        _onCaptureFinished = onFinished;

        // 更新 UI 显示，并禁止确认按钮直到捕获到按键
        if (nowKeyText) nowKeyText.text = "(未捕获)";
        if (confirmButton) confirmButton.interactable = false; // 未捕获前不允许确认

        // 设置取消按钮显示（视具体 UI 需求）
        if (cancelButton != null) cancelButton.gameObject.SetActive(true);

        // 直接绑定确认/取消按钮，避免先 Hide 再回调导致 OnDisable 把结果置为 null 的问题
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() =>
            {
                // 确认：使用最后一次捕获（如果为 null 则等同取消）
                StopCaptureInternal(_lastCapturedPath);
            });
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() =>
            {
                // 取消：显式返回 null
                StopCaptureInternal(null);
            });
        }

        // 显示面板
        Show();
    }

    // 每帧轮询键盘，捕获本帧按下的具体键（最后一次为准）
    private void Update()
    {
        if (!_capturing) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        // 遍历所有物理键，记录最后一个在本帧被按下的键
        KeyControl lastPressed = null;
        var keys = kb.allKeys; // 包含 A、B、Space、Digit1 等具体键
        for (int i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            if (key == null) continue; // 额外 null 检查，避免潜在的 null 引用
            if (key.wasPressedThisFrame)
            {
                lastPressed = key; // 以最后一次为准
            }
        }

        if (lastPressed != null)
        {
            var path = lastPressed.path; // e.g. "<Keyboard>/a"
            _lastCapturedPath = path;
            if (nowKeyText)
            {
                // 将 control.path 转为友好显示（例如 A、Space 等）
                nowKeyText.text = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames);
            }
            if (confirmButton) confirmButton.interactable = true; // 捕获到后允许确认
        }
    }

    /// <summary>
    /// 内部通用的结束捕获流程（确认或取消都会调用）。
    /// - 关闭面板
    /// - 调用 _onCaptureFinished 回调并传出最终值（可能为 null）
    /// </summary>
    private void StopCaptureInternal(string resultPath)
    {
        bool wasCapturing = _capturing;
        _capturing = false;
        if (wasCapturing)
        {
            // 仅在真正处于捕获流程中时才隐藏，避免首次 StartCaptureSingleKey 时不必要的 Hide 调用
            Hide(false);
            var cb = _onCaptureFinished; _onCaptureFinished = null;
            cb?.Invoke(resultPath);
        }
        else
        {
            // 即使未处于捕获流程，也保证 UI 状态一致
            Hide(false);
        }
    }

    /// <summary>
    /// 当脚本/对象被禁用（例如切换 UI、场景切换等）时，确保捕获流程被正确终止并回调 null。
    /// 这避免在面板被外部隐藏时留下未释放的 InputAction 或者丢失回调。
    /// </summary>
    protected override void OnDisable()
    {
        base.OnDisable();
        // 如果面板被外部隐藏，确保释放资源并通知回调(null)
        if (_capturing)
        {
            StopCaptureInternal(null);
        }
    }
}
