using TMPro;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 任务追踪方向箭头与距离显示：
/// 放在一个始终存在的 UI 节点下（例如 Overlay Canvas 内）。
/// 提供：指向当前追踪任务的目标（或跨场景传送点），进入范围后显示“已到达”并隐藏箭头主体。
/// </summary>
public class TaskArrowUI : MonoBehaviour
{
    [Header("引用")][SerializeField] private RectTransform arrowRect; // 箭头图片 Rect
    [SerializeField] private TMP_Text distanceText; // 距离或提示
    [SerializeField] private CanvasGroup canvasGroup; // 整体显隐
    [SerializeField] private TMP_Text taskNameText; // 任务名称显示

    [Header("罗盘模式")][SerializeField] private bool useCompassMode = true; // 始终固定在UI上，仅旋转
    [Tooltip("忽略高度差，仅在水平面计算方向")][SerializeField] private bool flattenY = true;
    [Tooltip("不同场景时在距离后面追加场景名")][SerializeField] private bool appendSceneNameWhenDifferent = true;
    [Tooltip("当目标在范围内时是否隐藏箭头图形")][SerializeField] private bool hideArrowWhenInside = true;

    [Header("显示配置")][SerializeField] private float fadeSpeed = 6f;
    [SerializeField] private bool hideWhenNoTrack = true;
    [Header("最小显示距离(进入范围后不再显示距离)")][SerializeField] private float minDistanceToShow = 0.5f;
    [SerializeField] private float playerMoveThreshold = 1.0f; // 玩家移动阈值（提到1m，避免每帧重复查询）

    // --- 新增动画相关配置 ---
    [Header("箭头过渡动画")]
    [Tooltip("旋转平滑时间（秒），使用 SmoothDampAngle。0.2s 可减少过冲")][SerializeField] private float arrowRotationSmoothTime = 0.2f;
    [Tooltip("箭头最大旋转速度（度/秒），防止目标角度突变时过冲转圈")][SerializeField] private float arrowMaxRotationSpeed = 360f;
    [Tooltip("是否启用缩放过渡（显示/隐藏时平滑缩放）")][SerializeField] private bool animateScaleOnChange = true;
    [Tooltip("缩放平滑时间（秒）")][SerializeField] private float arrowScaleSmoothTime = 0.12f;
    [Tooltip("当目标缩放小于此阈值时认为已隐藏（用于物体激活/禁用判断）")][SerializeField] private float hideScaleThreshold = 0.01f;

    // 新增：将位置查询与每帧视觉更新分离
    [Header("查询与更新频率")]
    [Tooltip("目标世界位置查询间隔（秒）")][SerializeField] private float positionFetchInterval = 0.5f;
    [Tooltip("距离文字更新间隔（秒）")][SerializeField] private float distanceUpdateInterval = 0.5f;
    [Tooltip("当距离变化超过该值（米）时会立即更新距离显示）")][SerializeField] private float distanceChangeThreshold = 1f;

    private float _positionFetchTimer;
    private float _distanceUpdateTimer;
    private Vector3 _lastPlayerPos;
    private string _lastText;
    private float _lastCachedDistanceSqr = -1f;

    // 缓存目标数据，减少频繁查询 TaskTrackingService
    private Vector3 _cachedTargetPos = Vector3.zero;
    private string _cachedTargetScene = string.Empty;
    private bool _cachedInside = false;
    // store squared distance to avoid calling Mathf.Sqrt every frame
    private float _cachedDistanceSqr = 0f;
    private bool _cachedValid = false;

    // 动画内部状态
    private float _rotationVelocity; // 用于 SmoothDampAngle
    private Vector3 _arrowScaleVelocity = Vector3.zero; // 用于 Vector3.SmoothDamp
    private Vector3 _targetArrowScale = Vector3.one;
    private Vector3 _currentArrowScale = Vector3.one;

    private void Awake()
    {
        // 初始化箭头缩放状态（避免 null 引用）
        if (arrowRect != null)
        {
            _currentArrowScale = arrowRect.localScale;
            if (_currentArrowScale == Vector3.zero)
                _currentArrowScale = Vector3.one;
            _targetArrowScale = _currentArrowScale;
        }
    }

    private void OnEnable()
    {
        TaskEventBridge.Instance.OnTaskTrackedChanged += OnTrackedChanged;
    }
    private void OnDisable()
    {
        TaskEventBridge.Instance.OnTaskTrackedChanged -= OnTrackedChanged;

    }

    private void OnTrackedChanged(int taskId)
    {
        _lastText = null; // 强制下一次刷新文本
        _lastPlayerPos = Vector3.positiveInfinity; // 强制刷新
        // 失效缓存，立刻在下一个位置查询周期获取新目标
        _cachedValid = false;
        _positionFetchTimer = positionFetchInterval; // 触发立即查询
    }

    private void Update()
    {
        if (TaskTrackingService.Instance == null)
        {
            SetVisible(false);
            return;
        }

        var playerState = CharacterRuntimeManager.Instance?.CurrentPlayerCharacter();
        if (playerState == null)
        {
            SetVisible(false);
            return;
        }

        float dt = Time.unscaledDeltaTime;
        _positionFetchTimer += dt;
        _distanceUpdateTimer += dt;

        // 判断是否需要重新查询目标位置（较重的操作）
        bool needFetchPosition = false;
        if (!_cachedValid) needFetchPosition = true;
        else if (_positionFetchTimer >= positionFetchInterval) needFetchPosition = true;
        else
        {
            // 如果玩家移动超过阈值，重新查询（用于动态锚点选择）
            if ((_lastPlayerPos - playerState.transform.position).sqrMagnitude > playerMoveThreshold * playerMoveThreshold)
                needFetchPosition = true;
        }

        if (needFetchPosition)
        {
            _positionFetchTimer = 0f;
            // 尝试从服务获取一次目标信息（可能涉及查找锚点，较重）
            // 使用快速接口获取平方距离，避免内部做开方运算
            bool ok = TaskTrackingService.Instance.TryGetArrowTargetFast(out var targetPos, out bool inside, out float fetchedDistanceSqr, out _, out string scene, out _);
            if (!ok)
            {
                // 无目标：隐藏/淡出
                if (hideWhenNoTrack)
                {
                    SetVisible(false);
                    UpdateTaskName(string.Empty);
                    if (arrowRect != null && animateScaleOnChange) _targetArrowScale = Vector3.zero;
                    else if (arrowRect != null && !animateScaleOnChange) arrowRect.gameObject.SetActive(false);
                }
                else
                {
                    SetVisible(true);
                    UpdateTexts("无追踪");
                    UpdateTaskName(string.Empty);
                    if (arrowRect != null)
                    {
                        if (animateScaleOnChange) _targetArrowScale = Vector3.zero;
                        else arrowRect.gameObject.SetActive(false);
                    }
                }
                _cachedValid = false;
                AnimateArrowScale();
                return;
            }

            // 有目标，缓存数据（直接使用平方距离）
            _cachedTargetPos = targetPos;
            _cachedTargetScene = scene;
            _cachedInside = inside;
            _cachedDistanceSqr = fetchedDistanceSqr;
            _cachedValid = true;
        }

        // 到这里 _cachedValid 为 true 且包含最近一次查询的数据
        // 填充任务名（如果有）
        var trackedTask = TaskTrackingService.Instance.GetTrackedTask();
        UpdateTaskName(trackedTask != null ? trackedTask.taskName : string.Empty);

        // 是否已到达
        bool showArrowGraphic; // 声明一次，在下面分支赋值并使用
        if (_cachedInside)
        {
            if (_lastText != "已到达")
            {
                UpdateTexts("已到达");
                _lastText = "已到达";
            }
            // 隐藏箭头图形或平滑缩放
            showArrowGraphic = !(_cachedInside && hideArrowWhenInside);
            if (arrowRect != null)
            {
                if (animateScaleOnChange)
                {
                    _targetArrowScale = showArrowGraphic ? Vector3.one : Vector3.zero;
                    if (showArrowGraphic && !arrowRect.gameObject.activeSelf) arrowRect.gameObject.SetActive(true);
                }
                else
                {
                    arrowRect.gameObject.SetActive(showArrowGraphic);
                }
            }
            AnimateArrowScale();
            return;
        }

        // 没到达：确保可见并根据缓存位置每帧平滑更新旋转以响应玩家转身
        SetVisible(true);
        showArrowGraphic = !(_cachedInside && hideArrowWhenInside);
        if (arrowRect != null)
        {
            if (animateScaleOnChange)
            {
                _targetArrowScale = showArrowGraphic ? Vector3.one : Vector3.zero;
                if (showArrowGraphic && !arrowRect.gameObject.activeSelf) arrowRect.gameObject.SetActive(true);
            }
            else
            {
                arrowRect.gameObject.SetActive(showArrowGraphic);
            }
        }

        // 计算方向与旋转 —— 每帧执行（轻量），保证旋转平滑且响应玩家转向
        var playerPos = playerState.transform.position;
        Vector3 flatTarget = _cachedTargetPos;
        if (flattenY) flatTarget.y = playerPos.y;
        Vector3 toTarget = flatTarget - playerPos;

        if (useCompassMode && arrowRect)
        {
            Vector3 forward = playerState.transform.forward;
            if (flattenY)
            {
                forward.y = 0f;
                toTarget.y = 0f;
            }

            if (toTarget.sqrMagnitude > 0.0001f)
            {
                float angleTo = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                float angleForward = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                float signedAngle = Mathf.DeltaAngle(angleForward, angleTo);

                float currentZ = arrowRect.localEulerAngles.z;
                if (currentZ > 180f) currentZ -= 360f;
                float targetZ = -signedAngle;
                float smoothZ = Mathf.SmoothDampAngle(currentZ, targetZ, ref _rotationVelocity, arrowRotationSmoothTime, arrowMaxRotationSpeed, Time.unscaledDeltaTime);

                arrowRect.localRotation = Quaternion.Euler(0f, 0f, smoothZ);
            }
        }

        // 文本距离只在显著变化时更新以节省 UI 开销
        // 仅在需要时（定时或显著变化）才计算开方并刷新 UI
        bool needUpdateDistance = false;
        float sqDiff = (_lastCachedDistanceSqr < 0f) ? float.PositiveInfinity : Mathf.Abs(_cachedDistanceSqr - _lastCachedDistanceSqr);
        float sqThreshold = distanceChangeThreshold * distanceChangeThreshold;
        if (sqDiff > sqThreshold) needUpdateDistance = true; // 距离变化显著
        if (_distanceUpdateTimer >= distanceUpdateInterval) needUpdateDistance = true; // 定期更新

        if (needUpdateDistance)
        {
            _distanceUpdateTimer = 0f;
            _lastCachedDistanceSqr = _cachedDistanceSqr;

            float distance = _cachedDistanceSqr > 0f ? Mathf.Sqrt(_cachedDistanceSqr) : 0f;
            if (distance < minDistanceToShow) distance = 0f;
            string disStr;
            if (distance <= 0.01f) disStr = "0m";
            else disStr = distance >= 1000f ? (distance / 1000f).ToString("F1") + "km" : Mathf.RoundToInt(distance) + "m";

            if (appendSceneNameWhenDifferent)
            {
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (!string.IsNullOrEmpty(_cachedTargetScene) && _cachedTargetScene != activeScene)
                {
                    disStr += " (" + _cachedTargetScene + ")";
                }
            }

            if (_lastText != disStr)
            {
                UpdateTexts(disStr);
                _lastText = disStr;
            }
        }

        _lastPlayerPos = playerPos;

        // 每帧推进缩放过渡
        AnimateArrowScale();
    }

    private void AnimateArrowScale()
    {
        if (arrowRect == null) return;
        if (!animateScaleOnChange)
            return;

        // 平滑缩放到目标值
        _currentArrowScale = Vector3.SmoothDamp(_currentArrowScale, _targetArrowScale, ref _arrowScaleVelocity, arrowScaleSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        arrowRect.localScale = _currentArrowScale;

        // 当缩放非常小时，考虑禁用 GameObject 以节省开销
        if (_currentArrowScale.magnitude <= hideScaleThreshold && arrowRect.gameObject.activeSelf)
        {
            arrowRect.gameObject.SetActive(false);
        }
        else if (_currentArrowScale.magnitude > hideScaleThreshold && !arrowRect.gameObject.activeSelf)
        {
            arrowRect.gameObject.SetActive(true);
        }
    }

    private void UpdateTexts(string txt)
    {
        if (distanceText != null) distanceText.text = txt;
    }

    private void UpdateTaskName(string taskName)
    {
        if (taskNameText == null) return;
        bool has = !string.IsNullOrEmpty(taskName);
        taskNameText.text = has ? taskName : string.Empty;
        // 简单显示控制：当没有名字时隐藏文本对象以避免占位
        if (taskNameText.gameObject.activeSelf != has)
            taskNameText.gameObject.SetActive(has);
    }

    private void SetVisible(bool v)
    {
        if (canvasGroup)
        {
            float target = v ? 1f : 0f;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
            canvasGroup.interactable = v;
            canvasGroup.blocksRaycasts = v;
        }
    }

}
