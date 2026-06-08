using UnityEngine;
using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

public class NotHaveGuildPanel : MonoBehaviour
{
    [SerializeField] private SearchGuildPanel searchGuildPanel;
    [SerializeField] private CreateGuildPanel createGuildPanel;
    private GuildPanel guildPanel; // 添加对公会主面板的引用

    /// <summary>
    /// 初始化面板，需要传入公会主面板引用
    /// </summary>
    /// <param name="panel">公会主面板</param>
    public void Init(GuildPanel panel)
    {
        guildPanel = panel;

        searchGuildPanel.gameObject.SetActive(false);
        createGuildPanel.gameObject.SetActive(false);

    }

    public void OnCreateGuildButtonClick()
    {
        searchGuildPanel.gameObject.SetActive(false);
        createGuildPanel.gameObject.SetActive(true);
        createGuildPanel.Init((name, desc) => OnCreateGuildConfirm(name, desc).Forget());
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }

    public void OnSearchGuildButtonClick()
    {
        createGuildPanel.gameObject.SetActive(false);
        searchGuildPanel.gameObject.SetActive(true);
        // 传入自身引用，便于在加入公会成功后回调切换到已有公会面板
        searchGuildPanel.Init(this);
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }

    /// <summary>
    /// 创建公会确认回调
    /// </summary>
    /// <param name="guildName">公会名称</param>
    /// <param name="guildDescription">公会描述</param>
    private async UniTaskVoid OnCreateGuildConfirm(string guildName, string guildDescription)
    {
        try
        {
            bool success = await GuildManager.Instance.CreateGuild(guildName, guildDescription);

            if (success)
            {
                Debug.Log("公会创建成功");
                if (GameModeConfig.IsMmoMode)
                {
                    // MMO 模式：guildId 由服务端快照异步同步，稍等后重试获取
                    UIManager.Instance.ShowSkillToast("公会创建请求已发送，等待服务端确认...");
                    await UniTask.Delay(1500);
                    // 异步等待快照同步 guildId 后再切换面板
                    _ = SwitchToGuildDetailsPanelWithRetry(3);
                }
                else
                {
                    await SwitchToGuildDetailsPanel();
                }
            }
            else
            {
                var err = GuildManager.Instance.LastError;
                var msg = string.IsNullOrEmpty(err) ? "公会创建失败" : err;
                UIManager.Instance?.ShowSkillToast(msg);
                Debug.LogWarning($"公会创建失败: {msg}");
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task SwitchToGuildDetailsPanelWithRetry(int maxRetries)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            var cd = SessionManager.Instance?.CurrentCharacter;
            if (cd != null && !string.IsNullOrEmpty(cd.guildId))
            {
                await SwitchToGuildDetailsPanel(cd.guildId);
                return;
            }
            await UniTask.Delay(1000);
        }
        Debug.LogWarning("等待公会数据超时，请手动刷新");
    }

    /// <summary>
    /// 加入公会后的回调处理
    /// </summary>
    /// <param name="guildId">公会ID</param>
    /// <returns></returns>
    public async Task OnJoinGuildSuccess(string guildId)
    {
        // 加入公会成功后，优先使用传入的公会ID进行切换，提升健壮性
        await SwitchToGuildDetailsPanel(guildId);
    }

    /// <summary>
    /// 切换到公会详情面板
    /// </summary>
    private async Task SwitchToGuildDetailsPanel(string guildIdOverride = null)
    {
        string targetGuildId = guildIdOverride;
        if (string.IsNullOrEmpty(targetGuildId))
        {
            // 获取最新的角色数据（包含公会ID）
            CharacterData characterData = SessionManager.Instance.CurrentCharacter;
            if (characterData != null)
            {
                targetGuildId = characterData.guildId;
            }
        }

        if (string.IsNullOrEmpty(targetGuildId))
        {
            Debug.LogError("切换到已有公会面板失败：目标公会ID为空");
            return;
        }

        // 处理可能的保存/读取延迟：尝试多次获取公会数据
        GuildData guildData = null;
        const int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            guildData = await MongoDBManager.Instance.GetGuildData(targetGuildId);
            if (guildData != null) break;
            await Task.Delay(150); // 短暂等待后重试，缓解极端情况下的延迟
        }

        // 确保有 GuildPanel 引用（防御：若未通过 Init 赋值，则自动向上查找）
        if (guildPanel == null)
        {
            guildPanel = GetComponentInParent<GuildPanel>();
        }

        if (guildData != null && guildPanel != null)
        {
            // 隐藏当前面板
            gameObject.SetActive(false);

            // 显示已有公会面板并初始化
            guildPanel.ShowAlreadyHaveGuildPanel(guildData);
        }
        else
        {
            if (guildData == null)
                Debug.LogError($"切换到已有公会面板失败：未获取到公会数据，guildId={targetGuildId}");
            if (guildPanel == null)
                Debug.LogError("切换到已有公会面板失败：GuildPanel 引用为空（请确认 NotHaveGuildPanel.Init 已被调用，或该组件在 GuildPanel 的层级下）");
        }
    }
}