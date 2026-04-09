using System;
using UnityEngine;

public class AlreadyHaveGuildPanel : MonoBehaviour
{
    [SerializeField] private GuildDetailsPanel guildDetailsPanel;
    [SerializeField] private GuildMemberPanel guildMemberPanel;
    
    private GuildData currentGuildData;
    
    public void Init(GuildData data, GuildPanel guildPanel = null)
    {
        currentGuildData = data;
        guildDetailsPanel.Init(data);
        guildMemberPanel.Init(data);
        
        // 设置AlreadyHaveGuildPanel引用，用于退出公会后切换面板
        guildDetailsPanel.SetAlreadyHaveGuildPanel(this);
        // 让成员面板也能回调到AlreadyHaveGuildPanel，以便在成员变动时刷新详情
        guildMemberPanel.SetAlreadyHaveGuildPanel(this);
        
        // 默认显示公会详情面板，隐藏成员面板
        if (guildDetailsPanel != null)
            guildDetailsPanel.gameObject.SetActive(true);
        if (guildMemberPanel != null)
            guildMemberPanel.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// 显示公会详情面板
    /// </summary>
    public void ShowGuildDetailsPanel()
    {
        if (guildDetailsPanel != null)
        {
            guildDetailsPanel.gameObject.SetActive(true);
        }
        
        if (guildMemberPanel != null)
        {
            guildMemberPanel.gameObject.SetActive(false);
        }
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }
    
    /// <summary>
    /// 显示公会成员面板
    /// </summary>
    public void ShowGuildMemberPanel()
    {
        if (guildDetailsPanel != null)
        {
            guildDetailsPanel.gameObject.SetActive(false);
        }
        
        if (guildMemberPanel != null)
        {
            guildMemberPanel.gameObject.SetActive(true);
            // 每次显示成员面板时都按职级排序
            guildMemberPanel.SortByRank();
        }
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }
    
    /// <summary>
    /// 获取当前显示的公会数据
    /// </summary>
    /// <returns>当前公会数据</returns>
    public GuildData GetCurrentGuildData()
    {
      return currentGuildData;
    }
    
    /// <summary>
    /// 更新公会面板显示数据
    /// </summary>
    /// <param name="data">新的公会数据</param>
    public void UpdateGuildData(GuildData data)
    {
      currentGuildData = data;
      
      if (guildDetailsPanel != null)
         guildDetailsPanel.Init(data);
      
      if (guildMemberPanel != null)
      {
         guildMemberPanel.Init(data);
         // 更新数据后默认按公会职级排序显示成员
         guildMemberPanel.SortByRank();
      }
    }

    /// <summary>
    /// 刷新公会成员列表显示
    /// </summary>
    public void RefreshGuildMembers()
    {
        if (guildMemberPanel != null)
        {
            guildMemberPanel.Init(currentGuildData);
            guildMemberPanel.SortByRank();
        }
    }
}