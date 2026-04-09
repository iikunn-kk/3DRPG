using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuildPlayerInformation : MonoBehaviour
{
    [SerializeField] private TMP_Text rank;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image professionIcon;
    [SerializeField] private TMP_Text professionText;
    [SerializeField] private TMP_Text lastLoginText;
    [SerializeField] private Button setGuildFunctionButton;
    [SerializeField] private Button confirmOutGuildButton;
    private Action<GuildMemberInfo> _onSetGuildFunctionButtonClick;
    private Action<GuildMemberInfo> _onConfirmOutGuildClick;
    private GuildMemberInfo _memberInfo;

    /// <summary>
    /// 初始化成员显示
    /// </summary>
    /// <param name="memberInfo">成员信息</param>
    /// <param name="onSetGuildFunctionButtonClick">设置权限回调</param>
    /// <param name="onConfirmOutGuildClick">踢出成员回调</param>
    /// <param name="showSetGuildFunctionButton">是否显示设置权限按钮</param>
    /// <param name="showConfirmOutGuildButton">是否显示踢出按钮</param>
    public void Init(GuildMemberInfo memberInfo, Action<GuildMemberInfo> onSetGuildFunctionButtonClick, Action<GuildMemberInfo> onConfirmOutGuildClick, bool showSetGuildFunctionButton = true, bool showConfirmOutGuildButton = true)
    {
        _memberInfo = memberInfo;
        _onSetGuildFunctionButtonClick = onSetGuildFunctionButtonClick;
        _onConfirmOutGuildClick = onConfirmOutGuildClick;
        // 设置职位
        if (rank != null)
        {
            switch (memberInfo.rank)
            {
                case GuildMemberRank.Leader:
                    rank.text = "会长";
                    break;
                case GuildMemberRank.ViceLeader:
                    rank.text = "副会长";
                    break;
                case GuildMemberRank.Officer:
                    rank.text = "干事";
                    break;
                default:
                    rank.text = "成员";
                    break;
            }
        }
        
        // 设置等级
        if (levelText != null)
            levelText.text = memberInfo.level.ToString();
        
        // 设置角色名
        if (nameText != null)
            nameText.text = memberInfo.characterName;
        
        // 设置职业
        if (professionText != null)
            professionText.text = memberInfo.profession.ToString();
        
        // 设置最后在线时间分类显示
        if (lastLoginText != null)
        {
            lastLoginText.text = FormatLastOnline(memberInfo.lastOnlineTime);
        }
        var headIcon = GameManager.Instance.playerCharacterStateDataSo.GetPlayerCharacterStateBaseData(memberInfo.profession)
            .proHeadIcon;
        if (professionIcon != null && headIcon != null)
        {
            professionIcon.sprite = headIcon;
        } 

        if (setGuildFunctionButton != null) 
            setGuildFunctionButton.gameObject.SetActive(showSetGuildFunctionButton); 
        if (confirmOutGuildButton != null) 
            confirmOutGuildButton.gameObject.SetActive(showConfirmOutGuildButton); 
    } 
    
    private string FormatLastOnline(long ticks) 
    { 
        try 
        { 
            DateTime last = new DateTime(ticks); 
            DateTime now = DateTime.Now; 
            int days = (now.Date - last.Date).Days; 
            if (days <= 0) return "今天"; 
            if (days == 1) return "昨天"; 
            if (days == 2) return "两天前"; 
            if (days == 3) return "三天前"; 
            if (days < 7) return "一周内"; 
            if (days < 30) return "一个月内"; 
            if (days < 90) return "三个月内"; 
            if (days < 180) return "半年内"; 
            if (days < 365) return "一年内"; 
            return "一年以上"; 
        } 
        catch 
        { 
            return "未知"; 
        } 
    } 
    
    public void OnSetGuildFunctionButtonClick() 
    { 
        _onSetGuildFunctionButtonClick?.Invoke(_memberInfo); 
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    } 
    public void OnConfirmOutGuildClick() 
    { 
        _onConfirmOutGuildClick?.Invoke(_memberInfo); 
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    } 
}