using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using Cysharp.Threading.Tasks;

public class ServerSelectionMod : MonoBehaviour
{
    [SerializeField] private TMP_Text serverNameText;
    [SerializeField] private Image serverStateImage;
    [Header("角标")]
    [SerializeField] private Image serverStateTagImage;
    [SerializeField] private TMP_Text serverStateText;
    [SerializeField] private Button serverButton;

    private ServerData _serverData;
    private Action<ServerData> _onServerSelected;

    public async UniTask Init(ServerData serverData, Action<ServerData> onServerSelected, string playerUid)
    {
        _serverData = serverData;
        _onServerSelected = onServerSelected;
        var characters = await MongoDBManager.Instance.GetCharactersByPlayerUIDAndServer(playerUid, serverData.serverId);
        if (characters.Count > 0)
        {
            serverNameText.text = serverData.serverName + "(" + characters.Count + ")";
            print(serverData.serverName);
        }
        else
        {
            serverNameText.text = serverData.serverName;
        }
        serverStateText.text = GetServerStateText(serverData.serverState);
        // 根据服务器状态设置颜色
        serverStateImage.color = GetServerStateColor(serverData.serverState);
        serverStateTagImage.color = GetServerStateColor(serverData.serverState);
        serverButton.interactable = serverData.serverState != ServerState.维护;
        // 添加按钮点击事件
        if (serverButton != null)
        {
            serverButton.onClick.RemoveAllListeners();
            serverButton.onClick.AddListener(() => OnServerSelected());
        }
    }

    private string GetServerStateText(ServerState state)
    {
        switch (state)
        {
            case ServerState.爆满: return "爆满";
            case ServerState.拥挤: return "拥挤";
            case ServerState.良好: return "良好";
            case ServerState.流畅: return "流畅";
            case ServerState.维护: return "维护";
            default: return "未知";
        }
    }

    private Color GetServerStateColor(ServerState state)
    {
        switch (state)
        {
            case ServerState.爆满: return Color.red;
            case ServerState.拥挤: return new Color(1f, 0.5f, 0f); // 橙色
            case ServerState.良好: return Color.yellow;
            case ServerState.流畅: return Color.green;
            case ServerState.维护: return Color.gray;
            default: return Color.white;
        }
    }

    public void OnServerSelected()
    {
        // 直接调用传入的Action
        _onServerSelected?.Invoke(_serverData);
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }
}