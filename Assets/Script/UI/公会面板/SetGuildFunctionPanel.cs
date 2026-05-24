using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SetGuildFunctionPanel : UIPopPanelBase
{
   [SerializeField] private TMP_Dropdown dropdown;
   [SerializeField] private TMP_Text nameText;
   [SerializeField] private Image headIcon;
   [SerializeField] private TMP_Text tipText;
    private Action<GuildMemberRank> onConfirm;
    private GuildMemberInfo targetMember;

    // 可设置的职位(不包括会长) 顺序需与 option 文本对应
    private readonly List<GuildMemberRank> selectableRanks = new List<GuildMemberRank>
    {
        GuildMemberRank.Member,
        GuildMemberRank.Officer,
        GuildMemberRank.ViceLeader
    };

    private readonly List<string> rankTexts = new List<string>
    {
        "成员", "干事", "副会长"
    };

    public void Init(GuildMemberInfo memberInfo, Action<GuildMemberRank> onConfirm)
    {
        targetMember = memberInfo;
        nameText.text = memberInfo.characterName;
        if (headIcon != null)
        {
            headIcon.sprite = GameDataConfig.Instance.PlayerCharacterStateDataSo.GetPlayerCharacterStateBaseData(memberInfo.profession)
                .proHeadIcon;
        }

        this.onConfirm = onConfirm;

        if (dropdown != null)
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(rankTexts);
            // 预选当前职位(如果是会长，默认选成员)
            int index = selectableRanks.IndexOf(memberInfo.rank);
            if (index < 0) index = 0;
            dropdown.value = index;
            dropdown.RefreshShownValue();
        }
        tipText.text = $"将{memberInfo.characterName}职位改为:";
        Show();
    }

    public void OnConfirm()
    {
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
        if (dropdown != null)
        {
            int idx = dropdown.value;
            if (idx >= 0 && idx < selectableRanks.Count)
            {
                GuildMemberRank newRank = selectableRanks[idx];
                onConfirm?.Invoke(newRank);
            }
        }
        Hide(false);
    }
    public void OnCancel()
    {
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
        Hide(false);
    }
}
