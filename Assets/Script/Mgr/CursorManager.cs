using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 控制鼠标光标的显示/隐藏与锁定状态。
/// 核心逻辑：程序只有两种状态
/// 1. UI控制模式：光标显示、不锁定，镜头锁定。
/// 2. 游戏控制模式：光标隐藏、锁定，镜头不锁定。
/// 触发条件：按下指定按键、UI面板打开、游戏窗口失焦时，进入“UI控制模式”，否则进入“游戏控制模式”。
/// </summary>
public class CursorManager : MonoBehaviour
{
    [Header("Input (New Input System)")]
    [Tooltip("按住此动作对应按键时显示鼠标（Action 要求类型为 Button）。可留空使用 defaultKey 作为后备。")]
    [SerializeField] private InputActionReference holdToShowCursorAction;

    [Tooltip("当未提供 InputActionReference 时使用的后备按键")]
    [SerializeField] private Key defaultKey = Key.LeftAlt;

    [Header("事件广播")]
    [Tooltip("当需要锁定相机时广播 true, 解锁时广播 false")]
    [SerializeField] private BoolEventSO cameraLockEvent;

    [Header("调试 / 状态只读")]
    [SerializeField, ReadOnlyInspector] private bool isHoldKeyActive; // 调试用：是否正按住按键
    [SerializeField, ReadOnlyInspector] private bool isPanelOpen;     // 调试用：是否有UI面板打开
    [SerializeField, ReadOnlyInspector] private bool isCursorVisible; // 调试用：当前光标是否可见/处于UI模式

    // 运行期使用的实际动作
    private InputAction _runtimeAction;
    private bool _isActionFromFallback;

    // 用于跟踪核心状态是否发生变化
    private bool _lastIsUiControlMode;

    private void Awake()
    {
        SetupInputAction();
    }

    private void OnEnable()
    {
        if (_runtimeAction != null)
        {
            if (!_runtimeAction.enabled)
                _runtimeAction.Enable();

            _runtimeAction.started += OnActionStarted;
            _runtimeAction.canceled += OnActionCanceled;
        }
        // 首次启用时，强制刷新一次以确保初始状态正确 (例如游戏开始时光标隐藏)
        ForceRefreshCursorState();
    }

    private void OnDisable()
    {
        if (_runtimeAction != null)
        {
            _runtimeAction.started -= OnActionStarted;
            _runtimeAction.canceled -= OnActionCanceled;

            // 如果是临时创建的Action，在禁用时也禁用它
            if (_isActionFromFallback)
            {
                _runtimeAction.Disable();
            }
        }
        // 失活时恢复显示光标，避免在编辑器中丢失鼠标控制
        SetCursorState(true);
    }

    private void OnDestroy()
    {
        // 如果是临时创建的Action，在销毁时彻底释放资源
        if (_isActionFromFallback && _runtimeAction != null)
        {
            _runtimeAction.Dispose();
            _runtimeAction = null;
        }
    }

    private void Update()
    {
        // 每帧检查UI面板状态和窗口焦点状态
        UpdateCursorStateIfNeeded();
    }

    private void SetupInputAction()
    {
        if (holdToShowCursorAction != null && holdToShowCursorAction.action != null)
        {
            _runtimeAction = holdToShowCursorAction.action;
            _isActionFromFallback = false;
        }
        else
        {
            // 如果没有提供InputAction，就根据默认按键创建一个临时的
            _runtimeAction = new InputAction(name: "HoldShowCursor_Fallback", type: InputActionType.Button, binding: $"<Keyboard>/{defaultKey.ToString().ToLowerInvariant()}");
            _isActionFromFallback = true;
        }
    }

    private void OnActionStarted(InputAction.CallbackContext ctx)
    {
        isHoldKeyActive = true;
        UpdateCursorStateIfNeeded();
    }

    private void OnActionCanceled(InputAction.CallbackContext ctx)
    {
        isHoldKeyActive = false;
        UpdateCursorStateIfNeeded();
    }

    /// <summary>
    /// 核心逻辑：检查是否需要更新光标状态，并在需要时统一更新
    /// </summary>
    private void UpdateCursorStateIfNeeded()
    {
        // 检查全局UI面板状态
        isPanelOpen = UIManager.Instance != null && UIManager.Instance.isOpenedPanel;

        // 核心判断：只要满足任一条件，就应该进入“UI控制模式”
        bool isUiControlMode = isHoldKeyActive || isPanelOpen || !Application.isFocused;

        // 只有在“模式”发生切换时，才执行状态更新，避免每帧重复调用API
        if (isUiControlMode != _lastIsUiControlMode)
        {
            SetCursorState(isUiControlMode);
            _lastIsUiControlMode = isUiControlMode;
        }
    }

    /// <summary>
    /// 设置光标状态，这是一个原子操作，保证所有状态同步
    /// </summary>
    /// <param name="isUiMode">是否进入UI模式</param>
    private void SetCursorState(bool isUiMode)
    {
        if (isUiMode)
        {
            // 进入UI模式：显示光标，解除锁定，并通知相机锁定
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            cameraLockEvent?.RaiseEvent(false, this); 
        }
        else
        {
            // 进入游戏模式：隐藏光标，锁定到屏幕中央，并通知相机解锁
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            cameraLockEvent?.RaiseEvent(true, this); 
        }

        // 更新调试信息
        isCursorVisible = isUiMode;
    }

    /// <summary>
    /// 供外部调用，强制立即刷新光标状态
    /// </summary>
    public void ForceRefreshCursorState()
    {
        // 通过将上一帧的状态设置为当前状态的反值，来强制触发一次更新
        _lastIsUiControlMode = !_lastIsUiControlMode;
        UpdateCursorStateIfNeeded();
    }

#if UNITY_EDITOR
    // OnValidate方法不是必需的，但可以保留
    private void OnValidate()
    {
        var _ = isCursorVisible; // 确保调试变量被使用
    }
#endif
}


// ReadOnlyInspectorAttribute 和其 PropertyDrawer 可以保持不变，无需修改
public class ReadOnlyInspectorAttribute : PropertyAttribute { }
#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ReadOnlyInspectorAttribute))]
public class ReadOnlyInspectorDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;
        EditorGUI.PropertyField(position, property, label, true);
        GUI.enabled = true;
    }
}
#endif