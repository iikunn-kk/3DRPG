using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 聊天气泡。挂到 ChatBubble.prefab 上。
/// 包含玩家名 + 消息文字 + 头像，用于 MMO 模式下区分不同发言者。
/// </summary>
public class ChatBubble : MonoBehaviour
{
    [Header("子对象引用")]
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Image _avatar;

    [Header("头像池")]
    [Tooltip("会话级随机头像候选。ChatUI 在首次发消息时从此数组中随机选取一张，本次运行期间固定使用。")]
    [SerializeField] private Sprite[] _avatarOptions;

    /// <summary>供 ChatUI 在会话首次发消息时从池中随机选取头像</summary>
    public Sprite[] AvatarOptions => _avatarOptions;

    public void Setup(string text, string playerName = null, Sprite avatar = null)
    {
        if (_messageText != null) _messageText.text = text;
        if (_nameText != null && !string.IsNullOrEmpty(playerName))
            _nameText.text = playerName;
        if (_avatar != null && avatar != null) _avatar.sprite = avatar;
    }
}
