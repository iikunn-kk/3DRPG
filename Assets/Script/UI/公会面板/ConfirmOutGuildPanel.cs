using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConfirmOutGuildPanel : UIPopPanelBase
{
    [SerializeField] private TMP_Text memberNameText;
    [SerializeField] private TMP_Text confirmText;
    [SerializeField] private Image headImage;
    private Action _onConfirm;
    public void Init(GuildMemberInfo memberInfo, System.Action onConfirm)
    {
        memberNameText.text = memberInfo.characterName.ToString();
        confirmText.text = memberInfo.characterName + "是否确认将" + memberInfo.characterName + "移出公会?";
        _onConfirm = onConfirm;
        Show();
    }
    public void OnConfirm()
    {
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
        _onConfirm?.Invoke();
        Hide(false);
    }
    public void OnCancel()
    {
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
        Hide(false);
    }
}
