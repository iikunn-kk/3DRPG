#region 命名空间引用
// using System; // 已移除：未使用
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#pragma warning disable CS0219, IDE0059
#endregion

namespace Game.UI.SkillUpgrade
{
    /// <summary>
    /// 技能升级界面的右侧详情面板：展示当前与下一等级属性、消耗、解锁能力，并提供升级按钮
    /// </summary>
    public class SkillUpgradeDetailPanel : MonoBehaviour
    {
        #region UI组件字段定义
        [Header("UI组件 - 顶部信息")] [SerializeField]
        private Image iconImage;

        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [Header("伤害类型")]
        [SerializeField] private TMP_Text attackType;
        [Header("UI组件 - 进度条（当前等级/上限）")] [SerializeField]
        private Slider levelProgress; // 0..1

        [SerializeField] private TextMeshProUGUI levelProgressText; // e.g. 3 / 10

        [Header("UI组件 - 当前属性与下一等级预览")]
        // 不再使用 currentStatsText。改为两个独立文本：显示当前伤害/当前冷却
        [SerializeField] private TextMeshProUGUI currentDamageText; // 当前伤害/攻击力简要
        [SerializeField] private TextMeshProUGUI currentCooldownText; // 当前冷却时间
        // 新增：技能介绍与单独的下一级数值显示
        [SerializeField] private TextMeshProUGUI descriptionText; // 技能介绍（SO.description）
        [SerializeField] private TextMeshProUGUI nextDamageText; // 单独显示下一级伤害增量，如 (+20) 或 +5%
        [SerializeField] private TextMeshProUGUI nextCooldownText; // 单独显示下一级冷却变化，如 -1.2s

        [Header("UI组件 - 升级花费与需求")] [SerializeField]
        private TextMeshProUGUI costText; // 金币花费

        [SerializeField] private TextMeshProUGUI reqLevelText; // 需要的角色等级
  
        [Header("UI组件 - 操作")] [SerializeField] private Button upgradeButton;
   
        // 新增：等级里程碑星（5级和10级）
        [Header("UI组件 - 等级里程碑星")] 
        [SerializeField] private Image star5Image;
        [SerializeField] private Image star10Image;
        [SerializeField] private Transform star5Transform;
        [SerializeField] private Transform star10Transform;
        #endregion

        #region 私有字段
        private SkillUpgradePanel _skillUpgradePanel;
        private string _skillID;
        #endregion

        #region 公共方法
        public void Show(string skillID, SkillUpgradePanel panel)
        {
            _skillID = skillID;
            _skillUpgradePanel = panel;
            Refresh();

            if (upgradeButton != null)
            {
                upgradeButton.onClick.RemoveAllListeners();
                upgradeButton.onClick.AddListener(OnClickUpgrade);
            }

        }

        #endregion

        #region 私有方法 - 主要逻辑
        /// <summary>
        /// 刷新技能详情面板显示内容
        /// </summary>
        private void Refresh()
        {
            if (_skillUpgradePanel == null || string.IsNullOrEmpty(_skillID)) return;

            var so = SkillManager.Instance.GetSkillSo(_skillID);
            var snapshot = _skillUpgradePanel.GetSkillsSnapshot();

            int curLv = 0;
            PlayerSkill ps = null;
            if (snapshot != null && snapshot.TryGetValue(_skillID, out ps))
            {
                curLv = ps.Level;
            }

            UpdateAttackType(so);
            UpdateHeader(so, curLv);

            // 更新星（里程碑）
            UpdateStars(curLv, so);

            SkillUpgradePreview preview;

            if (ps != null && SkillManager.Instance.TryGetUpgradePreview(ps, out preview, out _))
            {
                UpdateDescriptionWithPreview(so);
                UpdateCurrentAndNextValuesWithPreview(so, preview, curLv);
                UpdateCostsAndButton(preview, curLv);
            }
            else
            {
                UpdateDescriptionWithoutPreview(so, curLv);
                UpdateValuesWithoutPreview(so, curLv);
                // 没有预览时清空/占位处理
                if (costText) costText.text = "-";
                if (reqLevelText) reqLevelText.text = "-";
                if (upgradeButton) upgradeButton.interactable = false;
            }
        }

        /// <summary>
        /// 处理升级按钮点击事件
        /// </summary>
        private void OnClickUpgrade()
        {
            if (_skillUpgradePanel == null || string.IsNullOrEmpty(_skillID)) return;
            string failReason; // out 参数会在方法内赋值，这里无需初始化
            bool success = SkillManager.Instance.TryUpgradeSkill(_skillID, out failReason);
            AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
            if (!success)
            {
                // 升级失败：立即刷新（可能金币不足等）并输出原因
                Debug.LogWarning($"技能升级失败: {_skillID} - {failReason}");
                Refresh();
            }
            else
            {
                // 升级成功：通知���板刷新（金币、列表项、保持选择）
                _skillUpgradePanel.HandleSkillUpgraded(_skillID);
                // 本地再刷新一次详情（等级、预览更新）
                Refresh();
            }
        }
        #endregion

        #region 私有方法 - UI更新逻辑
        // 更新攻击类型显示与颜色
        private void UpdateAttackType(SkillSO so)
        {
            if (attackType == null) return;
            if (so == null)
            {
                attackType.text = string.Empty;
                return;
            }
            attackType.text = so.attackType.ToString();
            switch (so.attackType)
            {
                case AttackType.物理攻击:
                    attackType.color = Color.red;
                    break;
                case AttackType.魔法攻击:
                    attackType.color = Color.blue;
                    break;
                case AttackType.Buff技能:
                    attackType.color = Color.cyan;
                    break;
                case AttackType.回血技能:
                    attackType.color = Color.green;
                    break;
                default:
                    attackType.color = Color.white;
                    break;
            }
        }

        // 更新顶部标题、图标、等级等简单信息
        private void UpdateHeader(SkillSO so, int curLv)
        {
            if (so == null) return;
            if (iconImage) iconImage.sprite = so.icon;
            if (nameText) nameText.text = so.skillName.ToString();
            if (levelText) levelText.text = $"Lv.{curLv}";

            if (levelProgress)
            {
                levelProgress.minValue = 0;
                levelProgress.maxValue = 1;
                levelProgress.value = so.maxLevel <= 0 ? 0 : (float)curLv / so.maxLevel;
            }
            if (levelProgressText)
            {
                levelProgressText.text = $"{curLv} / {so.maxLevel}";
            }

            if (descriptionText && so != null)
            {
                descriptionText.text = so.description;
            }
        }

        // 在有 preview 的情况下，合并描述与解锁提示
        private void UpdateDescriptionWithPreview(SkillSO so)
        {
            if (descriptionText == null || so == null) return;
            string desc = so.description ?? string.Empty;
            // 始终将两个特殊解锁说明（若存在）追加到描述中，每个占一行
            if (!string.IsNullOrEmpty(so.specialUnlockNoteLv5)) desc += $"\n[Lv5解锁] {so.specialUnlockNoteLv5}";
            if (!string.IsNullOrEmpty(so.specialUnlockNoteLv10)) desc += $"\n[Lv10解锁] {so.specialUnlockNoteLv10}";
            descriptionText.text = desc;
        }

        private void UpdateDescriptionWithoutPreview(SkillSO so, int curLv)
        {
            if (descriptionText == null || so == null) return;
            // 使用 curLv 以避免未使用参数的分析器警告（逻辑上当前方法不再依赖等级判断）
            _ = curLv;
            string desc = so.description ?? string.Empty;
            // 无论等级如何，均将两个特殊解锁说明（若存在）追加到描述中，每个占一行
            if (!string.IsNullOrEmpty(so.specialUnlockNoteLv5)) desc += $"\n[Lv5解锁] {so.specialUnlockNoteLv5}";
            if (!string.IsNullOrEmpty(so.specialUnlockNoteLv10)) desc += $"\n[Lv10解锁] {so.specialUnlockNoteLv10}";
            descriptionText.text = desc;
        }

        // 使用 preview 填充当前与下一级显示项
        private void UpdateCurrentAndNextValuesWithPreview(SkillSO so, SkillUpgradePreview preview, int curLv)
        {
            // 当前数值
            if (currentDamageText != null)
            {
                string curD;
                if (so != null)
                {
                    // 优先按技能名判断：若为 奥术智慧，总是按百分比显示，不受 attackType 影响
                    if (so.skillName == SkillType.奥术智慧)
                    {
                        // SkillManager 提供的是“百分数”单位（例如 13 表示 13%），这里直接追加 "%" 即可
                        curD = $"{preview.CurrentAttackScalePercent:F1}%";
                    }
                    else
                    {
                        switch (so.attackType)
                        {
                            case AttackType.物理攻击:
                            case AttackType.魔法攻击:
                                // 奥术智慧技能显示百分比，其他技能显示具体数值
                                // （奥术智慧已在上面处理，此处为其他普通攻击类型）
                                curD = $"{preview.CurrentBaseDamage:F0}";
                                break;
                            case AttackType.Buff技能:
                                curD = $"{preview.CurrentBuffValue:F1}";
                                break;
                            case AttackType.回血技能:
                                curD = $"{preview.CurrentHealAmount:F0}";
                                break;
                            default:
                                curD = preview.CurrentBaseDamage > 0 ? $"{preview.CurrentBaseDamage:F0}" : "-";
                                break;
                        }
                    }
                }
                else
                {
                    curD = preview.CurrentBaseDamage > 0 ? $"{preview.CurrentBaseDamage:F0}" : "-";
                }
                currentDamageText.text = curD;
            }

            if (currentCooldownText != null)
            {
                currentCooldownText.text = $"{preview.CurrentCooldown:F1}";
            }

            // 下一级显示（如果未达到10级）
            bool hideNextValues = curLv >= 10;
            if (nextDamageText != null) nextDamageText.gameObject.SetActive(!hideNextValues);
            if (nextCooldownText != null) nextCooldownText.gameObject.SetActive(!hideNextValues);

            if (!hideNextValues)
            {
                if (nextDamageText != null)
                {
                    string dmgText;
                    if (so != null)
                    {
                        // 优先处理 奥术智慧：无论 attackType，增量按百分比显示
                        if (so.skillName == SkillType.奥术智慧)
                        {
                            float pctDelta = preview.NextAttackScalePercent - preview.CurrentAttackScalePercent; // 单位：百分数
                            dmgText = FormatDelta(pctDelta, false);
                            if (dmgText != "-")
                                dmgText += "%";
                        }
                        else
                        {
                            switch (so.attackType)
                            {
                                case AttackType.物理攻击:
                                case AttackType.魔法攻击:
                                    float flatDelta2 = preview.NextBaseDamage - preview.CurrentBaseDamage;
                                    dmgText = FormatDelta(flatDelta2);
                                    break;
                                case AttackType.Buff技能:
                                    float buffDelta = preview.NextBuffValue - preview.CurrentBuffValue;
                                    dmgText = FormatDelta(buffDelta, false);
                                    break;
                                case AttackType.回血技能:
                                    float healDelta = preview.NextHealAmount - preview.CurrentHealAmount;
                                    dmgText = FormatDelta(healDelta);
                                    break;
                                default:
                                    float flatDeltaDefault = preview.NextBaseDamage - preview.CurrentBaseDamage;
                                    dmgText = FormatDelta(flatDeltaDefault);
                                    break;
                            }
                        }
                    }
                    else
                    {
                        float flatDelta = preview.NextBaseDamage - preview.CurrentBaseDamage;
                        dmgText = FormatDelta(flatDelta);
                    }

                    // 只在存在数值且为正的情况下添加 '+' 前缀，避免出现 "+-..." 这种情况
                    if (string.IsNullOrEmpty(dmgText) || dmgText == "-")
                        nextDamageText.text = "-";
                    else if (dmgText.StartsWith("-"))
                        nextDamageText.text = dmgText; // 负数直接显示负号
                    else
                        nextDamageText.text = "+" + dmgText;
                }

                if (nextCooldownText != null)
                {
                    float cdDelta = preview.NextCooldown - preview.CurrentCooldown; // 正值表示冷却变长，负值表示缩短
                    string cdText;
                    if (Mathf.Abs(cdDelta) < 0.01f)
                        cdText = "-";
                    else
                        cdText = $"{Mathf.Abs(cdDelta):F1}";
                    nextCooldownText.text = "-"+cdText+"s";
                }
            }
        }

        // 将一个增量格式化为可显示的字符串：
        // - 绝对值非常接近 0 时返回 "-" 表示无变化
        // - 绝对值 >= 1 时显示整数（例如 2 或 -3）
        // - 绝对值 < 1 且不为 0 时显示一位小数（例如 0.5 或 -0.3）
        private string FormatDelta(float delta, bool preferIntegerWhenLarge = true)
        {
            if (Mathf.Abs(delta) < 0.005f) // 小于 0.005 视为无变化
                return "-";
            if (Mathf.Abs(delta) >= 1f)
                return preferIntegerWhenLarge ? $"{delta:F0}" : $"{delta:F1}";
            return $"{delta:F1}"; // 小于 1 时保留一位小数以展示精度
        }

        // 无 preview（例如已满级）时填充当前显示项
        private void UpdateValuesWithoutPreview(SkillSO so, int curLv)
        {
            if (currentDamageText != null)
            {
                float baseD = SkillManager.Instance.GetBaseDamageAtLevel(so, curLv);
                float atkScale = SkillManager.Instance.GetAttackScalePercentAtLevel(so, curLv);
                if (so != null)
                {
                    if (so.skillName == SkillType.奥术智慧)
                    {
                        // 直接显示百分数并���加 "%"。例如 13 -> "13.0%"
                        currentDamageText.text = $"{atkScale:F1}%";
                    }
                    else if (so.attackType == AttackType.物理攻击 || so.attackType == AttackType.魔法攻击)
                    {
                        currentDamageText.text = $"{baseD:F0}";
                    }
                    else if (so.attackType == AttackType.回血技能)
                        currentDamageText.text = $"{SkillManager.Instance.GetHealAtLevel(so, curLv):F0}";
                    else if (so.attackType == AttackType.Buff技能)
                        currentDamageText.text = $"{SkillManager.Instance.GetBuffValueAtLevel(so, curLv):F1}";
                    else
                        currentDamageText.text = baseD > 0 ? $"{baseD:F0}" : "-";
                }
                else
                {
                    currentDamageText.text = baseD > 0 ? $"{baseD:F0}" : "-";
                }
            }
            if (currentCooldownText != null)
            {
                currentCooldownText.text = $"{SkillManager.Instance.GetCooldownAtLevel(so, curLv):F1}";
            }

            bool hideNextValues = curLv >= 10;
            if (nextDamageText != null)
            {
                if (hideNextValues) nextDamageText.gameObject.SetActive(false);
                else nextDamageText.text = "-";
            }
            if (nextCooldownText != null)
            {
                if (hideNextValues) nextCooldownText.gameObject.SetActive(false);
                else nextCooldownText.text = "-";
            }
        }

        // 更新消耗、需求与升级按钮状态
        private void UpdateCostsAndButton(SkillUpgradePreview preview, int curLv)
        {
            if (costText) costText.text = preview.Cost.ToString();
            if (reqLevelText) reqLevelText.text = preview.RequiredPlayerLevel.ToString();

            bool enoughGold = (CharacterService.Instance != null ? CharacterService.Instance.Money : 0) >= preview.Cost;
            bool enoughLevel = (_skillUpgradePanel.GetPlayerLevel()) >= preview.RequiredPlayerLevel;
            bool isMaxLevel = curLv >= preview.MaxLevel;
            bool canUpgrade = enoughGold && enoughLevel && !isMaxLevel;
            if (upgradeButton) upgradeButton.interactable = canUpgrade;
        }

        // 将里程碑星根据当前等级点亮或暗化
        private void UpdateStars(int currentLevel, SkillSO so)
        {
            // 如果是奥术射线技能，关闭星的显示
            if (so != null && so.skillName == SkillType.奥术射线)
            {
                if (star5Transform != null) star5Transform.gameObject.SetActive(false);
                if (star10Transform != null) star10Transform.gameObject.SetActive(false);
                return;
            }
            else
            {
                if (star5Transform != null) star5Transform.gameObject.SetActive(true);
                if (star10Transform != null) star10Transform.gameObject.SetActive(true);
            }

            // 目标阈值（5级和10级）
            const int star5Threshold = 5;
            const int star10Threshold = 10;

            if (star5Image != null)
                star5Image.gameObject.SetActive(currentLevel >= star5Threshold);

            if (star10Image != null)
                star10Image.gameObject.SetActive(currentLevel >= star10Threshold);
        }
        #endregion
    }
}
#pragma warning restore CS0219, IDE0059
