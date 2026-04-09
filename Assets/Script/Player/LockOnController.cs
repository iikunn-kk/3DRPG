using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// 锁定系统（重构版）：
/// 1. 基于“玩家面朝方向”的一个矩形盒区域（宽*高*深）来检索怪物，而不是依赖摄像机角度。
/// 2. 180°（前半空间）含义：只要在玩家 forward 前方的盒子里就算候选。
/// 3. 规则：
///    - 没有当前目标：锁定最近的。
///    - 已有目标：如果当前目标不是最近的，则改为最近的。
///    - 已有目标且已经是最近的，并且有多个候选：在“近距离组”中随机换一个（近距离组 = 距离 <= nearestDistance + nearGroupRadius）。
/// 4. 支持动态扩容 Overlap 缓冲，减少 GC。
/// </summary>
public class LockOnController : MonoBehaviour
{
    [Header("区域设置（以玩家朝向为中心）")]
    [Tooltip("盒区域的深度（Forward 方向长度）")] public float maxLockDistance = 25f; // depth
    [Tooltip("盒区域的水平宽度（X 方向总宽度）")] public float lockWidth = 20f; // total width
    [Tooltip("盒区域的垂直高度（Y 方向总高度）")] public float lockHeight = 8f; // total height
    [Tooltip("接近距离阈值：在已经是最近目标时，从 [nearestDistance, nearestDistance+nearGroupRadius] 内随机切换")] public float nearGroupRadius = 5f;
    [Tooltip("用于物理检索的 LayerMask（怪物层）")] public LayerMask targetLayerMask = ~0;

    [Header("事件/回调")]
    [SerializeField] private TargetChangeEventSO targetChangeEventSO;
    [SerializeField] private VoidEventSO voidEventSO;

    // 当前锁定目标 & 相关组件缓存
    private MonsterBase _currentTarget;
    private MonsterCombat _currentTargetCombat;

    // 复用碰撞体缓存
    private Collider[] _overlapBuffer = new Collider[64];
    private const int MaxOverlapBufferSize = 1024;

    // 临时列表缓存（候选排序/处理），避免每次 new
    private readonly List<MonsterBase> _candidates = new List<MonsterBase>(64);
    private readonly List<MonsterBase> _nearGroup = new List<MonsterBase>(32);

    // 供外部调试：是否绘制 Gizmos
    [Header("调试")]
    public bool drawGizmos = true;
    public Color gizmoColor = new Color(0f, 0.7f, 1f, 0.15f);
    public Color gizmoEdgeColor = new Color(0f, 0.7f, 1f, 0.6f);

    // 缓存 transform
    private Transform _selfTransform;
    private InputSystem_Actions playerInput;

    private void Awake()
    {
        //新输入系统的配置
        playerInput = new InputSystem_Actions();
        playerInput.Player.Enable();
        _selfTransform = transform;
        // 尝试从 Resources 加载事件 SO（如果没有在 Inspector 指定）
        if (targetChangeEventSO == null)
            targetChangeEventSO = Resources.Load<TargetChangeEventSO>("TargetChangeEventSO");
    }
    private void OnEnable()
    {
        playerInput.Player.LockOn.performed += OnLockActionPerformed;
    }

    private void OnDisable()
    {
        playerInput.Player.LockOn.performed -= OnLockActionPerformed;
        ClearLock();
    }

    public void OnLockActionPerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.ReadValueAsButton()) return;
        HandleLockToggle();
    }

    private void Update()
    {
        if (_currentTarget != null)
        {
            if (_currentTargetCombat == null || _currentTarget == null)
            {
                ClearLock();
                return;
            }
            // 超出深度直接清除
            Vector3 origin = (_selfTransform != null) ? _selfTransform.position : transform.position;
            float distSqr = (_currentTarget.transform.position - origin).sqrMagnitude;
            float maxDepthSqr = maxLockDistance * maxLockDistance;
            if (distSqr > maxDepthSqr)
            {
                ClearLock();
            }
        }
    }

    private void HandleLockToggle()
    {
        // 刷新候选列表
        CollectCandidates();

        if (_candidates.Count == 0)
        {
            // 无候选
            if (_currentTarget != null) ClearLock(); else RaiseNoTargetFeedback();
            return;
        }

        // 如果当前没有目标 -> 选最近
        MonsterBase nearest = _candidates[0]; // CollectCandidates 已经按距离升序
        if (_currentTarget == null)
        {
            SetLock(nearest);
            return;
        }

        // 如果当前目标已经失效（不在候选中）
        if (!_candidates.Contains(_currentTarget))
        {
            SetLock(nearest);
            return;
        }

        // 当前目标在候选中，但如果不是最邻近 -> 切换到最近
        if (_currentTarget != nearest)
        {
            SetLock(nearest);
            return;
        }

        // 当前就是最近：若候选数不足 2 不操作
        if (_candidates.Count < 2)
        {
            // 可反馈一下（保持原锁定）
            return;
        }

        // 构建“近距离组” = 距离 <= nearestDist + nearGroupRadius
        float nearestDist = Vector3.Distance(_selfTransform.position, nearest.transform.position);
        float threshold = nearestDist + Mathf.Max(0.01f, nearGroupRadius);
        _nearGroup.Clear();
        for (int i = 0; i < _candidates.Count; i++)
        {
            var c = _candidates[i];
            if (c == null || c == _currentTarget) continue;
            float d = Vector3.Distance(_selfTransform.position, c.transform.position);
            if (d <= threshold)
                _nearGroup.Add(c);
        }

        // 如果近距离组为空，则尝试用所有候选（除当前）作为备选
        if (_nearGroup.Count == 0)
        {
            for (int i = 0; i < _candidates.Count; i++)
            {
                var c = _candidates[i];
                if (c != null && c != _currentTarget)
                    _nearGroup.Add(c);
            }
        }

        if (_nearGroup.Count == 0)
            return; // 没别的可选

        int randomIndex = Random.Range(0, _nearGroup.Count);
        SetLock(_nearGroup[randomIndex]);
    }

    private void RaiseNoTargetFeedback()
    {
        if (voidEventSO != null)
            voidEventSO.Raise(this);
    }

    #region Candidate Collection

    private void CollectCandidates()
    {
        _candidates.Clear();
        if (_selfTransform == null) _selfTransform = transform;

        Vector3 origin = _selfTransform.position;
        Vector3 forward = _selfTransform.forward;

        // 盒中心放在前方一半深度处，使得盒子整体位于角色前面
        Vector3 center = origin + forward * (maxLockDistance * 0.5f);
        Vector3 halfExtents = new Vector3(lockWidth * 0.5f, lockHeight * 0.5f, maxLockDistance * 0.5f);
        Quaternion orientation = Quaternion.LookRotation(forward, Vector3.up);

        int hitCount = OverlapBoxNonAllocAdaptive(center, halfExtents, orientation);
        if (hitCount <= 0) return;

        int count = Mathf.Min(hitCount, _overlapBuffer.Length);
        for (int i = 0; i < count; i++)
        {
            Collider c = _overlapBuffer[i];
            if (c == null) continue;
            MonsterBase m = c.GetComponentInParent<MonsterBase>();
            if (m == null) continue;
            // 检查 combat / 血量
            MonsterCombat mc = m.GetComponent<MonsterCombat>();
            if (mc == null || mc.CurrentHealth <= 0) continue;

            // 只取前半空间：dot >= 0
            Vector3 dir = (m.transform.position - origin);
            if (Vector3.Dot(forward, dir) < 0f) continue;

            // 去重：一个怪物被多个 collider 命中时避免重复
            if (!_candidates.Contains(m))
                _candidates.Add(m);
        }

        // 按距离升序排序
        _candidates.Sort((a, b) =>
        {
            float da = (a.transform.position - origin).sqrMagnitude;
            float db = (b.transform.position - origin).sqrMagnitude;
            return da.CompareTo(db);
        });
    }

    // 动态扩容 OverlapBox 缓冲
    private int OverlapBoxNonAllocAdaptive(Vector3 center, Vector3 halfExtents, Quaternion orientation)
    {
        // 显式包含 Trigger，避免依赖全局 Physics.queriesHitTriggers
        int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlapBuffer, orientation, targetLayerMask, QueryTriggerInteraction.Collide);
        if (hitCount < _overlapBuffer.Length) return hitCount;

        int newSize = _overlapBuffer.Length * 2;
        while (hitCount >= _overlapBuffer.Length && newSize <= MaxOverlapBufferSize)
        {
            _overlapBuffer = new Collider[newSize];
            hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlapBuffer, orientation, targetLayerMask, QueryTriggerInteraction.Collide);
            if (hitCount < _overlapBuffer.Length) break;
            newSize *= 2;
        }
        return hitCount;
    }

    #endregion

    #region Lock / Clear

    private void SetLock(MonsterBase target)
    {
        if (target == null) return;

        if (_currentTarget != null && _currentTarget != target)
        {
            _currentTarget.SetLocked(false);
        }

        _currentTarget = target;
        _currentTargetCombat = target.GetComponent<MonsterCombat>();
        _currentTarget.SetLocked(true);

        if (targetChangeEventSO != null)
            targetChangeEventSO.RaiseEvent(_currentTarget, this);
    }

    public void ClearLock()
    {
        if (_currentTarget != null)
            _currentTarget.SetLocked(false);

        _currentTarget = null;
        _currentTargetCombat = null;

        if (targetChangeEventSO != null)
            targetChangeEventSO.RaiseEvent(null, this);

        if (voidEventSO != null)
            voidEventSO.Raise(this);
    }

    public MonsterBase GetCurrentTarget() => _currentTarget;

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Transform t = Application.isPlaying ? _selfTransform : transform;
        if (t == null) return;
        Vector3 origin = t.position;
        Vector3 forward = t.forward;
        Vector3 center = origin + forward * (maxLockDistance * 0.5f);
        Vector3 size = new Vector3(lockWidth, lockHeight, maxLockDistance);
        Quaternion orientation = Quaternion.LookRotation(forward, Vector3.up);
        Color old = Gizmos.color;
        Gizmos.color = gizmoColor;
        Matrix4x4 m = Matrix4x4.TRS(center, orientation, Vector3.one);
        Gizmos.matrix = m;
        Gizmos.DrawCube(Vector3.zero, size);
        Gizmos.color = gizmoEdgeColor;
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = old;
    }
#endif
}
