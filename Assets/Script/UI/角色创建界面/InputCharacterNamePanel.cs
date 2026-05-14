using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;

public class InputCharacterNamePanel : UIPopPanelBase
{
    [SerializeField] private TMP_InputField inputField;
    private Action<string> _onComplete;
    [SerializeField] private TMP_Text tipText;
    [SerializeField] private Button enterButton;
    [Header("验证按钮（可选，若在Prefab中配置则可直接绑定）")]
    [SerializeField] private Button validateButton;

    private const int MAX_NAME_LENGTH = 16;
    private bool _isValidated = false;

    public void Init(Action<string> onComplete)
    {
        _onComplete = onComplete;
        inputField.text = "";
        tipText.text = "请输入角色名，然后点击“验证”按钮进行校验。";
        enterButton.interactable = false;
        _isValidated = false;
        Show();
    }

    public void OnInputValueChanged(string value)
    {
        // 任何输入变更都需要重新验证
        _isValidated = false;
        enterButton.interactable = false;

        // 基础本地校验
        if (value.Length > MAX_NAME_LENGTH)
        {
            tipText.text = $"角色名不能超过{MAX_NAME_LENGTH}个字符";
            return;
        }

        if (!IsNameValid(value))
        {
            tipText.text = "角色名不能包含特殊符号";
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            tipText.text = "";
            return;
        }

        // 格式有效，但尚未远端验证
        tipText.text = "格式有效，请点击“验证”按钮进行重名校验";
    }

    //验证是否包含特殊符号
    private bool IsNameValid(string candidate)
    {
        string pattern = @"^[\u4e00-\u9fa5a-zA-Z0-9]*$";
        return Regex.IsMatch(candidate, pattern);
    }

    public void OnValidateNameButtonClick()
    {
        OnValidateNameButtonClickAsync().Forget();
    }

    private async UniTaskVoid OnValidateNameButtonClickAsync()
    {
        try
        {
            //播放按下音效
            AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
            string value = inputField.text.Trim();

            // 本地校验
            if (string.IsNullOrEmpty(value))
            {
                tipText.text = "请输入角色名";
                enterButton.interactable = false;
                _isValidated = false;
                return;
            }
            if (value.Length > MAX_NAME_LENGTH)
            {
                tipText.text = $"角色名不能超过{MAX_NAME_LENGTH}个字符";
                enterButton.interactable = false;
                _isValidated = false;
                return;
            }
            if (!IsNameValid(value))
            {
                tipText.text = "角色名不能包含特殊符号";
                enterButton.interactable = false;
                _isValidated = false;
                return;
            }

            // 远端校验当前服务器是否重名
            int serverId = 0;
            if (PlayerLogInManager.Instance != null)
            {
                serverId = PlayerLogInManager.Instance.GetCurrentServerId();
            }
            else
            {
                Debug.LogWarning("未找到 PlayerLogInManager.Instance，serverId 默认为 0 进行校验。");
            }

            tipText.text = "正在验证角色名，请稍候…";
            bool exists = false;
            try
            {
                exists = await MongoDBManager.Instance.IsCharacterNameExistsOnServer(value, serverId);
            }
            catch (Exception e)
            {
                Debug.LogError($"验证角色名时发生异常: {e.Message}");
                tipText.text = "验证失败，请稍后重试";
                enterButton.interactable = false;
                _isValidated = false;
                return;
            }

            if (exists)
            {
                tipText.text = "当前服务器已有同名角色，请更换角色名";
                enterButton.interactable = false;
                _isValidated = false;
            }
            else
            {
                tipText.text = "角色名可用";
                enterButton.interactable = true;
                _isValidated = true;
            }
        }
        catch (OperationCanceledException) { }
    }

    public void OnEnterButtonClick()
    {
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
        // 必须通过验证后才可进入
        if (!_isValidated)
        {
            tipText.text = "请先点击“验证”按钮通过校验";
            enterButton.interactable = false;
            return;
        }

        // 再次本地校验确保安全
        if (inputField.text.Length <= MAX_NAME_LENGTH && IsNameValid(inputField.text) && !string.IsNullOrWhiteSpace(inputField.text))
        {
            Hide(true, () =>
            {
                _onComplete?.Invoke(inputField.text.Trim());
            });
        }
    }

    public void OnCancelButtonClick()
    {
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
        Hide();
    }
}