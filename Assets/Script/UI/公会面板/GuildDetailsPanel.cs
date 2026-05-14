using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

public class GuildDetailsPanel : MonoBehaviour
{
    [Header("公会信息显示组件")]
    [SerializeField] private TMP_Text guildNameText;

    [SerializeField] private TMP_Text guildMemberCountText;
    [SerializeField] private TMP_Text guildMasterNameText;
    [SerializeField] private TMP_Text guildDescriptionText;
    [SerializeField] private TMP_Text guildAnnouncementText;
    [SerializeField] private TMP_InputField guildAnnouncementInputField;
    [SerializeField] private EnterQuitGuildPanel quitPopupPanel;
    private GuildData currentGuildData;
    private bool isEditingAnnouncement = false;

    // 添加对公会主面板的引用，用于切换面板
    private AlreadyHaveGuildPanel alreadyHaveGuildPanel;

    public void SetAlreadyHaveGuildPanel(AlreadyHaveGuildPanel panel)
    {
        alreadyHaveGuildPanel = panel;
    }

    public void OnQuitGuildButtonClick()
    {
        quitPopupPanel.gameObject.SetActive(true);
        quitPopupPanel.Init(currentGuildData.guildName, async () =>
        {
            bool success = await GuildManager.Instance.QuitGuild();
            if (success && alreadyHaveGuildPanel != null)
            {
                // 获取公会面板的父对象（GuildPanel）
                Transform guildPanel = alreadyHaveGuildPanel.transform.parent;
                if (guildPanel != null)
                {
                    // 查找NotHaveGuildPanel并激活它
                    NotHaveGuildPanel notHaveGuildPanel = guildPanel.GetComponentInChildren<NotHaveGuildPanel>(true);
                    if (notHaveGuildPanel != null)
                    {
                        notHaveGuildPanel.gameObject.SetActive(true);
                        // 重新初始化，确保其持有 GuildPanel 引用
                        var guildPanelComp = guildPanel.GetComponent<GuildPanel>();
                        if (guildPanelComp != null)
                        {
                            notHaveGuildPanel.Init(guildPanelComp);
                        }
                        alreadyHaveGuildPanel.gameObject.SetActive(false);
                    }
                }
            }
            quitPopupPanel.gameObject.SetActive(false);
        });
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }

    public void Init(GuildData data)
    {
        currentGuildData = data;

        if (guildNameText != null)
            guildNameText.text = data.guildName;
        // 公会成员数量
        if (guildMemberCountText != null)
            guildMemberCountText.text = $"{data.members.Count}/50";
        // 会长名称
        if (guildMasterNameText != null)
            guildMasterNameText.text = data.leaderCharacterName;
        // 公会描述
        if (guildDescriptionText != null)
            guildDescriptionText.text = string.IsNullOrEmpty(data.guildDescription) ? "暂无描述" : data.guildDescription;
        // 公会公告
        if (guildAnnouncementText != null)
        {
            string ann = string.IsNullOrEmpty(data.guildAnnouncement) ? "欢迎加入本公会！" : data.guildAnnouncement;
            guildAnnouncementText.text = ann;
            guildAnnouncementText.gameObject.SetActive(true);
        }

        if (guildAnnouncementInputField != null)
        {
            guildAnnouncementInputField.gameObject.SetActive(false);
        }

        quitPopupPanel.gameObject.SetActive(false);
    }

    /// <summary>
    /// 点击公告文本 — 如果是会长，切换为可编辑的输入框
    /// （在编辑器中需要把公告Text的Button或点击事件绑定到这个方法）
    /// </summary>
    public void OnAnnouncementClicked()
    {
        if (currentGuildData == null) return;
        var current = SessionManager.Instance.CurrentCharacter;
        if (current == null) return;

        // 仅允许会长修改公会公告
        if (current.playerUid != currentGuildData.leaderUid) return;

        if (guildAnnouncementInputField == null || guildAnnouncementText == null) return;

        isEditingAnnouncement = true;
        guildAnnouncementInputField.gameObject.SetActive(true);
        guildAnnouncementInputField.text = currentGuildData.guildAnnouncement ?? string.Empty;
        // 移除 Select() 以兼容部分环境解析问题，仅激活输入框即可
        guildAnnouncementInputField.ActivateInputField();
        guildAnnouncementText.gameObject.SetActive(false);
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }

    /// <summary>
    /// 输入框结束编辑回调 — 保存公告并隐藏输入框
    /// </summary>
    /// <param name="text"></param>
    public void OnAnnouncementEndEdit(string text)
    {
        OnAnnouncementEndEditAsync(text).Forget();
    }

    private async UniTaskVoid OnAnnouncementEndEditAsync(string text)
    {
        try
        {
            if (!isEditingAnnouncement) return;
            isEditingAnnouncement = false;

            if (guildAnnouncementInputField != null)
                guildAnnouncementInputField.gameObject.SetActive(false);
            if (guildAnnouncementText != null)
                guildAnnouncementText.gameObject.SetActive(true);

            if (currentGuildData == null) return;

            string newText = text?.Trim() ?? string.Empty;
            if (newText == currentGuildData.guildAnnouncement)
            {
                guildAnnouncementText.text = string.IsNullOrEmpty(newText) ? "欢迎加入本公会！" : newText;
                return;
            }

            currentGuildData.guildAnnouncement = newText;

            bool save = await MongoDBManager.Instance.SaveGuildDataAsync(currentGuildData);
            if (!save)
            {
                Debug.LogError("保存公会公告失败");
            }

            if (guildAnnouncementText != null)
                guildAnnouncementText.text = string.IsNullOrEmpty(newText) ? "欢迎加入本公会！" : newText;

            if (alreadyHaveGuildPanel != null)
            {
                alreadyHaveGuildPanel.UpdateGuildData(currentGuildData);
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// 从数据库重新加载公会数据并刷新显示
    /// </summary>
    public async UniTaskVoid RefreshThisPanel()
    {
        try
        {
            if (currentGuildData == null) return;
            var data = await MongoDBManager.Instance.GetGuildData(currentGuildData.guildId);
            if (data == null) return;
            Init(data);
        }
        catch (OperationCanceledException) { }
    }

    public void SetGuildAnnouncement(string announcement)
    {
        if (currentGuildData == null) return;
        currentGuildData.guildAnnouncement = announcement;
        if (guildAnnouncementText != null)
            guildAnnouncementText.text = string.IsNullOrEmpty(announcement) ? "欢迎加入本公会！" : announcement;
    }
}