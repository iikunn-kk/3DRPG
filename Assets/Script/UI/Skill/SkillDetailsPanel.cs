using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Text;

/// <summary>
/// 技能详情面板：显示技能名字/等级/冷却/描述，并在描述中追加“伤害/治疗/增益”的详细拆解：
/// - 清晰标注来自“技能基础”的数值与来自“玩家攻击力”的数值；
/// - 不再使用单独的伤害文本，所有说明汇总到 descriptionText。
/// </summary>
public class SkillDetailsPanel : MonoBehaviour
{
    [Header("UI 引用")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text cooldownText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image iconImage;
    
    private RectTransform _rect;
    private Canvas _canvas;

    private void Awake()
    {
        _rect = transform as RectTransform;
        _canvas = GetComponentInParent<Canvas>();
        if (_rect != null) gameObject.SetActive(false);
    }

    /// <summary>
    /// 显示技能详情（说明文字统一写入 descriptionText）。
    /// </summary>
    public void ShowAt(SkillSO so, PlayerSkill ps, int playerAttack)
    {
        if (so == null || ps == null || _rect == null) return;
        var sm = SkillManager.Instance;
        if (sm == null) return;

        // 基本信息
        if (nameText) nameText.text = so.skillName.ToString();
        if (levelText) levelText.text = $"Lv.{ps.Level}";

        float cd = sm.GetCooldownAtLevel(so, ps.Level);
        if (cooldownText)
        {
            cooldownText.text = cd > 0f ? $"冷却: {cd:F1}s" : "冷却: -";
        }

        // 计算通用数值
        float baseDmg = sm.GetBaseDamageAtLevel(so, ps.Level);
        float atkPct = sm.GetAttackScalePercentAtLevel(so, ps.Level);
        float atkPart = playerAttack * (atkPct / 100f);
        float totalDmg = baseDmg + atkPart;

        float baseHeal = so.baseHealAmount * (1f + so.perLevelHealAmountPercent * ps.Level);
        float buffVal = so.buffValue + so.perLevelBuffValue * ps.Level;

        // 组织描述文本：原始描述 + 一句话式的综合说明
        var sb = new StringBuilder(256);
        if (!string.IsNullOrEmpty(so.description))
        {
            sb.AppendLine(so.description.Trim());
        }

        bool hasHeal = baseHeal > 0.0001f;
        bool hasDamage = (baseDmg > 0.0001f) || (atkPct > 0.0001f);
        bool hasBuff = Mathf.Abs(buffVal) > 0.0001f || so.skillType == SkillEffectType.Buff;

        // 伤害：技能基础(X) + 玩家属性加成(Y) = Z（如果某一项为0则省略）
        if (hasDamage)
        {
            var line = new StringBuilder(64);
            line.Append("伤害: ");
            bool wrote = false;
            if (baseDmg > 0.0001f)
            {
                line.Append($"技能基础({baseDmg:F0})");
                wrote = true;
            }
            if (atkPct > 0.0001f && playerAttack > 0)
            {
                if (wrote) line.Append(" + ");
                line.Append($"玩家属性加成({atkPart:F0})");
                wrote = true;
            }
            if (wrote && (baseDmg > 0.0001f && (atkPct > 0.0001f && playerAttack > 0)))
            {
                line.Append($" = {totalDmg:F0}");
            }
            sb.AppendLine(line.ToString());
        }

        // 治疗：仅来自技能基础（不受玩家属性加成）
        if (hasHeal)
        {
            sb.AppendLine($"治疗: 技能基础({baseHeal:F0})");
        }

        // Buff：若是奥术智慧，固定文案；否则显示通用数值
        if (hasBuff)
        {
            if (so.skillName == SkillType.奥术智慧)
            {
                sb.AppendLine($"增加{buffVal:F1}%攻击力(技能基础加成{so.buffValue:F1}%)");
            }
            else if (Mathf.Abs(buffVal) > 0.0001f)
            {
                sb.AppendLine($"增益: 技能基础({so.buffValue:F1}) + 等级加成({so.perLevelBuffValue * ps.Level:F1}) = {buffVal:F1}");
            }
        }

        if (descriptionText)
        {
            descriptionText.text = sb.ToString();
        }
        if (iconImage) iconImage.sprite = so.icon;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
