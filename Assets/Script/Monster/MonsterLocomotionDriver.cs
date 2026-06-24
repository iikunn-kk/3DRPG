using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 将AI寻路/移动与动画Blend Tree(VSpeed/HSpeed)对齐的驱动器。
/// - 输入：NavMeshAgent的速度/期望速度 或 位置增量
/// - 输出：Animator的 VSpeed/HSpeed（非根运动，四方向平移），并平滑旋转朝向
/// - 目标：避免模型面朝向与实际移动方向不一致；Idle 时严格置零
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]  // 在 PositionInterpolator(-100) 之后执行，确保读到当帧插值后的位置
public class MonsterLocomotionDriver : MonoBehaviour
{
    [Header("必需/可选引用")]
    [Tooltip("可选：如果 Animator 不在同一对象上可手动赋值；若与 MonsterAnimationController 共存，仅用于设置 V/H 参数")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent; // 可选，若为空将从本体获取；若禁用则回退用位移速度

    [Header("可选：只旋转该模型节点（不影响导航物体），为空则旋转本体")]
    [SerializeField] private Transform visualRoot;

    [Header("参数名（需与动画树一致)")]
    [SerializeField] private string vSpeedParam = "VSpeed";
    [SerializeField] private string hSpeedParam = "HSpeed";

    [Header("速度->动画 映射 & 平滑")]
    [Tooltip("参考的行走速度（该速度在动画树中约等于 1.0）；追击速度高于此值时可超过 1.5 触发跑步层")]
    [SerializeField] private float referenceWalkSpeed = 2f;
    [Tooltip("启动时若存在 NavMeshAgent 则用其初始 speed 作为参考行走速度（建议为巡逻速度）")]
    [SerializeField] private bool autoDetectReferenceFromAgent = true;
    [Tooltip("认为静止的最小实际速度（低于该值视为 0）")]
    [SerializeField] private float minMoveSpeed = 0.05f;
    [Tooltip("Animator.SetFloat 的阻尼时间（秒）")]
    [SerializeField] private float blendDampTime = 0.08f;
    [Tooltip("用于原始速度的低通滤波，0=无滤波，数值越大越平滑")]
    [Range(0f, 1f)][SerializeField] private float velocitySmoothing = 0.12f;
    [Tooltip("限制 V/H 的最大绝对值，避免极端速度导致动画出界（保持大于 1.5 即可触发跑步）")]
    [SerializeField] private float maxBlendAbs = 3f;

    [Header("朝向控制")]
    [Tooltip("是否由本组件负责旋转朝向；关掉则只驱动 V/H，不做旋转")]
    public bool enableRotation = true;
    [Tooltip("旋转速度（度/秒）或插值速度（更直觉）")]
    [SerializeField] private float rotateLerpSpeed = 12f;
    [Tooltip("优先面向的目标（追击/攻击时由状态机设置）。为空则按速度/路径方向对齐")]
    [SerializeField] private Transform faceTarget;

    [Header("模型前向修正")]
    [Tooltip("若模型的可见前方其实是 -Z（或整体旋转了180°），勾选此项以翻转朝向与前进速度映射，修复偶发‘倒着走’的表现。")]
    [SerializeField] private bool invertModelForward = false;
    [SerializeField][Tooltip("是否在开局自动检测一次是否需要反转模型前向（采样若干帧平均点积<0则自动勾选）")] private bool autoCalibrateForward = true;
    [SerializeField][Tooltip("用于自动校准的最少有效采样帧数")] private int calibrateSamples = 20;

    [Header("生命周期")]
    [SerializeField] private MonsterCombat combat; // 用于侦测死亡，死亡后停止旋转/更新

    // 缓存
    private int _vHash, _hHash;
    private Transform _refTransform; // 参考坐标，用于将速度转到本地以求 H/V
    private Vector3 _lastPos;
    private Vector3 _smoothedPlanarVel;
    private float _lastAgentSpeed;
    private bool _stoppedDueToDeath;
    private PositionInterpolator _interpolator;  // 远程实体的位置插值器（如果有）

    // 自动校准内部状态
    private bool _forwardCalibrated;
    private int _forwardCalibCount;
    private float _forwardDotAccum;

    public Transform FaceTarget { get => faceTarget; set => faceTarget = value; }

    // 允许外部显式设置行走参考速度（例如在 Init 中用 patrolSpeed 覆盖）
    public void SetReferenceWalkSpeed(float speed)
    {
        referenceWalkSpeed = Mathf.Max(0.01f, speed);
        // 一旦外部设定，关闭自动检测，避免被初始 agent.speed 覆盖
        autoDetectReferenceFromAgent = false;
    }

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        combat = GetComponent<MonsterCombat>();
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (combat == null) combat = GetComponent<MonsterCombat>();
        _interpolator = GetComponent<PositionInterpolator>();
        _refTransform = visualRoot != null ? visualRoot : transform;
        _vHash = string.IsNullOrEmpty(vSpeedParam) ? 0 : Animator.StringToHash(vSpeedParam);
        _hHash = string.IsNullOrEmpty(hSpeedParam) ? 0 : Animator.StringToHash(hSpeedParam);
        _lastPos = transform.position;
        if (animator != null)
        {
            animator.applyRootMotion = false; // 非根运动
        }
        if (agent != null)
        {
            _lastAgentSpeed = agent.speed;
            if (autoDetectReferenceFromAgent)
            {
                // 用初始 agent.speed 作为行走参考速度（通常是巡逻速度）
                referenceWalkSpeed = Mathf.Max(0.01f, agent.speed);
            }
            agent.updateRotation = false; // 旋转交由本组件
        }
        // 初始化自动校准状态
        _forwardCalibrated = !autoCalibrateForward; // 若不开启自动校准，则视为已校准
        _forwardCalibCount = 0;
        _forwardDotAccum = 0f;
    }

    private void Update()
    {
        // 死亡后彻底停止：不再更新旋转/动画参数，避免“尸体还在转头/抖动”
        if (combat != null && combat.IsDead)
        {
            if (!_stoppedDueToDeath)
            {
                // 一次性清理：清空面向目标 & 将 Blend 值置零
                faceTarget = null;
                if (animator != null)
                {
                    if (_hHash != 0) animator.SetFloat(_hHash, 0f);
                    if (_vHash != 0) animator.SetFloat(_vHash, 0f);
                }
                _stoppedDueToDeath = true;
            }
            return;
        }

        // 1) 计算水平速度（优先 NavMeshAgent.desiredVelocity，其次 agent.velocity，再次 位置差）
        Vector3 planarVel = Vector3.zero;
        bool haveAgent = agent != null && agent.enabled;
        if (haveAgent)
        {
            Vector3 dv = agent.desiredVelocity; dv.y = 0f;
            Vector3 av = agent.velocity; av.y = 0f;
            planarVel = dv.sqrMagnitude > 0.0001f ? dv : av;
        }

        if (!haveAgent)
        {
            if (_interpolator != null)
            {
                // 远程实体：直接从 PositionInterpolator 获取已平滑的速度，
                // 避免从位置增量反推时把插值跳跃放大为速度尖峰
                planarVel = _interpolator.SmoothedVelocity;
                planarVel.y = 0f;
            }
            else
            {
                Vector3 delta = transform.position - _lastPos; delta.y = 0f;
                float dt = Mathf.Max(Time.deltaTime, 0.000001f);
                planarVel = delta / dt;
            }
        }
        _lastPos = transform.position;

        // 低通滤波，避免参数跳变
        _smoothedPlanarVel = Vector3.Lerp(_smoothedPlanarVel, planarVel, 1f - Mathf.Clamp01(velocitySmoothing));

        float speed = _smoothedPlanarVel.magnitude;

        // 静止时强制归零，避免低通滤波器指数衰减残留的 e-4 级抖动
        if (speed < minMoveSpeed)
        {
            _smoothedPlanarVel = Vector3.zero;
            speed = 0f;
        }

        // 1.5) 自动校准模型前向（仅在未面向某目标、且存在足够移动速度时采样若干帧）
        if (!_forwardCalibrated && speed >= (minMoveSpeed * 2f) && faceTarget == null)
        {
            Vector3 dir = _smoothedPlanarVel.sqrMagnitude > 0.0001f ? _smoothedPlanarVel.normalized : Vector3.zero;
            Vector3 visualFwd = (_refTransform != null ? _refTransform.forward : transform.forward);
            visualFwd.y = 0f; dir.y = 0f;
            if (visualFwd.sqrMagnitude > 0.0001f && dir.sqrMagnitude > 0.0001f)
            {
                float dot = Vector3.Dot(visualFwd.normalized, dir.normalized);
                _forwardDotAccum += dot;
                _forwardCalibCount++;
                if (_forwardCalibCount >= Mathf.Max(5, calibrateSamples))
                {
                    float avg = _forwardDotAccum / _forwardCalibCount;
                    // 若平均朝向与移动方向大多相反，则启用翻转
                    if (avg < -0.2f) invertModelForward = true;
                    _forwardCalibrated = true;
                }
            }
        }

        // 2) 朝向（保持不变）
        if (enableRotation)
        {
            Vector3 faceDir = Vector3.zero;
            if (faceTarget != null)
            {
                faceDir = faceTarget.position - _refTransform.position; faceDir.y = 0f;
            }
            if (faceDir.sqrMagnitude < 0.0001f)
            {
                faceDir = _smoothedPlanarVel; faceDir.y = 0f;
            }
            if (faceDir.sqrMagnitude < 0.0001f && haveAgent && agent.hasPath)
            {
                faceDir = agent.steeringTarget - _refTransform.position; faceDir.y = 0f;
            }
            if (faceDir.sqrMagnitude > 0.0001f)
            {
                // 若模型前向被倒装（-Z 为视觉前方），则取反以保证视觉朝向正确
                Vector3 lookDir = invertModelForward ? -faceDir : faceDir;
                Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                Transform rotT = visualRoot != null ? visualRoot : transform;
                rotT.rotation = Quaternion.Slerp(rotT.rotation, targetRot, Mathf.Clamp01(rotateLerpSpeed * Time.deltaTime));
            }
        }

        // 3) 计算本地 H/V 并写入 Animator（以参考速度归一化，不夹到 ±1）
        if (animator != null)
        {
            float v = 0f, h = 0f;
            if (speed >= minMoveSpeed && referenceWalkSpeed > 0.0001f)
            {
                Vector3 local = _refTransform.InverseTransformDirection(_smoothedPlanarVel);
                v = local.z / referenceWalkSpeed;
                h = local.x / referenceWalkSpeed;
                // 若模型前向倒装，翻转 V 的正负（H 左右无需翻）
                if (invertModelForward) v = -v;
                // 限幅，保留 >1.5 的跑步区间
                v = Mathf.Clamp(v, -maxBlendAbs, maxBlendAbs);
                h = Mathf.Clamp(h, -maxBlendAbs, maxBlendAbs);
            }
            if (_hHash != 0) animator.SetFloat(_hHash, h, blendDampTime, Time.deltaTime);
            if (_vHash != 0) animator.SetFloat(_vHash, v, blendDampTime, Time.deltaTime);
        }
    }
}
