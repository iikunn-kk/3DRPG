using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MMO 聊天 UI — 世界频道。
/// 气泡(头像+文字+sprite表情) + EmojiPicker + 垂直布局滚动 + Enter 弹出/发送。
/// 挂到 Canvas/ChatPanel 上。
/// </summary>
public class ChatUI : MonoBehaviour
{
    [Header("UI 引用")]
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private Button _sendButton;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private GameObject _panelRoot;

    [Header("气泡")]
    [SerializeField] private ChatBubble _bubblePrefab;
    [SerializeField] private Transform _bubbleParent;

    [Header("输入锁")]
    [SerializeField] private BoolEventSO cameraRotationActiveEvent;   // 关闭时锁相机（和 UIManager 共用同一个 SO）
    [SerializeField] private BoolEventSO movementLockEvent;           // 打开时禁止角色移动

    [Header("设置")]
    [SerializeField] private int _maxBubbles = 50;
    [SerializeField] private KeyCode _toggleKey = KeyCode.P;

    [SerializeField] private Sprite _avatar;

    private bool _isFocused;
    public static bool IsChatFocused { get; private set; }    // 供 QuickInventoryBar 等外部系统检查
    private NetworkManager _nm;
    private readonly Queue<ChatBubble> _bubbles = new();

    void Start()
    {
        _nm = FindFirstObjectByType<NetworkManager>();
        _panelRoot?.SetActive(false);

        _sendButton?.onClick.AddListener(SendMessage);
        _inputField?.onSubmit.AddListener(_ => SendMessage());

        if (_nm != null && _nm.Tcp != null)
            _nm.Tcp.OnChatReceived += OnChatReceived;
    }

    void Update()
    {
        if (Input.GetKeyDown(_toggleKey))
        {
            if (_isFocused) CloseChat();
            else OpenChat();
        }
    }

    private void OpenChat()
    {
        _panelRoot?.SetActive(true);
        _isFocused = true;
        IsChatFocused = true;
        _inputField?.Select();
        _inputField?.ActivateInputField();

        // 锁定鼠标/相机 + 禁止角色移动
        cameraRotationActiveEvent?.RaiseEvent(false, this);
        movementLockEvent?.RaiseEvent(true, this);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void SendMessage()
    {
        if (_inputField == null || string.IsNullOrWhiteSpace(_inputField.text)) return;

        var raw = _inputField.text.Trim();
        _inputField.text = "";

        if (_nm != null && _nm.IsConnected)
            _nm.SendChat(raw);
        else
            AddBubble(raw);

        // 保持面板打开，重新聚焦输入框，继续输入
        _inputField?.Select();
        _inputField?.ActivateInputField();
    }

    private void CloseChat()
    {
        _isFocused = false;
        IsChatFocused = false;
        _panelRoot?.SetActive(false);

        // 恢复鼠标/相机 + 恢复角色移动
        cameraRotationActiveEvent?.RaiseEvent(true, this);
        movementLockEvent?.RaiseEvent(false, this);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void OnChatReceived(string json)
    {
        try
        {
            var msg = JsonUtility.FromJson<ChatMsg>(json);
            if (msg != null && !string.IsNullOrEmpty(msg.text))
                AddBubble(msg.text);
        }
        catch { }
    }

    private void AddBubble(string text)
    {
        if (_bubblePrefab == null || _bubbleParent == null) return;

        var bubble = Instantiate(_bubblePrefab, _bubbleParent);
        bubble.Setup(text, _avatar);
        bubble.name = $"Bubble_{_bubbles.Count}";

        _bubbles.Enqueue(bubble);
        while (_bubbles.Count > _maxBubbles)
            Destroy(_bubbles.Dequeue().gameObject);

        Canvas.ForceUpdateCanvases();
        if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 0f;
    }

    void OnDestroy()
    {
        if (_nm != null && _nm.Tcp != null)
            _nm.Tcp.OnChatReceived -= OnChatReceived;
    }

    [System.Serializable]
    private class ChatMsg
    {
        public string uid;
        public string text;
    }
}
