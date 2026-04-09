using System.Collections;
using UnityEngine;

/// <summary>
/// 闪电护盾：
/// - 在自身周围召唤三个闪电球，围绕自身旋转，触碰敌人造成伤害。
/// - Lv5：在召唤电球时，对自身位置落下雷击，造成一次范围伤害。
/// - Lv10：改为在玩家正前方依次落下三道雷击（有短暂延迟），每道造成范围伤害。
/// 说明：
/// - 不需要目标；
/// - 冷却由 SkillController 正常设置；
/// - 伤害与半径等可在Inspector中调整；
/// - 电球的视觉由 orbPrefab 提供（可选）；strikeVfxPrefab 为雷击视觉（可选）。
/// </summary>
public class LightningShieldSkill : Skill
{
    [Header("Orbs")]
    [SerializeField] private GameObject orbPrefab;               // 可选：电球的外观Prefab
    [SerializeField] private int orbCount = 3;                   // 电球数量
    [SerializeField] private float orbitRadius = 1.4f;           // 电球绕转半径
    [SerializeField] private float orbitAngularSpeed = 180f;     // 角速度（度/秒）
    [SerializeField] private float orbDuration = 8f;             // 电球持续时间
    [SerializeField] private float orbContactRadius = 0.6f;      // 电球接触伤害半径
    [SerializeField] private float perTargetHitInterval = 0.6f;  // 同一目标再次受击的冷却
    [SerializeField] private float orbHeightOffset = 1.0f;       // 电球在角色身上的高度偏移

    [Header("Lightning Strike (Lv5/Lv10)")]
    [SerializeField] private GameObject strikeVfxPrefab;         // 可选：雷击特效
    [SerializeField] private float strikeRadius = 2.2f;          // 雷击范围半径
    [SerializeField] private int strikeCountLv10 = 3;            // Lv10：雷击次数
    [SerializeField] private float strikeForwardSpacing = 1.8f;  // Lv10：每道雷击之间的前向间距
    [SerializeField] private float strikeDelayBetween = 0.2f;    // Lv10：每道雷击之间的延迟

    [Header("Common")]
    [SerializeField] private LayerMask enemyLayers;              // 敌人层

    // 性能：重用物理查询缓冲区，避免频繁分配
    private const int DefaultOverlapBufferSize = 32;
    private readonly Collider[] _overlapBuffer = new Collider[DefaultOverlapBufferSize];
    // 大缓冲作为回退非分配扫描
    private static readonly Collider[] _largeBuffer = new Collider[128];

    public override void Execute(Transform caster, PlayerSkill playerSkill)
    {
        base.Execute(caster, playerSkill);
        if (Caster == null || PlayerSkill == null || PlayerSkill.SkillSO == null)
        {
            Destroy(gameObject);
            return;
        }

        // 播放闪电护盾释放音效
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayWeaponSound(SkillSoundType.闪电护盾释放);
        }

        // 计算本次技能的基础伤害快照
        int damage = Mathf.Max(0, Mathf.RoundToInt(PlayerSkill.GetDamage()));

        // 生成电球
        SpawnOrbs(damage);

        // 触发雷击（Lv5/Lv10）
        if (PlayerSkill.Level >= 10)
        {
            StartCoroutine(DoForwardStrikes(damage));
        }
        else if (PlayerSkill.Level >= 5)
        {
            DoSingleStrikeAtSelf(damage);
        }

        // 技能主体在电球结束后销毁
        StartCoroutine(SelfDestructAfter(orbDuration + 0.1f));
    }

    private void SpawnOrbs(int damage)
    {
        int count = Mathf.Max(1, orbCount);
        for (int i = 0; i < count; i++)
        {
            float startAngle = (360f / count) * i; // 均分起始角
            GameObject orbGo;
            if (orbPrefab != null)
            {
                orbGo = Instantiate(orbPrefab, Caster.position, Quaternion.identity, transform);
            }
            else
            {
                orbGo = new GameObject($"LightningOrb_{i+1}");
                orbGo.transform.SetParent(transform);
                orbGo.transform.position = Caster.position;
            }
            var orb = orbGo.GetComponent<LightningOrbBehavior>();
            if (orb == null)
            {
                orb = orbGo.AddComponent<LightningOrbBehavior>();
            }
            orb.Init(Caster,
                     startAngle,
                     orbitRadius,
                     orbitAngularSpeed,
                     orbDuration,
                     orbHeightOffset,
                     enemyLayers,
                     orbContactRadius,
                     perTargetHitInterval,
                     damage,
                     CasterState);
        }
    }

    private void DoSingleStrikeAtSelf(int damage)
    {
        Vector3 pos = Caster.position;
        if (strikeVfxPrefab != null)
        {
            var vfx = Instantiate(strikeVfxPrefab, pos, Quaternion.identity);
            Destroy(vfx, 3f);
        }
        DoAoEDamage(pos, strikeRadius, damage);
    }

    private IEnumerator DoForwardStrikes(int damage)
    {
        int count = Mathf.Max(1, strikeCountLv10);
        float spacing = Mathf.Max(0.5f, strikeForwardSpacing);
        float delay = Mathf.Max(0f, strikeDelayBetween);
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = Caster.position + Caster.forward * ((i + 1) * spacing);
            if (strikeVfxPrefab != null)
            {
                var vfx = Instantiate(strikeVfxPrefab, pos, Quaternion.identity);
                Destroy(vfx, 3f);
            }
            DoAoEDamage(pos, strikeRadius, damage);
            if (i < count - 1 && delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
        }
    }

    private void DoAoEDamage(Vector3 center, float radius, int damage)
    {
        // 使用 OverlapSphereNonAlloc 减少 GC 分配
        int found = Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer, enemyLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < found; i++)
        {
            var col = _overlapBuffer[i];
            if (col == null) continue;
            DealDamage(col.transform, damage, false);
        }

        // 极端情况下缓冲区可能被填满，作为保险使用一次更大缓冲的非分配版本
        if (found == _overlapBuffer.Length)
        {
            int foundLarge = Physics.OverlapSphereNonAlloc(center, radius, _largeBuffer, enemyLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < foundLarge; i++)
            {
                var col = _largeBuffer[i];
                if (col == null) continue;
                DealDamage(col.transform, damage, false);
            }
        }
    }

    private IEnumerator SelfDestructAfter(float t)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, t));
        if (this != null && gameObject != null)
        {
            Destroy(gameObject);
        }
    }
}
