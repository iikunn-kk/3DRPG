using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class SkillManager : Singleton<SkillManager>
{
    private Dictionary<string, SkillSO> _skillDatabase;

    // 新增：引用包含所有技能的ScriptableObject
    [SerializeField] private AllSkillsSO allSkillsData;

    // 当前玩家（当前角色）的技能快照（由本管理器负责重建与维护）
    private readonly Dictionary<string, PlayerSkill> _currentPlayerSkills = new();

    // 当当前玩家技能被（重新）初始化/重建 时广播：参数为快照（只读视图）
    public event Action<IReadOnlyDictionary<string, PlayerSkill>> PlayerSkillsInitialized;
    
    [SerializeField] private SkillUpgradedEventSO skillUpgradedEvent;
    protected override void Awake()
    {
        base.Awake();
        LoadSkills();
    }

    private void LoadSkills()
    {
        _skillDatabase = new Dictionary<string, SkillSO>();
        // 从AllSkillsSO加载所有技能
        if (allSkillsData != null && allSkillsData.allSkills != null)
        {
            foreach (var skillSo in allSkillsData.allSkills)
            {
                if (skillSo != null && !_skillDatabase.ContainsKey(skillSo.SkillID))
                {
                    _skillDatabase.Add(skillSo.SkillID, skillSo);
                }
                else if (skillSo != null)
                {
                    Debug.LogWarning($"重复的技能ID: {skillSo.SkillID} 在 {skillSo.name} 和 {_skillDatabase[skillSo.SkillID].name} 中。");
                }
            }
        }
        else
        {
            Debug.LogError("未分配AllSkillsSO或技能列表为空！");
        }
    }

    /// <summary>
    /// 根据技能ID获取技能的ScriptableObject。
    /// </summary>
    /// <param name="skillID">技能的唯一ID。</param>
    /// <returns>如果找到，返回SkillSO；否则返回null。</returns>
    public SkillSO GetSkillSo(string skillID)
    {
        _skillDatabase.TryGetValue(skillID, out var skillSo);
        return skillSo;
    }

    /// <summary>
    /// 获取所有技能 ScriptableObjects。
    /// </summary>
    /// <returns>该职业的技能SO列表。</returns>
    public List<SkillSO> GetSkillsForProfession()
    {
        return _skillDatabase.Values.ToList();
    }

    // ============ 新增：由管理器负责构建"当前玩家"的技能快照，并广播 ============
    /// <summary>
    /// 重建当前玩家技能快照。
    /// </summary>
    public void RebuildCurrentPlayerSkillsFromGame()
    {
        _currentPlayerSkills.Clear();
        var characterState = GameManager.Instance?.CurrentPlayerCharacter();
        var data = characterState?.PlayerCharacterData;
        
        if (data != null)
        {
            // 如果角色的技能数据为空或null，自动初始化全部技能为1级
            if (data.skills == null || data.skills.Count == 0)
            {
                data.skills = new List<SkillSaveData>();
                var allSkills = GetSkillsForProfession();
                foreach (var skillSo in allSkills)
                {
                    data.skills.Add(new SkillSaveData(skillSo.SkillID, 1));
                }
            }

            // 构建技能快照
            foreach (var sd in data.skills)
            {
                var so = GetSkillSo(sd.SkillID);
                if (so != null)
                {
                    _currentPlayerSkills[sd.SkillID] = new PlayerSkill(so, sd.Level);
                }
                else
                {
                    Debug.LogWarning($"SkillManager: 未找到SkillSO: {sd.SkillID}");
                }
            }
        }
        // 广播给订阅者（如 SkillController / UI）
        PlayerSkillsInitialized?.Invoke(_currentPlayerSkills);
    }

    public IReadOnlyDictionary<string, PlayerSkill> GetCurrentPlayerSkillsSnapshot() => _currentPlayerSkills;

    #region 升级规则与公式
    /// <summary>
    /// 计算"当前等级"的冷却时间。
    /// 普通攻击：恒为 so.cooldown；
    /// 其他技能：cooldown(level) = so.cooldown * (1 - perLevelCooldownReducePercent * level)
    /// </summary>
    public float GetCooldownAtLevel(SkillSO so, int level)
    {
        if (so.skillType == SkillEffectType.普通攻击) return so.cooldown;
        float factor = Mathf.Max(0f, 1f - so.perLevelCooldownReducePercent * level);
        return so.cooldown * factor;
    }
    public float GetBaseDamageAtLevel(SkillSO so, int level) => so.baseDamage * (1f + so.perLevelBaseDamagePercent * level);

    /// <summary>
    /// 计算"当前等级"的攻击力百分比加成（%）。
    /// 规则：AtkScale%(level) = baseAttackScalePercent + perLevelAttackScalePercent * level
    /// </summary>
    public float GetAttackScalePercentAtLevel(SkillSO so, int level) => so.baseAttackScalePercent + so.perLevelAttackScalePercent * level;

    public float GetHealAtLevel(SkillSO so, int level) => so.baseHealAmount * (1f + so.perLevelHealAmountPercent * level);
    public float GetBuffValueAtLevel(SkillSO so, int level) => so.buffValue + so.perLevelBuffValue * level;

    /// <summary>
    /// 升级到"下一等级"所需的角色等级（规则：nextLevel * 5）。
    /// </summary>
    public int GetRequiredPlayerLevelForNext(int nextLevel) => nextLevel * 5;

    /// <summary>
    /// 升级到"指定等级"的金币消耗（指数增长）：
    /// cost(level) = round(100 * 1.2^(level - 1))。
    /// 注意：这里的 level 通常传入"下一等级"。
    /// </summary>
    public int GetUpgradeCostForLevel(int level) => (int)Math.Round(100d * Math.Pow(1.2d, Math.Max(0, level - 1)), MidpointRounding.AwayFromZero);

    /// <summary>
    /// 获取关键等级（Lv5/Lv10）的解锁说明文案。
    /// </summary>
    public string GetSpecialUnlockNote(SkillSO so, int level) => level == 5 ? so.specialUnlockNoteLv5 : (level == 10 ? so.specialUnlockNoteLv10 : string.Empty);

    /// <summary>
    /// 生成"升级预览"数据包。
    /// </summary>
    public bool TryGetUpgradePreview(PlayerSkill ps, out SkillUpgradePreview preview, out string reason)
    {
        preview = default;
        reason = string.Empty;
        var so = ps.SkillSO;
        int curLv = ps.Level;
        if (curLv >= so.maxLevel)
        {
            reason = "已达最大等级";
            return false;
        }

        int nextLv = curLv + 1;
        
        // 注意：这里的伤害计算是"基础"预览，不包含角色实时攻击力
        float currentBaseDamage = GetBaseDamageAtLevel(so, curLv);
        float nextBaseDamage = GetBaseDamageAtLevel(so, nextLv);
        float currentAttackScale = GetAttackScalePercentAtLevel(so, curLv);
        float nextAttackScale = GetAttackScalePercentAtLevel(so, nextLv);
        
        // 伤害提升百分比可以基于一个假定的基础攻击力来估算，这里用100作为参考值
        float currentTotalDamage = currentBaseDamage + 100 * (currentAttackScale / 100f);
        float nextTotalDamage = nextBaseDamage + 100 * (nextAttackScale / 100f);

        float curCd = GetCooldownAtLevel(so, curLv);
        float nextCd = GetCooldownAtLevel(so, nextLv);
        float dmgInc = currentTotalDamage <= 0 ? 0 : (nextTotalDamage - currentTotalDamage) / currentTotalDamage * 100f;
        float cdRed = curCd <= 0 ? 0 : (curCd - nextCd) / curCd * 100f;

        preview = new SkillUpgradePreview
        {
            SkillID = so.SkillID,
            DisplayName = so.skillName.ToString(),
            Icon = so.icon,
            CurrentLevel = curLv,
            NextLevel = nextLv,
            MaxLevel = so.maxLevel,
            CurrentBaseDamage = currentBaseDamage,
            NextBaseDamage = nextBaseDamage,
            CurrentAttackScalePercent = currentAttackScale,
            NextAttackScalePercent = nextAttackScale,
            CurrentCooldown = curCd,
            NextCooldown = nextCd,
            CurrentHealAmount = GetHealAtLevel(so, curLv),
            NextHealAmount = GetHealAtLevel(so, nextLv),
            CurrentBuffValue = GetBuffValueAtLevel(so, curLv),
            NextBuffValue = GetBuffValueAtLevel(so, nextLv),
            CurrentTotalDamage = currentTotalDamage, // 仅用于计算百分比，非实际伤害
            NextTotalDamage = nextTotalDamage,     // 仅用于计算百分比，非实际伤害
            DamageIncreasePercent = dmgInc,
            CooldownReducePercent = cdRed,
            Cost = GetUpgradeCostForLevel(nextLv),
            RequiredPlayerLevel = GetRequiredPlayerLevelForNext(nextLv),
            SpecialUnlockNote = GetSpecialUnlockNote(so, nextLv),
            AttackTypeText = so.attackType.ToString()
        };
        return true;
    }

    /// <summary>
    /// 尝试升级技能。
    /// </summary>
    public bool TryUpgradeSkill(PlayerSkill ps, CharacterData characterData, int playerLevel, out string failReason)
    {
        failReason = string.Empty;
        var so = ps.SkillSO;
        int curLv = ps.Level;
        if (curLv >= so.maxLevel)
        {
            failReason = "已达最大等级";
            return false;
        }

        int nextLv = curLv + 1;
        int cost = GetUpgradeCostForLevel(nextLv);
        int reqLv = GetRequiredPlayerLevelForNext(nextLv);
        if (playerLevel < reqLv)
        {
            failReason = $"需要角色等级达到 {reqLv}";
            return false;
        }

        if (!PlayerCurrencyManager.Instance.RemoveMoney(cost))
        {
            failReason = "金币不足";
            return false;
        }

        ps.UpgradeLevel();

        // 确保写入到可持久化的 CharacterData 中
        var data = characterData ?? GameManager.Instance?.CurrentCharacter;
        if (data == null)
        {
            // 无法获取到角色数据（理论上不应该发生），返回失败以便上层处理
            failReason = "角色数据不可用，无法保存技能等级";
            return false;
        }

        if (data.skills == null) data.skills = new List<SkillSaveData>();
        var sd = data.skills.FirstOrDefault(s => s.SkillID == ps.SkillSO.SkillID);
        if (sd != null)
        {
            sd.Level = ps.Level;
        }
        else
        {
            data.skills.Add(new SkillSaveData(ps.SkillSO.SkillID, ps.Level));
        }

        // 同步到运行时 PlayerCharacterData（如果存在），保证 UI/运行时逻辑读取到最新等级
        var runtimePlayer = GameManager.Instance?.CurrentPlayerCharacter();
        var runtimeData = runtimePlayer?.PlayerCharacterData;
        if (runtimeData != null)
        {
            if (runtimeData.skills == null) runtimeData.skills = new List<SkillSaveData>();
            var rsd = runtimeData.skills.FirstOrDefault(s => s.SkillID == ps.SkillSO.SkillID);
            if (rsd != null)
            {
                rsd.Level = ps.Level;
            }
            else
            {
                runtimeData.skills.Add(new SkillSaveData(ps.SkillSO.SkillID, ps.Level));
            }
        }

        SkillUpgradedPayload payload = new SkillUpgradedPayload
        {
            SkillID = ps.SkillSO.SkillID,
            NewLevel = ps.Level
        };
        skillUpgradedEvent.RaiseEvent(payload,this);
        // 升级后刷新当前玩家技能快照并广播（让UI/控制器同步）
        RebuildCurrentPlayerSkillsFromGame();
        GameManager.Instance.SaveCurrentCharacterData();
        return true;
    }

    /// <summary>
    /// 尝试升级技能（UI专用）。
    /// </summary>
    public bool TryUpgradeSkill(string skillID, out string failReason)
    {
        var characterState = GameManager.Instance?.CurrentPlayerCharacter();
        var data = characterState?.PlayerCharacterData;
        int playerLevel = data?.level ?? 0;

        failReason = string.Empty;
        if (!_currentPlayerSkills.TryGetValue(skillID, out var ps))
        {
            failReason = "未拥有该技能。";
            return false;
        }
        return TryUpgradeSkill(ps, data, playerLevel, out failReason);
    }
    #endregion

    /// <summary>
    /// 将当前玩家技能快照写入 CharacterData.skills（覆盖式）。
    /// 优化：优先从运行时的 CharacterState.PlayerCharacterData 读取已生效的技能等级，回退到管理器快照。
    /// </summary>
    public void PopulateCharacterDataSkills(CharacterData data)
    {
        if (data == null) return;

        // 合并策略：从三个来源合并技能等级，取最大值，避免把较新的等级回退为较旧的
        // 来源优先级不强制，而是以最大等级为准：
        //  - SkillManager 快照（当前运行时快照）
        //  - 运行时 PlayerCharacterData
        //  - 传入的 data（已有存档数据）

        var merged = new Dictionary<string, int>(StringComparer.Ordinal);

        // 1) 来自管理器快照
        var snapshot = GetCurrentPlayerSkillsSnapshot();
        if (snapshot != null)
        {
            foreach (var kv in snapshot)
            {
                if (kv.Value != null && !string.IsNullOrEmpty(kv.Key))
                {
                    merged[kv.Key] = Math.Max(merged.GetValueOrDefault(kv.Key), kv.Value.Level);
                }
            }
        }

        // 2) 来自运行时 PlayerCharacterData（如果存在）
        var runtimePlayer = GameManager.Instance?.CurrentPlayerCharacter();
        var runtimeData = runtimePlayer?.PlayerCharacterData;
        if (runtimeData != null && runtimeData.skills != null)
        {
            foreach (var sd in runtimeData.skills)
            {
                if (sd == null || string.IsNullOrEmpty(sd.SkillID)) continue;
                merged[sd.SkillID] = Math.Max(merged.GetValueOrDefault(sd.SkillID), sd.Level);
            }
        }

        // 3) 来自已有的 CharacterData（传入的 data）
        if (data.skills != null)
        {
            foreach (var sd in data.skills)
            {
                if (sd == null || string.IsNullOrEmpty(sd.SkillID)) continue;
                merged[sd.SkillID] = Math.Max(merged.GetValueOrDefault(sd.SkillID), sd.Level);
            }
        }

        // 如果合并结果为空，说明三方均无初始技能：使用技能库初始化为1级
        if (merged.Count == 0)
        {
            var allSkills = GetSkillsForProfession();
            foreach (var so in allSkills)
            {
                if (so != null && !string.IsNullOrEmpty(so.SkillID)) merged[so.SkillID] = 1;
            }
        }

        // 最终将合并结果写回到 data.skills（覆盖式）
        data.skills = new List<SkillSaveData>();
        foreach (var kv in merged)
        {
            data.skills.Add(new SkillSaveData(kv.Key, kv.Value));
        }
    }
}