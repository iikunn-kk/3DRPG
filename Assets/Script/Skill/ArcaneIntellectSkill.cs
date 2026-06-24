using UnityEngine;

/// <summary>
/// 奥术智慧（Arcane Intellect）技能入口：
/// - 立即在玩家身上应用/刷新 ArcaneIntellectBuff（持续30分钟）；
/// - 新的施放会顶掉旧的；
/// - 数值来源：SkillSO.buffValue + perLevelBuffValue * Lv（视为"攻击力百分比加成"的数值，单位%）。
/// - 施放后本Skill预制体可立即销毁，不驻留场景。
/// </summary>
public class ArcaneIntellectSkill : Skill
{
    [Tooltip("Buff持续时长（秒），默认30分钟")]
    [SerializeField]
    private float durationSeconds = 30f * 60f;

    [Header("视觉（在Skill的Prefab上配置）")]
    [Tooltip("奥术智慧显示的Buff粒子（配置在Skill prefab的Inspector）")]
    [SerializeField] private GameObject arcaneVfxPrefab;

    public override void Execute(Transform caster, PlayerSkill playerSkill)
    {
        base.Execute(caster, playerSkill);
        if (caster == null || playerSkill == null || playerSkill.SkillSO == null)
        {
            Destroy(gameObject);
            return;
        }

        // 播放共享 Buff 释放音效（与回春术共用）
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayWeaponSound(SkillSoundType.Buff释放);
        }

        var cs = CharacterService.Instance?.CurrentPlayerCharacter();
        if (cs == null)
        {
            Debug.LogWarning("ArcaneIntellectSkill: 未找到玩家角色");
            Destroy(gameObject);
            return;
        }

        // 确保CharacterBuffs组件存在
        var buffs = cs.GetComponent<CharacterBuffs>();
        if (buffs == null)
            buffs = cs.gameObject.AddComponent<CharacterBuffs>();

        // 来源：如果可用，使用SkillID作为稳定标识符
        string source = playerSkill.SkillSO != null && !string.IsNullOrEmpty(playerSkill.SkillSO.SkillID)
            ? playerSkill.SkillSO.SkillID
            : "ArcaneIntellect";

        // 根据SkillSO计算攻击百分比
        float attackPercent = SkillManager.Instance != null
            ? SkillManager.Instance.GetBuffValueAtLevel(playerSkill.SkillSO, playerSkill.Level)
            : (playerSkill.SkillSO.buffValue + playerSkill.SkillSO.perLevelBuffValue * playerSkill.Level);

        // 基于当前基础攻击计算baseAttackAdd（通过从Attack中减去现有的AttackFlat来避免递归）
        int baseAtk = Mathf.Max(0, cs.Attack - buffs.GetAttackFlatTotal());
        int baseAttackAdd = Mathf.Max(0, Mathf.RoundToInt(baseAtk * (attackPercent / 100f)));

        // 通过CharacterBuffs应用主要数值buff
        if (baseAttackAdd > 0)
            buffs.ApplyBuff(source, CharacterBuffs.BuffType.AttackFlat, baseAttackAdd, durationSeconds);
        if (playerSkill.Level >= 5)
            buffs.ApplyBuff(source, CharacterBuffs.BuffType.CritPercent, 5f, durationSeconds);

        // 从技能预制体注册视觉效果（技能预制体具有VFX引用）
        if (arcaneVfxPrefab != null)
        {
            buffs.RegisterBuffVisual(source, arcaneVfxPrefab);
        }

        // 确保ArcaneIntellectRuntime存在于玩家上并设置/刷新以管理Lv10加倍和生命周期
        var runtime = cs.GetComponent<ArcaneIntellectRuntime>();
        if (runtime == null)
        {
            runtime = cs.gameObject.AddComponent<ArcaneIntellectRuntime>();
            runtime.Setup(source, baseAttackAdd, playerSkill.Level, durationSeconds, cs, buffs);
        }
        else
        {
            runtime.Refresh(baseAttackAdd, playerSkill.Level, durationSeconds);
        }

        Destroy(gameObject);
    }
}