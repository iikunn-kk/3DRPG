using System.Collections;
using UnityEngine;

/// <summary>
/// 持久化的奥术射线（通道）实现：
/// - 不在停止时销毁自身，而是切换为非激活状态供复用；
/// - 管理一个单一的命中特效实例（loop 粒子），只有当射线持续命中同一目标时才播放；
/// - 提供 Activate/Deactivate 接口，供外部控制器复用同一个预制体实例。
/// - 注意：不再主动获取 LockOnController，目标由外部（例如 NormalAttackController）传入。
/// - 改进：将命中特效与 LineRenderer 作为 prefab 的子物体（通过 Inspector 绑定），避免运行时 Instantiate；
///         增加命中特效延迟关闭以消除闪烁；优先使用 MonsterBase.hitVfxPoint（若存在）来放置命中特效。
/// </summary>
public class ArcaneRaySkill : Skill
{
    [Header("Beam Visuals")]
    [SerializeField] private LineRenderer line; // 射线段（应为 prefab 子物体）
    [Tooltip("作为 Prefab 子物体存在的命中特效（loop）。请在 ArcaneRaySkill prefab 中置为子物体并勾选禁用。")]
    [SerializeField] private GameObject hitVfxPrefab; // 现在期望为 prefab 的子物体（在场景中只有这一个实例）

    [Header("Ray Settings")]
    [SerializeField] private float rangeOverride; // 0 使用 SkillSO.castRange
    [SerializeField] private float tickInterval = 0.2f;
    [SerializeField] private LayerMask targetLayerMask;

    [Header("VFX & Visual smoothing")]
    [Tooltip("命中特效在短时间内丢失时不会立即关闭，避免闪烁（秒）")]
    [SerializeField] private float vfxDisableDelay = 0.12f;
    [Tooltip("射线端点平滑跟随速度，越大跟得越快")]
    [SerializeField] private float beamFollowSpeed = 20f;

    // 运行时
    private bool _isChanneling;
    private Coroutine _tickCo;
    private float _range;

    // 持久化的命中特效实例（不再 Instantiate）
    private GameObject _hitVfxInstance;
    private ParticleSystem[] _hitVfxSystems;
    private Transform _currentHitTarget;

    // VFX 关闭延迟协程
    private Coroutine _vfxDisableCo;

    // firePoint 由 NormalAttackController（玩家）提供
    private Transform _firePoint;

    // 外部可传入的指定目标（可为 null -> 无目标模式）
    private Transform _target;

    // 射线端点平滑追踪
    private Vector3 _currentBeamEnd;

    public bool IsActive => _isChanneling;
    public void RequestStopFromExternal() => Deactivate();

    private void Awake()
    {
        enableAutoDestroy = false;
        // 初始化 LineRenderer 与子物体 VFX 引用（期望它们是 prefab 的子物体）
        if (line != null) line.enabled = false;

        // 处理 hitVfxPrefab：如果在 Inspector 中指向了子物体，则直接引用并禁用；否则尝试在子层级中查找常见命名
        if (hitVfxPrefab != null)
        {
            _hitVfxInstance = hitVfxPrefab;
        }
        else
        {
            // 尝试查找名为 "HitVfx" 或包含 ParticleSystem 的子物体
            var t = transform.Find("HitVfx");
            if (t != null) _hitVfxInstance = t.gameObject;
            else
            {
                var ps = GetComponentInChildren<ParticleSystem>(true);
                if (ps != null) _hitVfxInstance = ps.gameObject;
            }
        }

        if (_hitVfxInstance != null)
        {
            _hitVfxInstance.SetActive(false);
            _hitVfxSystems = _hitVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
        }
    }

    /// <summary>
    /// 激活此持久化实例：
    /// - 调用方需要先准备好 PlayerSkill（例如从 SkillController 的快照中获取）
    /// - 可传入 target 为锁定目标（由 NormalAttackController 获取并传入），也可以为 null（无目标朝前方发射）
    /// </summary>
    public void Activate(Transform caster, PlayerSkill playerSkill, Transform firePoint, Transform target = null)
    {
        gameObject.SetActive(true);
        _firePoint = firePoint;
        _target = target;
        Execute(caster, playerSkill);

        _range = rangeOverride > 0f ? rangeOverride : (PlayerSkill?.SkillSO != null ? PlayerSkill.SkillSO.castRange : 20f);
        // 初始化平滑端点
        Vector3 origin = _firePoint != null ? _firePoint.position : (Caster != null ? Caster.position + Vector3.up * 1.2f : transform.position);
        _currentBeamEnd = origin + (Caster != null ? Caster.forward : transform.forward) * _range;

        StartChannel();
    }

    private void StartChannel()
    {
        _isChanneling = true;
        if (line != null) line.enabled = true;
        if (_tickCo != null) StopCoroutine(_tickCo);
        _tickCo = StartCoroutine(TickDamageRoutine());
    }

    /// <summary>
    /// 取消激活但不销毁，可复用
    /// </summary>
    public void Deactivate()
    {
        if (!_isChanneling) return;
        _isChanneling = false;
        if (_tickCo != null)
        {
            StopCoroutine(_tickCo);
            _tickCo = null;
        }

        if (line != null) line.enabled = false;

        // 停止循环音效（防止由外部直接调用 Deactivate 未经控制器停止声音）
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopWeaponSound(SkillSoundType.奥术射线发射);
        }

        // 立即停止命中特效（因为玩家已停止射击）
        if (_vfxDisableCo != null)
        {
            StopCoroutine(_vfxDisableCo);
            _vfxDisableCo = null;
        }
        SetHitVfxActive(false);
        _currentHitTarget = null;
        _firePoint = null;
        _target = null;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!_isChanneling) return;
        UpdateBeamVisual();
    }

    private IEnumerator TickDamageRoutine()
    {
        if (PlayerSkill == null) yield break;
        var cs = GameManager.Instance?.CurrentPlayerCharacter();

        while (_isChanneling)
        {
            Vector3 origin = _firePoint != null ? _firePoint.position : (Caster != null ? Caster.position + Vector3.up * 1.2f : transform.position);
            Vector3 dir = ResolveDirection(_target);

            bool hitSomething = false;
            // 修改：包含 Trigger 的射线检测，并且从命中的 collider 向父级查找 MonsterBase
            if (Physics.Raycast(origin, dir, out RaycastHit hit, _range, targetLayerMask, QueryTriggerInteraction.Collide))
            {
                var hitCol = hit.collider;
                var monster = hitCol != null ? hitCol.GetComponent<MonsterBase>() : null;
                if (monster != null)
                {
                    var monsterCombat = monster.GetComponent<MonsterCombat>();
                    if (monsterCombat != null && monsterCombat.CurrentHealth > 0)
                    {
                        hitSomething = true;

                        float total = PlayerSkill.GetDamage();
                        if (cs != null)
                        {
                            cs.DealDamageTo(monster.transform, total);
                        }
                        else
                        {
                            monsterCombat.TakeDamage(Mathf.RoundToInt(total));
                        }

                        Vector3 vfxPos = hit.point;
                        Transform hitVfxPoint = monster.GetHitVfxPoint();
                        if (hitVfxPoint != null)
                        {
                            vfxPos = hitVfxPoint.position;
                        }

                        bool shouldShowVfx = (_target == null) || (monster.transform == _target);

                        if (shouldShowVfx)
                        {
                            if (_currentHitTarget != monster.transform)
                            {
                                _currentHitTarget = monster.transform;
                                if (_vfxDisableCo != null)
                                {
                                    StopCoroutine(_vfxDisableCo);
                                    _vfxDisableCo = null;
                                }
                                AttachAndPlayHitVfx(vfxPos, monster.transform);
                            }
                            else
                            {
                                if (_hitVfxInstance != null && _hitVfxInstance.activeSelf)
                                {
                                    _hitVfxInstance.transform.position = vfxPos;
                                }
                            }
                        }
                        else
                        {
                            _currentHitTarget = null;
                            ScheduleDisableVfx();
                        }
                    }
                }
            }

            if (!hitSomething)
            {
                _currentHitTarget = null;
                ScheduleDisableVfx();
            }

            yield return new WaitForSeconds(Mathf.Max(0.01f, tickInterval));
        }
    }

    private void UpdateBeamVisual()
    {
        if (line == null) return;
        Vector3 origin = _firePoint != null ? _firePoint.position : (Caster != null ? Caster.position + Vector3.up * 1.2f : transform.position);
        Vector3 dir = ResolveDirection(_target);
        Vector3 desiredEnd = origin + dir * _range;

        // 修改：包含 Trigger 的射线检测以匹配伤害判定
        if (Physics.Raycast(origin, dir, out RaycastHit hit, _range, targetLayerMask, QueryTriggerInteraction.Collide))
        {
            desiredEnd = hit.point;
        }

        // 平滑端点过渡，避免视觉抖动
        _currentBeamEnd = Vector3.Lerp(_currentBeamEnd, desiredEnd, Mathf.Clamp01(Time.deltaTime * beamFollowSpeed));

        line.positionCount = 2;
        line.SetPosition(0, origin);
        line.SetPosition(1, _currentBeamEnd);
    }

    private void AttachAndPlayHitVfx(Vector3 worldPos, Transform parent)
    {
        if (_hitVfxInstance == null) return;
        _hitVfxInstance.SetActive(true);
        _hitVfxInstance.transform.SetParent(parent, true);
        _hitVfxInstance.transform.position = worldPos;
        SetHitVfxActive(true);
    }

    private void ScheduleDisableVfx()
    {
        if (_hitVfxInstance == null) return;
        // 如果已有倒计时正在运行，则不重复启动
        if (_vfxDisableCo != null) return;
        _vfxDisableCo = StartCoroutine(DisableVfxAfterDelay());
    }

    private IEnumerator DisableVfxAfterDelay()
    {
        float t = 0f;
        while (t < vfxDisableDelay)
        {
            t += Time.deltaTime;
            // 如果在等待期间重新开始了命中特效（有新目标），则取消关闭
            if (_currentHitTarget != null) break;
            yield return null;
        }
        // 仅在 _currentHitTarget 为 null 时关闭
        if (_currentHitTarget == null)
        {
            SetHitVfxActive(false);
        }
        _vfxDisableCo = null;
    }

    private void SetHitVfxActive(bool active)
    {
        if (_hitVfxInstance == null) return;
        if (active)
        {
            _hitVfxInstance.SetActive(true);
            if (_hitVfxSystems != null)
            {
                foreach (var ps in _hitVfxSystems) if (!ps.isPlaying) ps.Play();
            }
        }
        else
        {
            if (_hitVfxSystems != null)
            {
                foreach (var ps in _hitVfxSystems) if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            _hitVfxInstance.SetActive(false);
        }
    }

    /// <summary>
    /// 解析射线方向：
    /// - 若传入 target，返回以目标为方向（仅考虑水平面）；
    /// - 若未传入 target，则返回 Caster 的 forward。
    /// </summary>
    private Vector3 ResolveDirection(Transform target)
    {
        if (target != null && Caster != null)
        {
            Vector3 toTarget = target.position - Caster.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f) return Caster.forward;
            return toTarget.normalized;
        }

        return Caster != null ? Caster.forward : transform.forward;
    }

    private void OnDisable()
    {
        if (_tickCo != null)
        {
            StopCoroutine(_tickCo);
            _tickCo = null;
        }
    }
}