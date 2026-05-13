using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;

namespace Game.UI.SkillUpgrade
{
    /// <summary>
    /// 技能升级主面板：显示玩家拥有的技能列表与右侧详情面板（使用SO事件驱动）
    /// </summary>
    public class SkillUpgradePanel : UIPopPanelBase
    {
        [Header("引用")]
        [SerializeField] private Transform listContainer;             // 技能项容器（Vertical/Horizontal Layout）
        [SerializeField] private GameObject listItemPrefab;           // 技能列表项Prefab（挂载 SkillUpgradeListItem）
        [SerializeField] private SkillUpgradeDetailPanel detailPanel; // 右侧详情面板
        [SerializeField] private TextMeshProUGUI goldText;            // 顶部金币显示

        private readonly List<GameObject> _spawnedItems = new();
        private string _currentSelectedSkillID;
        private IReadOnlyDictionary<string, PlayerSkill> _skillsSnapshot;

        // 去掉事件订阅，仅初始化
        public void Init()
        {
            var skillController = CharacterRuntimeManager.Instance.CurrentPlayerCharacter()?.GetComponent<SkillController>();
            RefreshGold();
            OnSkillsInitialized(skillController);
            Show();
        }

        public void RefreshGold()
        {
            if (goldText != null)
            {
                goldText.text = PlayerCurrencyManager.Instance != null ? PlayerCurrencyManager.Instance.Money.ToString() : "0";
            }
        }

        public void OnSkillsInitialized(SkillController controller)
        {
            if (controller == null) return;
            _skillsSnapshot = controller.GetAllSkillsSnapshot();
            if (_skillsSnapshot != null && _skillsSnapshot.Count > 0)
            {
                BuildList(_skillsSnapshot);
            }
        }

        // 供详情面板在升级成功后回调
        public void HandleSkillUpgraded(string skillID)
        {
            // 刷新金币显示
            RefreshGold();
            // 刷新该技能条目
            RefreshListItem(skillID);
            // 保持当前选中不跳转
            if (_currentSelectedSkillID == skillID && detailPanel != null)
            {
                detailPanel.Show(skillID, this);
            }
        }

        private void BuildList(IReadOnlyDictionary<string, PlayerSkill> skills)
        {
            string previousSelected = _currentSelectedSkillID;
            foreach (var go in _spawnedItems)
            {
                if (go != null) Destroy(go);
            }
            _spawnedItems.Clear();

            var ordered = new List<PlayerSkill>(skills.Values);
            ordered.Sort((a, b) =>
            {
                int t = (a.SkillSO.skillType == SkillEffectType.普通攻击 ? 0 : 1)
                        .CompareTo(b.SkillSO.skillType == SkillEffectType.普通攻击 ? 0 : 1);
                if (t != 0) return t;
                int c = a.SkillSO.cooldown.CompareTo(b.SkillSO.cooldown);
                if (c != 0) return c;
                return string.Compare(a.SkillSO.skillName.ToString(), b.SkillSO.skillName.ToString(), StringComparison.Ordinal);
            });

            foreach (var ps in ordered)
            {
                var go = Instantiate(listItemPrefab, listContainer);
                _spawnedItems.Add(go);
                var item = go.GetComponent<SkillUpgradeListItem>();
                if (item != null) item.Init(ps.SkillSO.SkillID, OnClickListItem);
            }

            if (ordered.Count > 0)
            {
                if (!string.IsNullOrEmpty(previousSelected) && skills.ContainsKey(previousSelected))
                    _currentSelectedSkillID = previousSelected;
                else
                    _currentSelectedSkillID = ordered[0].SkillSO.SkillID;

                if (detailPanel != null)
                {
                    detailPanel.Show(_currentSelectedSkillID, this);
                }
            }
        }

        private void RefreshListItem(string skillID)
        {
            foreach (var go in _spawnedItems)
            {
                var item = go?.GetComponent<SkillUpgradeListItem>();
                if (item != null && item.SkillID == skillID)
                {
                    item.Refresh();
                    break;
                }
            }
        }

        private void OnClickListItem(string skillID)
        {
            _currentSelectedSkillID = skillID;
            if (detailPanel != null) detailPanel.Show(skillID, this);
        }

        public void OnClose()
        {
            UIManager.Instance.ClosePanel<SkillUpgradePanel>();
            Hide();
        }


        public IReadOnlyDictionary<string, PlayerSkill> GetSkillsSnapshot() => _skillsSnapshot;
        public int GetPlayerLevel() => CharacterRuntimeManager.Instance.CurrentPlayerCharacter()?.PlayerCharacterData.level ?? 0;
    }
}
