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
    private Sprite _sessionAvatar;       // 本地会话缓存的头像 Sprite
    private int _sessionAvatarIndex = -1; // 对应在头像池中的下标，随消息发送给其他客户端
    private bool _avatarPicked;          // 是否已完成首次选取

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
        {
            PickSessionAvatar(); // 确保首次选取已完成
            _nm.SendChat(raw, _sessionAvatarIndex);
        }
        else
        {
            var localName = CharacterService.Instance?.CurrentPlayerCharacter()?.CharacterName ?? "";
            AddBubble(raw, localName);
        }

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
                AddBubble(msg.text, msg.characterName ?? msg.uid, msg.avatarIndex);
        }
        catch { }
    }

    private void AddBubble(string text, string playerName = null, int avatarIndex = -1)
    {
        if (_bubblePrefab == null || _bubbleParent == null) return;

        // 头像来源：远程消息用发送者指定的下标查本地池；本地消息用会话随机选取
        Sprite avatar;
        if (avatarIndex >= 0)
        {
            var pool = _bubblePrefab.AvatarOptions;
            avatar = (pool != null && avatarIndex < pool.Length) ? pool[avatarIndex] : _avatar;
        }
        else
        {
            avatar = PickSessionAvatar();
        }

        var bubble = Instantiate(_bubblePrefab, _bubbleParent);
        bubble.Setup(text, playerName, avatar);
        bubble.name = $"Bubble_{_bubbles.Count}";

        _bubbles.Enqueue(bubble);
        while (_bubbles.Count > _maxBubbles)
            Destroy(_bubbles.Dequeue().gameObject);

        Canvas.ForceUpdateCanvases();
        if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 0f;
    }

    /// <summary>
    /// 会话级头像选取：首次调用时从气泡 Prefab 的头像池中随机选取，
    /// 后续直接返回缓存值。同时缓存下标，随聊天消息发送给其他客户端。
    /// </summary>
    private Sprite PickSessionAvatar()
    {
        if (_avatarPicked)
            return _sessionAvatar;

        _avatarPicked = true;
        var pool = _bubblePrefab?.AvatarOptions;
        if (pool != null && pool.Length > 0)
        {
            _sessionAvatarIndex = Random.Range(0, pool.Length);
            _sessionAvatar = pool[_sessionAvatarIndex];
            return _sessionAvatar;
        }

        // 池为空时回退
        _sessionAvatar = _avatar;
        _sessionAvatarIndex = -1;
        return _sessionAvatar;
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
        public string characterName;
        public int avatarIndex = -1;
    }
}
