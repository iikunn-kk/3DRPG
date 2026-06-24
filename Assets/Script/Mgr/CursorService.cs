using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 鼠标光标服务 — 绞杀 CursorManager 的替代品。
/// 控制光标显示/隐藏与锁定状态，两种模式：UI模式 / 游戏模式。
/// </summary>
public class CursorService : MonoBehaviour
{
    [Header("Input (New Input System)")]
    [SerializeField] private InputActionReference holdToShowCursorAction;
    [SerializeField] private Key defaultKey = Key.LeftAlt;

    [Header("事件广播")]
    [SerializeField] private BoolEventSO cameraLockEvent;

    [Header("调试")]
    [SerializeField, ReadOnlyInspector] private bool isHoldKeyActive;
    [SerializeField, ReadOnlyInspector] private bool isPanelOpen;
    [SerializeField, ReadOnlyInspector] private bool isCursorVisible;

    private InputAction _runtimeAction;
    private bool _isActionFromFallback;
    private bool _lastIsUiControlMode;

    private void Awake() => SetupInputAction();

    private void OnEnable()
    {
        if (_runtimeAction != null)
        {
            if (!_runtimeAction.enabled)
                _runtimeAction.Enable();

            _runtimeAction.started += OnActionStarted;
            _runtimeAction.canceled += OnActionCanceled;
        }
        ForceRefreshCursorState();
    }

    private void OnDisable()
    {
        if (_runtimeAction != null)
        {
            _runtimeAction.started -= OnActionStarted;
            _runtimeAction.canceled -= OnActionCanceled;
            if (_isActionFromFallback)
                _runtimeAction.Disable();
        }
        SetCursorState(true);
    }

    private void OnDestroy()
    {
        if (_isActionFromFallback && _runtimeAction != null)
        {
            _runtimeAction.Dispose();
            _runtimeAction = null;
        }
    }

    private void Update() => UpdateCursorStateIfNeeded();

    private void SetupInputAction()
    {
        if (holdToShowCursorAction != null && holdToShowCursorAction.action != null)
        {
            _runtimeAction = holdToShowCursorAction.action;
            _isActionFromFallback = false;
        }
        else
        {
            _runtimeAction = new InputAction("HoldShowCursor_Fallback",
                InputActionType.Button,
                $"<Keyboard>/{defaultKey.ToString().ToLowerInvariant()}");
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

    private void UpdateCursorStateIfNeeded()
    {
        isPanelOpen = UIManager.Instance != null && UIManager.Instance.isOpenedPanel;
        bool isUiControlMode = isHoldKeyActive || isPanelOpen || !Application.isFocused;

        if (isUiControlMode != _lastIsUiControlMode)
        {
            SetCursorState(isUiControlMode);
            _lastIsUiControlMode = isUiControlMode;
        }
    }

    private void SetCursorState(bool isUiMode)
    {
        if (isUiMode)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            cameraLockEvent?.RaiseEvent(false, this);
        }
        else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            cameraLockEvent?.RaiseEvent(true, this);
        }
        isCursorVisible = isUiMode;
    }

    public void ForceRefreshCursorState()
    {
        _lastIsUiControlMode = !_lastIsUiControlMode;
        UpdateCursorStateIfNeeded();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var _ = isCursorVisible;
    }
#endif
}
