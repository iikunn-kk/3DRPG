using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 聊天气泡。挂到 ChatBubble.prefab 上。
/// 仅同步文字 + 头像，样式在 Unity Prefab 中自行设置。
/// </summary>
public class ChatBubble : MonoBehaviour
{
    [Header("子对象引用")]
    [SerializeField] private Image _avatar;
    [SerializeField] private TMP_Text _messageText;

    public void Setup(string text, Sprite avatar = null)
    {
        if (_messageText != null) _messageText.text = text;
        if (_avatar != null && avatar != null) _avatar.sprite = avatar;
    }
}
