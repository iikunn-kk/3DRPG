using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CreateGuildPanel : UIPopPanelBase
{
    [SerializeField] private TMP_InputField guildNameInputField;
    [SerializeField] private TMP_InputField guildDescriptionInputField;
    [SerializeField] private TMP_Text guildNameTipText;
    [SerializeField] private TMP_Text guildDescriptionTipText;
    [SerializeField] private Button enterButton; // 确认按钮，输入不合法时禁用
    private System.Action<string, string> _onEnterButtonClick;
    
    
    public void Init(System.Action<string, string> onEnterButtonClick)
    {
        _onEnterButtonClick = onEnterButtonClick;
        // 清空输入框
        guildNameInputField.text = "";
        guildDescriptionInputField.text = "";
        
        // 初始化提示文本状态
        UpdateGuildNameTip();
        UpdateGuildDescriptionTip();
        // 根据初始输入设置确认按钮状态
        ValidateInputs();
        Show();
    }

    public void OnEnterButtonClick()
    {
        // 获取输入的公会名称和描述
        string guildName = guildNameInputField.text;
        string guildDescription = guildDescriptionInputField.text;
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
        _onEnterButtonClick?.Invoke(guildName, guildDescription);
        Hide(false);
    }
    
    public void OnCancelButtonClick()
    {
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
        Hide(false);
    }
    
    public void OnGuildNameValueChanged(string value)
    {
        UpdateGuildNameTip();
        ValidateInputs();
    }
    
    public void OnGuildDescriptionValueChanged(string value)
    {
        UpdateGuildDescriptionTip();
        ValidateInputs();
    }
    
    private void UpdateGuildNameTip()
    {
        // 公会名字要小于六个字(这里按字符数计算，一个中文字符算一个字)
        bool isValid = guildNameInputField.text.Length <= 6 && guildNameInputField.text.Length > 0;
        guildNameTipText.gameObject.SetActive(!isValid);
        
        if (!isValid)
        {
            if (guildNameInputField.text.Length == 0)
            {
                guildNameTipText.text = "公会名称不能为空";
            }
            else
            {
                guildNameTipText.text = "公会名称不能超过6个字符";
            }
        }
    }
    
    private void UpdateGuildDescriptionTip()
    {
        // 公会简介要小于十六个字(这里按字符数计算，一个中文字符算一个字)
        bool isValid = guildDescriptionInputField.text.Length <= 16;
        guildDescriptionTipText.gameObject.SetActive(!isValid);
        
        if (!isValid)
        {
            guildDescriptionTipText.text = "公会简介不能超过16个字符";
        }
    }

    // 校验当前输入是否满足要求，并设置确认按钮的 interactable 状态
    private bool ValidateInputs()
    {
        bool validName = guildNameInputField != null && guildNameInputField.text.Length <= 6 && guildNameInputField.text.Length > 0;
        bool validDesc = guildDescriptionInputField != null && guildDescriptionInputField.text.Length <= 16;
        bool valid = validName && validDesc;
        if (enterButton != null)
            enterButton.interactable = valid;
        return valid;
    }
}