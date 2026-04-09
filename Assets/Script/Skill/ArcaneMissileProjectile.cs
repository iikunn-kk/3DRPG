using UnityEngine;

/// <summary>
/// 奥术飞弹的弹丸逻辑：
/// - 可追踪（homing）到目标；
/// - 命中后产生爆炸粒子（可选）并对命中目标造成伤害；
/// - 可选：在命中时做范围伤害（Lv5 行为）或查找弹射目标并造成弹射伤害（Lv10 行为）。
/// - 内部使用 OverlapSphereNonAlloc 重用缓冲区，减少 GC 分配。
/// - 命中检测使用目标的 Collider.ClosestPoint 来获得实际接触点，避免角色枢轴在脚下导致的偏差。
/// </summary>
public class ArcaneMissileProjectile : MonoBehaviour
{
    private Transform _target;
    private float _speed;
    private int _damage;
    private float _hitRadius;
    private GameObject _explosionPrefab;
    private LayerMask _enemyLayers;
    private float _aoeRadius;
    private float _aoePercent;
    private float _bounceRange;
    private int _remainingBounces;

    private float _maxLifetime = 6f;
    private float _lifeTimer;

    // 新增：由发射者注入的玩家 CharacterState，用于统一走暴击伤害流程
    private CharacterState _cs;

    // 重用缓冲区，避免每次 OverlapSphere 分配
    private const int OverlapBufferSize = 32;
    private static readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];

    // 初始化由发射者调用，传入所有必要参数
    public void Initialize(Transform target, float speed, int damage, float hitRadius, GameObject explosionPrefab,
                           LayerMask enemyLayers, float aoeRadius, float aoePercent, float bounceRange, int remainingBounces,
                           CharacterState casterState = null)
    {
        _target = target;
        _speed = Mathf.Max(0.01f, speed);
        _damage = Mathf.Max(0, damage);
        _hitRadius = Mathf.Max(0.01f, hitRadius);
        _explosionPrefab = explosionPrefab;
        _enemyLayers = enemyLayers;
        _aoeRadius = Mathf.Max(0f, aoeRadius);
        _aoePercent = Mathf.Clamp01(aoePercent);
        _bounceRange = Mathf.Max(0f, bounceRange);
        _remainingBounces = Mathf.Max(0, remainingBounces);
        _cs = casterState;

        _lifeTimer = 0f;
    }

    private void Update()
    {
        _lifeTimer += Time.deltaTime;
        if (_lifeTimer > _maxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (_target != null)
        {
            // Use MonsterBase's hit VFX point when available instead of relying on a Collider's ClosestPoint.
            Vector3 targetClosestPoint;

            var targetMb = _target.GetComponent<MonsterBase>();
            if (targetMb != null)
            {
                Transform hv = targetMb.GetHitVfxPoint();
                if (hv != null)
                {
                    targetClosestPoint = hv.position;
                }
                else
                {
                    targetClosestPoint = _target.position + Vector3.up * 0.8f;
                }
            }
            else
            {
                // 若没有 MonsterBase，则退回到以前的简单点位
                targetClosestPoint = _target.position + Vector3.up * 0.8f;
            }

            Vector3 to = targetClosestPoint - transform.position;
            float distSqr = to.sqrMagnitude;
            Vector3 dir = distSqr > 0.0001f ? to / Mathf.Sqrt(distSqr) : transform.forward;

            float step = _speed * Time.deltaTime;
            float stepSqr = step * step;

            // 如果在一步之内可以命中，或者与目标距离小于命中半径，则认为命中
            if (distSqr <= _hitRadius * _hitRadius || stepSqr >= distSqr)
            {
                // 命中点使用 MonsterBase 提供的 hitVfxPoint（若存在），否则使用计算得到的最近点
                Vector3 hitPoint = targetClosestPoint;
                HandleHit(_target, hitPoint);
                return;
            }

            transform.position += dir * step;
            transform.rotation = Quaternion.LookRotation(dir);
        }
        else
        {
            // 无目标时向前飞行并计时销毁
            transform.position += transform.forward * (_speed * Time.deltaTime);
        }
    }

    private void HandleHit(Transform hitTarget, Vector3 hitPoint)
    {
        // 命中播放音效（带默认冷却，避免过度堆叠）
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayWeaponSound(SkillSoundType.奥术飞弹击中);
        }
        // 先播放爆炸特效
        // 优先使用目标 MonsterBase 上配置的 hitVfxPoint（艺术家可配置），若不存在则使用射线的 hitPoint
        Vector3 vfxPos = hitPoint;
        var mb = hitTarget != null ? hitTarget.GetComponent<MonsterBase>() : null;
        if (mb != null)
        {
            // 使用MonsterBase的新API获取hitVfxPoint
            Transform hitVfxPoint = mb.GetHitVfxPoint();
            if (hitVfxPoint != null)
            {
                vfxPos = hitVfxPoint.position;
            }
        }
        if (_explosionPrefab != null)
        {
            var e = Instantiate(_explosionPrefab, vfxPos, Quaternion.identity);
            Destroy(e, 4f);
        }

        // 对命中目标造成伤害（Lv5/Lv10 都应该保留单体伤害）
        ApplyDamageToTransform(hitTarget, _damage);

        // 如果存在 AoE（Lv5 行为），则对范围内其他敌人造成 aoePercent 的伤害（叠加存在于 Lv10 之上）
        if (_aoeRadius > 0f && _aoePercent > 0f)
        {
            float aoeDmg = _damage * _aoePercent;
            int found = Physics.OverlapSphereNonAlloc(hitPoint, _aoeRadius, _overlapBuffer, _enemyLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < found; i++)
            {
                var col = _overlapBuffer[i];
                if (col == null) continue;
                // 忽略命中目标本身
                if (col.transform == hitTarget) continue;
                ApplyDamageToTransform(col.transform, aoeDmg);
            }
            if (found == _overlapBuffer.Length)
            {
                var all = Physics.OverlapSphere(hitPoint, _aoeRadius, _enemyLayers, QueryTriggerInteraction.Collide);
                for (int i = 0; i < all.Length; i++)
                {
                    var col = all[i];
                    if (col == null) continue;
                    if (col.transform == hitTarget) continue;
                    ApplyDamageToTransform(col.transform, aoeDmg);
                }
            }
        }

        // Lv10：若有剩余弹射次数，则从命中点寻找新目标并弹射（不会覆盖上面的伤害/AoE）
        if (_remainingBounces > 0 && _bounceRange > 0f)
        {
            Transform bounce = FindBounceTarget(hitPoint, _bounceRange);
            if (bounce != null)
            {
                // 克隆当前弹丸并初始化为新的目标，从命中点发射
                // 使用视觉点作为克隆生成点（更贴近艺术期望）
                var clone = Instantiate(this.gameObject, vfxPos, Quaternion.identity);
                var newProj = clone.GetComponent<ArcaneMissileProjectile>();
                if (newProj != null)
                {
                    newProj.Initialize(target: bounce,
                                       speed: _speed,
                                       damage: _damage,
                                       hitRadius: _hitRadius,
                                       explosionPrefab: _explosionPrefab,
                                       enemyLayers: _enemyLayers,
                                       aoeRadius: _aoeRadius,
                                       aoePercent: _aoePercent,
                                       bounceRange: _bounceRange,
                                       remainingBounces: Mathf.Max(0, _remainingBounces - 1),
                                       casterState: _cs);
                }
            }
        }

        // 本弹丸完成使命
        Destroy(gameObject);
    }

    // 查找弹射目标：使用命中点作为圆心进行 OverlapSphere 检索，返回最近的非 origin（通过 Transform 对比）
    private Transform FindBounceTarget(Vector3 originPoint, float range)
    {
        int found = Physics.OverlapSphereNonAlloc(originPoint, range, _overlapBuffer, _enemyLayers, QueryTriggerInteraction.Collide);
        Transform best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < found; i++)
        {
            var c = _overlapBuffer[i];
            if (c == null) continue;
            float dSqr = (c.bounds.center - originPoint).sqrMagnitude;
            if (dSqr < bestSqr)
            {
                bestSqr = dSqr;
                best = c.transform;
            }
        }
        if (found == _overlapBuffer.Length)
        {
            var all = Physics.OverlapSphere(originPoint, range, _enemyLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null) continue;
                float dSqr = (c.bounds.center - originPoint).sqrMagnitude;
                if (dSqr < bestSqr)
                {
                    bestSqr = dSqr;
                    best = c.transform;
                }
            }
        }
        return best;
    }

    private void ApplyDamageToTransform(Transform t, float dmg)
    {
        if (t == null) return;
        if (_cs != null)
        {
            _cs.DealDamageTo(t, dmg);
            return;
        }
        // 回退：兼容直接调用 IDamageable/MonsterCombat
        var damageable = t.GetComponent<IDamageable>();
        if (damageable == null && t.parent != null) damageable = t.parent.GetComponent<IDamageable>();
        if (damageable != null)
        {
            int iv = Mathf.Max(0, Mathf.RoundToInt(dmg));
            damageable.TakeDamage(iv, AttackType.魔法攻击);
        }
        else
        {
            // 通过MonsterCombat组件应用伤害，而不是直接调用MonsterBase的TakeDamage
            var mc = t.GetComponent<MonsterCombat>();
            if (mc == null && t.parent != null) mc = t.parent.GetComponent<MonsterCombat>();
            if (mc != null)
            {
                int iv = Mathf.Max(0, Mathf.RoundToInt(dmg));
                mc.TakeDamage(iv);
            }
        }
    }
}