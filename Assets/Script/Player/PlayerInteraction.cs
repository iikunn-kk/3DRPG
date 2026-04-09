using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem; // 添加新输入系统命名空间

/// <summary>
/// 玩家交互组件，处理与NPC和其他可交互对象的交互
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("交互设置")]
    [Tooltip("玩家与NPC交互的最大距离")]
    public float interactionDistance = 3f;

    [Tooltip("检测间隔时间（秒)")]
    public float detectionInterval = 0.2f;

    [Tooltip("可交互对象所在的图层")]
    public LayerMask interactableLayerMask = Physics.AllLayers;

    [Tooltip("显示在提示UI上的文本")]
    public string interactionPrompt = "F";

    // 当前目标交互对象引用
    private IInteractable _currentTarget;

    // 上次检测时间
    private float _lastDetectionTime;

    // 缓存用于OverlapSphereNonAlloc的碰撞体数组，避免频繁分配
    private Collider[] _collidersCache = new Collider[25];

    // 中央化的交互提示UI实例
    private InteractionPromptUI _centralPrompt;

    private InputSystem_Actions playerInput;

    private bool IsValid(IInteractable target)
    {
        if (target == null) return false;
        // Unity 对象被 Destroy 后会重载 == 变为 null，这里通过 as Object 再判断一次
        var unityObj = target as Object;
        return unityObj != null;
    }

    void Awake()
    {
        playerInput = new InputSystem_Actions();
        playerInput.Player.Enable();
    }

    void OnEnable()
    {
        playerInput.Player.Interact.performed += OnInteract;
    }
    void OnDisable()
    {
        playerInput.Player.Interact.performed -= OnInteract;
    }

    private void Start()
    {
        // 只实例化一次中央提示UI（从 Resources 加载），并保持隐藏
        GameObject prefab = Resources.Load<GameObject>("PlayingUI/InteractionPromptUI");
        if (prefab != null)
        {
            GameObject go = Instantiate(prefab);
            _centralPrompt = go.GetComponent<InteractionPromptUI>();
            if (_centralPrompt != null)
            {
                _centralPrompt.HidePrompt();
            }
        }
        else
        {
            Debug.LogError("未找到 InteractionPromptUI 预制体，请确保它在 Resources/PlayingUI/InteractionPromptUI");
        }
    }



    /// <summary>
    /// 执行交互
    /// </summary>
    private void InteractWith(IInteractable interactable)
    {
        if (interactable != null)
        {
            interactable.Interact(this);
            // 在调用交互逻辑后，由 ClearCurrentTarget 负责重置检测并重新创建提示（如果仍在范围内）。
            ClearCurrentTarget();
        }
    }

    private void Update()
    {
        DetectInteractableInRange();
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.ReadValueAsButton() && _currentTarget != null && !EventSystem.current.IsPointerOverGameObject())
        {
            InteractWith(_currentTarget);
        }
    }

    private void DetectInteractableInRange()
    {
        // 始终先校验当前目标是否还有效（是否被销毁）
        if (_currentTarget != null && !IsValid(_currentTarget))
        {
            _currentTarget = null;
            if (_centralPrompt != null && _centralPrompt.gameObject.activeSelf)
            {
                _centralPrompt.HidePrompt();
            }
        }

        if (Time.time - _lastDetectionTime < detectionInterval || UIManager.Instance.isOpenedPanel)
            return;

        _lastDetectionTime = Time.time;

        int colliderCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            interactionDistance,
            _collidersCache,
            interactableLayerMask);

        IInteractable closestInteractable = null;
        float closestDistanceSqr = float.MaxValue;

        for (int i = 0; i < colliderCount; i++)
        {
            IInteractable interactable = _collidersCache[i].GetComponent<IInteractable>();
            if (interactable != null && IsValid(interactable))
            {
                float distanceSqr = (transform.position - _collidersCache[i].transform.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    closestInteractable = interactable;
                }
            }
        }

        if (closestInteractable != null && closestDistanceSqr > interactionDistance * interactionDistance)
        {
            closestInteractable = null;
        }

        if (_currentTarget != closestInteractable)
        {
            if (_currentTarget != null)
            {
                // 隐藏中央提示
                if (_centralPrompt != null)
                    _centralPrompt.HidePrompt();
            }

            _currentTarget = closestInteractable;

            if (_currentTarget != null)
            {
                // 将中央提示附着到目标的锚点并显示目标提供的提示文本(当前策略是统一显示交互键位)
                Transform anchor = _currentTarget.GetPromptAnchor();
                if (_centralPrompt != null)
                {
                    if (anchor != null)
                        _centralPrompt.AttachTo(anchor);
                    _centralPrompt.ShowPrompt(interactionPrompt);
                }
            }
        }
        else
        {
            // 当前目标未变化，但如果 UI 被意外隐藏（例如目标销毁触发 LateUpdate 隐藏而这一帧还未重新检测到新目标）且目标仍有效，则重新显示
            if (_currentTarget != null && _centralPrompt != null && !_centralPrompt.gameObject.activeSelf)
            {
                Transform anchor = _currentTarget.GetPromptAnchor();
                if (anchor != null)
                    _centralPrompt.AttachTo(anchor);
                _centralPrompt.ShowPrompt(interactionPrompt);
            }
        }
    }

    /// <summary>
    /// 清除当前交互目标
    /// </summary>
    public void ClearCurrentTarget()
    {
        _currentTarget = null;
        // 允许下一帧立即重新检测，以便如果玩家仍在范围内，提示会被重新创建
        _lastDetectionTime = 0f;
        DetectInteractableInRange();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}