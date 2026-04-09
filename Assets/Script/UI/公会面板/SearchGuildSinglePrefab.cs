using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SearchGuildSinglePrefab : MonoBehaviour
{
    [SerializeField] private TMP_Text guildNameText;
    [SerializeField] private TMP_Text guildLevelText;
    [SerializeField] private TMP_Text guildMemberCountText;
    [SerializeField] private TMP_Text guildMasterNameText;
    [SerializeField] private Image guildIconImage;
    private Action<string> _addClickAction;
    private string _guildId;
    private GuildData _guildData;
    
    public void Init(GuildData data, Action<string> addClickAction)
    { 
        _addClickAction = addClickAction;
        SetGuildInfo(data);
    }
    
    /// <summary>
    /// 设置公会信息
    /// </summary>
    /// <param name="guildData">公会数据</param>
    private void SetGuildInfo(GuildData guildData)
    {
        _guildId = guildData.guildId;
        _guildData = guildData;
        
        if (guildNameText != null)
            guildNameText.text = guildData.guildName;
            
        // TODO: 设置公会等级（需要确定如何计算公会等级）
        if (guildLevelText != null)
            guildLevelText.text = "Lv.1";
            
        if (guildMemberCountText != null)
            guildMemberCountText.text = $"{guildData.members.Count}/50"; // 假设最大成员数为50
            
        if (guildMasterNameText != null)
            guildMasterNameText.text = guildData.leaderCharacterName;
            
        // TODO: 设置公会图标
        // if (guildIconImage != null)
        //     guildIconImage.sprite = someSprite;
    }
    
    /// <summary>
    /// 获取公会数据
    /// </summary>
    /// <returns>公会数据</returns>
    public GuildData GetGuildData()
    {
        return _guildData;
    }
    
    public void OnAddButtonClick()
    {
        _addClickAction?.Invoke(_guildId);
    }
}