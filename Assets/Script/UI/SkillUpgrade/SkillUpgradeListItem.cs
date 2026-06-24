using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace Game.UI.SkillUpgrade
{
    /// <summary>
    /// 技能升级界面左侧的单项条目
    /// </summary>
    public class SkillUpgradeListItem : MonoBehaviour
    {
        [Header("UI组件")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Button button;

        public string SkillID { get; private set; }
        private Action<string> _onClick;

        public void Init(string skillID, Action<string> onClickCallback)
        {
            SkillID = skillID;
            _onClick = onClickCallback; // 之前缺失，导致点击无效
            Refresh();
        }
        public void Refresh()
        {
            if (string.IsNullOrEmpty(SkillID)) return;
            var so = SkillManager.Instance?.GetSkillSo(SkillID);
            if (GameDataConfig.Instance!=null)
            {
                var skills = CharacterService.Instance.CurrentPlayerCharacter()?.GetComponent<SkillController>()?.GetAllSkillsSnapshot();
                if (so != null && skills != null && skills.TryGetValue(SkillID, out var ps))
                {
                    if (iconImage && so.icon != null) iconImage.sprite = so.icon; // 修复初始未显示图标（若 prefab 引用正确）
                    if (nameText) nameText.text = so.skillName.ToString();
                    if (levelText) levelText.text = $"Lv.{ps.Level}";
                }
                else
                {
                    // 防御：缺失数据时给出占位
                    if (nameText) nameText.text = so != null ? so.skillName.ToString() : "-";
                    if (levelText) levelText.text = "Lv.-";
                }
            }
        }

        public void OnClick()
        {
            _onClick?.Invoke(SkillID);
            AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
        }
    }
}
