using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// MMO 聊天 UI。挂到 Canvas 下的 ChatPanel 上。
/// 需要：TMP_InputField（输入）、Button（发送）、ScrollRect + TMP_Text（消息列表）。
/// 按 Enter 发送，消息从 Gateway 广播到所有在线玩家。
/// </summary>
public class ChatUI : MonoBehaviour
{
    [Header("UI 引用")]
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private TMP_Text _chatLog;
    [SerializeField] private Button _sendButton;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private GameObject _panelRoot;

    [Header("设置")]
    [SerializeField] private int _maxLines = 50;

    [Header("快捷键")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.Return;

    private bool _isFocused;
    private string _logText = "";

    void Start()
    {
        _sendButton?.onClick.AddListener(SendMessage);
        _panelRoot?.SetActive(false);

        // 注册到 NetworkManager 接收聊天
        if (NetworkManager.Instance != null && NetworkManager.Instance.Tcp != null)
        {
            NetworkManager.Instance.Tcp.OnChatReceived += OnChatReceived;
        }
    }

    void Update()
    {
        // 按快捷键打开/关闭聊天输入
        if (Input.GetKeyDown(_toggleKey))
        {
            if (!_isFocused)
            {
                OpenChat();
            }
            else if (_inputField != null)
            {
                SendMessage();
            }
        }
    }

    private void OpenChat()
    {
        _panelRoot?.SetActive(true);
        _isFocused = true;
        _inputField?.Select();
        _inputField?.ActivateInputField();
    }

    public void SendMessage()
    {
        if (_inputField == null || string.IsNullOrWhiteSpace(_inputField.text)) return;

        var text = _inputField.text.Trim();
        _inputField.text = "";

        var nm = FindFirstObjectByType<NetworkManager>();
        if (nm != null && nm.IsConnected)
        {
            nm.SendChat(text);
        }
        else
        {
            // 离线模式：只显示本地消息
            AppendMessage("你", text);
        }

        _inputField?.Select();
        _inputField?.ActivateInputField();
    }

    private void OnChatReceived(string json)
    {
        try
        {
            var msg = JsonUtility.FromJson<ChatMsg>(json);
            if (msg != null && !string.IsNullOrEmpty(msg.uid))
                AppendMessage(msg.uid, msg.text);
        }
        catch { /* 忽略解析失败 */ }
    }

    private void AppendMessage(string sender, string text)
    {
        _logText += $"[{sender}]: {text}\n";

        // 限制行数
        var lines = _logText.Split('\n');
        if (lines.Length > _maxLines)
        {
            _logText = string.Join("\n", lines, lines.Length - _maxLines, _maxLines);
        }

        if (_chatLog != null)
        {
            _chatLog.text = _logText;
            Canvas.ForceUpdateCanvases();
            _scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.Tcp != null)
        {
            NetworkManager.Instance.Tcp.OnChatReceived -= OnChatReceived;
        }
    }

    [System.Serializable]
    private class ChatMsg
    {
        public string uid;
        public string text;
    }
}
