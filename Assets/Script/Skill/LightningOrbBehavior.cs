using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 闪电护盾的电球行为：
/// - 围绕 caster 在水平面绕转，半径/角速度/持续时间可配；
/// - 电球高度使用高度偏移；
/// - 接触敌人（半径）时对其造成伤害，单个目标在 perTargetHitInterval 内不会重复受击；
/// - 使用玩家的 CharacterState 统一造成伤害（支持暴击）。
/// </summary>
public class LightningOrbBehavior : MonoBehaviour
{
    private Transform _caster;
    private float _angleDeg;
    private float _radius;
    private float _angularSpeedDeg;
    private float _duration;
    private float _heightOffset;
    private LayerMask _enemyLayers;
    private float _contactRadius;
    private float _perTargetHitInterval;
    private int _damage;

    // 新增：用于统一伤害路径
    private CharacterState _cs;

    // 目标Transform -> 下次可受击时间（使用Transform以减少重复命中的碰撞体差异）
    private readonly Dictionary<Transform, float> _nextHitTime = new();

    // 非分配重用缓冲
    private static readonly Collider[] _buffer = new Collider[32];

    private float _lifeTimer;

    public void Init(Transform caster,
                     float startAngleDeg,
                     float radius,
                     float angularSpeedDeg,
                     float duration,
                     float heightOffset,
                     LayerMask enemyLayers,
                     float contactRadius,
                     float perTargetHitInterval,
                     int damage,
                     CharacterState casterState)
    {
        _caster = caster;
        _angleDeg = startAngleDeg;
        _radius = Mathf.Max(0.1f, radius);
        _angularSpeedDeg = angularSpeedDeg;
        _duration = Mathf.Max(0.1f, duration);
        _heightOffset = heightOffset;
        _enemyLayers = enemyLayers;
        _contactRadius = Mathf.Max(0.05f, contactRadius);
        _perTargetHitInterval = Mathf.Max(0.05f, perTargetHitInterval);
        _damage = Mathf.Max(0, damage);
        _cs = casterState;

        UpdatePosition();
    }

    private void Update()
    {
        if (_caster == null)
        {
            Destroy(gameObject);
            return;
        }

        _lifeTimer += Time.deltaTime;
        if (_lifeTimer >= _duration)
        {
            Destroy(gameObject);
            return;
        }

        // 旋转更新
        _angleDeg += _angularSpeedDeg * Time.deltaTime;
        UpdatePosition();

        // 接触伤害
        DoContactDamage();
    }

    private void UpdatePosition()
    {
        if (_caster == null) return;
        var dir = Quaternion.Euler(0f, _angleDeg, 0f) * Vector3.forward;
        var center = _caster.position + Vector3.up * _heightOffset;
        transform.position = center + dir * _radius;
    }

    private void DoContactDamage()
    {
        int found = Physics.OverlapSphereNonAlloc(transform.position, _contactRadius, _buffer, _enemyLayers, QueryTriggerInteraction.Ignore);
        float now = Time.time;
        for (int i = 0; i < found; i++)
        {
            var col = _buffer[i];
            if (col == null) continue;
            // 优先取 IDamageable 所在 Transform 作为目标根节点
            Transform targetRoot = col.GetComponentInParent<IDamageable>() is Component comp ? comp.transform : col.transform;

            if (!_nextHitTime.TryGetValue(targetRoot, out var nextTime) || now >= nextTime)
            {
                _nextHitTime[targetRoot] = now + _perTargetHitInterval;
                if (_cs != null)
                {
                    _cs.DealDamageTo(targetRoot, _damage);
                }
                else
                {
                    var dmg = targetRoot.GetComponent<IDamageable>();
                    if (dmg != null) dmg.TakeDamage(_damage,AttackType.魔法攻击);
                }
            }
        }
    }
}
