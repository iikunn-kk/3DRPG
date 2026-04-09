using System.Collections;
using UnityEngine;

/// <summary>
/// 连环踢Lv10：旋风行为。
/// - 存活duration秒；
/// - 若范围内有敌人，朝最近敌人移动；否则四处游荡；
/// - 当与敌人接触（接近）时，每秒对其造成一次伤害（伤害值由外部Init传入）；
/// - 可选：使用简单的OverlapSphere做“接触”判断。
/// </summary>
public class WhirlwindBehavior : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float wanderTurnInterval = 1.2f;
    [SerializeField] private float wanderTurnAngle = 60f;

    [Header("Detection & Damage")]
    [SerializeField] private float detectRadius = 6f;
    [SerializeField] private float contactRadius = 1.0f;

    private LayerMask _enemyLayers;
    private int _damagePerTick;
    private float _duration;

    private float _wanderTimer;
    private Vector3 _wanderDir;
    private bool _started;

    // 新增：统一伤害路径用
    private CharacterState _cs;

    // 非分配缓冲
    private static readonly Collider[] Buffer = new Collider[32];

    // Message-based configuration API
    public void SetLayerMask(int mask) { _enemyLayers = mask; }
    public void SetDamagePerSecond(int dps) { _damagePerTick = Mathf.Max(0, dps); }
    public void SetDuration(float dur) { _duration = Mathf.Max(0.1f, dur); }
    public void SetCasterState(CharacterState cs) { _cs = cs; }
    public void Begin()
    {
        if (_started) return;
        _wanderDir = Random.insideUnitSphere; _wanderDir.y = 0f; if (_wanderDir.sqrMagnitude < 0.01f) _wanderDir = Vector3.forward;
        StartCoroutine(LifetimeRoutine());
        StartCoroutine(DamageRoutine());
        _started = true;
    }

    private void Update()
    {
        // 选择移动方向：若有最近敌人则朝向，否则游荡
        Transform target = FindNearestEnemy(detectRadius);
        Vector3 dir;
        if (target != null)
        {
            dir = (target.position - transform.position); dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                dir.Normalize();
                _wanderDir = dir; // 记住方向，避免每帧抖动
            }
            dir = _wanderDir;
        }
        else
        {
            // 游荡：定期改变方向
            _wanderTimer -= Time.deltaTime;
            if (_wanderTimer <= 0f)
            {
                _wanderTimer = wanderTurnInterval;
                _wanderDir = Quaternion.Euler(0f, Random.Range(-wanderTurnAngle, wanderTurnAngle), 0f) * _wanderDir;
                if (_wanderDir.sqrMagnitude < 0.01f) _wanderDir = Vector3.forward;
                _wanderDir.Normalize();
            }
            dir = _wanderDir;
        }

        transform.position += dir * (moveSpeed * Time.deltaTime);
        if (dir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 0.2f);
        }
    }

    private void UpdateDamageOnNearestInRadius(float radius)
    {
        int found = Physics.OverlapSphereNonAlloc(transform.position, radius, Buffer, _enemyLayers, QueryTriggerInteraction.Ignore);
        Transform victim = null; float best = float.MaxValue;
        for (int i = 0; i < found; i++)
        {
            var c = Buffer[i];
            if (c == null) continue;
            float d = (c.transform.position - transform.position).sqrMagnitude;
            if (d < best)
            {
                best = d; victim = c.transform;
            }
        }
        if (victim != null && _damagePerTick > 0)
        {
            if (_cs != null)
            {
                _cs.DealDamageTo(victim, _damagePerTick);
            }
            else
            {
                var damageable = victim.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(_damagePerTick, AttackType.物理攻击);
                }
            }
        }
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(_duration);
        Destroy(gameObject);
    }

    private IEnumerator DamageRoutine()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            // 每秒寻找“接触”范围内的一个敌人并造成伤害（若有多个，这里取最近一个）
            UpdateDamageOnNearestInRadius(contactRadius);
            yield return wait;
        }
    }

    private Transform FindNearestEnemy(float radius)
    {
        int found = Physics.OverlapSphereNonAlloc(transform.position, radius, Buffer, _enemyLayers, QueryTriggerInteraction.Ignore);
        Transform nearest = null; float best = float.MaxValue;
        for (int i = 0; i < found; i++)
        {
            var c = Buffer[i];
            if (c == null) continue;
            float d = (c.transform.position - transform.position).sqrMagnitude;
            if (d < best)
            {
                best = d; nearest = c.transform;
            }
        }
        return nearest;
    }
}
