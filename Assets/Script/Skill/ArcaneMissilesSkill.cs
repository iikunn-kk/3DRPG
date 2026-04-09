using System.Collections;
using UnityEngine;
/// <summary>
/// 奥术飞弹（Arcane Missiles）技能逻辑：
/// - 通过施法动画中的关键帧事件触发实际发射（CharacterAnimationController.OnSkillCastPoint）
/// - 如找不到动画控制器或未触发事件，将回退为立即发射（避免挂起）
/// - 立即发射3枚追踪飞弹，间隔0.2秒（可配置）
/// - 每枚飞弹命中时对目标造成伤害，伤害来源于 PlayerSkill.GetDamage()
/// - Lv5：命中时对目标周围敌人造成额外的 AoE 伤害（百分比，基于主目标伤害）
/// - Lv10：在保留 Lv5 的前提下，飞弹命中后会弹射至另一个附近敌人（若有）
/// - 弹射的飞弹不再继续弹射（仅1次）
/// - 伤害计算与应用均通过 IDamageable 接口完成
/// - 本实现要求提供 projectilePrefab（含 ArcaneMissileProjectile）
/// - 发射序列结束后销毁本技能实例
/// </summary>
public class ArcaneMissilesSkill : Skill
{
    [Header("Projectile & Hit")]
    [Tooltip("优先用于发射的弹丸预制体；该预制体应包含 ArcaneMissileProjectile 脚本以处理追踪/爆炸逻辑")]
    [SerializeField] private GameObject projectilePrefab; // 弹丸 Prefab（包含移动/碰撞/爆炸逻辑）
    [Tooltip("命中时播放的爆炸粒子（可选），如果不为空会传递给 projectilePrefab 使用")]
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private float hitRadius = 0.3f;         // 命中判定半径（备用，但优先用弹丸逻辑）
    [SerializeField] private LayerMask enemyLayers;           // 敌人层（用于范围与弹射筛选）

    [Header("等级解锁效果配置 (Lv5 / Lv10)")]
    [Tooltip("Lv5：爆炸半径（米）")]
    [SerializeField] private float aoeRadiusLv5 = 2.5f;
    [Tooltip("Lv5：范围伤害占单体伤害的百分比(0-1)")]
    [Range(0f, 1f)] [SerializeField] private float aoePercentLv5 = 0.5f;
    [Tooltip("Lv10：弹射范围（米）")]
    [SerializeField] private float bounceRangeLv10 = 6f;

    private Transform _target;
    private Transform _firePoint; // 由控制器注入的发射点

    // 动画事件集成
    private CharacterAnimationController _animCtrl;
    private bool _firedViaEvent;

    public override void SetTarget(Transform target)
    {
        _target = target;
    }

    public override void SetFirePoint(Transform firePoint)
    {
        _firePoint = firePoint;
    }

    public override void Execute(Transform caster, PlayerSkill playerSkill)
    {
        base.Execute(caster, playerSkill);

        // 施法类：等待动画事件触发发射
        _animCtrl = caster != null ? caster.GetComponent<CharacterAnimationController>() : null;
        if (_animCtrl != null)
        {
            _animCtrl.SkillCastPointReached += OnSkillCastPointReached;
            // 为避免极端情况下事件缺失导致挂起，增加一个轻量兜底协程：若若干秒内未触发，则回退为立即发射
            StartCoroutine(FallbackCastPointTimeout(3.0f));
        }
        else
        {
            // 找不到动画控制器：立即发射（兜底）
            StartCoroutine(FireSequence());
        }
        Debug.LogWarning("我出生了");
    }

    private IEnumerator FallbackCastPointTimeout(float timeout)
    {
        float t = 0f;
        while (!_firedViaEvent && t < timeout)
        {
            t += Time.deltaTime;
            yield return null;
        }
        if (!_firedViaEvent)
        {
            // 超时兜底：直接发射，防止技能卡住
            StartCoroutine(FireSequence());
            SafeUnsubscribe();
        }
    }

    private void OnSkillCastPointReached()
    {
        if (_firedViaEvent) return;
        _firedViaEvent = true;
        SafeUnsubscribe();
        StartCoroutine(FireSequence());
    }

    private void SafeUnsubscribe()
    {
        if (_animCtrl != null)
        {
            _animCtrl.SkillCastPointReached -= OnSkillCastPointReached;
        }
    }

    private void OnDisable()
    {
        SafeUnsubscribe();
    }

    private IEnumerator FireSequence()
    {
        if (PlayerSkill == null || PlayerSkill.SkillSO == null) { Destroy(gameObject); yield break; }
        var so = PlayerSkill.SkillSO;

        if (_target == null)
        {
            Debug.LogWarning("ArcaneMissilesSkill: 未提供目标，技能取消。");
            Destroy(gameObject);
            yield break;
        }

        // 发射序列开始时播放一次发射音效
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayWeaponSound(SkillSoundType.奥术飞弹发射);
        }

        int count = 3;
        float interval = Mathf.Max(0f, so.missileInterval);
        float speed = so.projectileSpeed > 0 ? so.projectileSpeed : 12f;

        for (int i = 0; i < count; i++)
        {
            if (_target == null) break;

            if (projectilePrefab != null)
            {
                Vector3 spawnPos = (_firePoint != null) ? _firePoint.position : Caster.position + Vector3.up * 1.2f;
                var go = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
                var proj = go.GetComponent<ArcaneMissileProjectile>();
                if (proj != null)
                {
                    proj.Initialize(target: _target,
                                    speed: speed,
                                    damage: Mathf.Max(0, Mathf.RoundToInt(PlayerSkill.GetDamage())),
                                    hitRadius: hitRadius,
                                    explosionPrefab: explosionPrefab,
                                    enemyLayers: enemyLayers,
                                    aoeRadius: aoeRadiusLv5,
                                    aoePercent: aoePercentLv5,
                                    bounceRange: bounceRangeLv10,
                                    remainingBounces: PlayerSkill.Level >= 10 ? 1 : 0,
                                    casterState: CasterState);
                }
                else
                {
                    Debug.LogWarning("ArcaneMissilesSkill: projectilePrefab 缺少 ArcaneMissileProjectile 组件，已销毁该实例。");
                    Destroy(go);
                }
            }
            else
            {
                Debug.LogWarning("ArcaneMissilesSkill: 未提供 projectilePrefab，技能将不会产生弹丸。请在 Inspector 中分配 projectilePrefab。");
            }

            if (i < count - 1 && interval > 0f)
                yield return new WaitForSeconds(interval);
        }

        Destroy(gameObject);
    }
}
