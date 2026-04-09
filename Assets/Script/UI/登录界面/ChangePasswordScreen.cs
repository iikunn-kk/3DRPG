using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;

/// <summary>
/// 修改密码界面类，处理用户修改密码逻辑
/// </summary>
public class ChangePasswordScreen : MonoBehaviour
{
    #region 字段

    #region UI组件
    [Header("输入字段")]
    [SerializeField] private TMP_InputField userNameInputField;
    [SerializeField] private TMP_InputField oldPasswordInputField;
    [SerializeField] private TMP_InputField newPasswordInputField;
    [SerializeField] private TMP_InputField confirmNewPasswordInputField;

    [Header("按钮")]
    [SerializeField] private Button changePasswordButton;
    [SerializeField] private Button returnLoginButton;
    [SerializeField] private Button[] switchPasswordVisibilityButton;
    [Header("密码可视与否")]
    [SerializeField] private TMP_InputField[] passwordInputField;
    public Sprite eyeOpenSprite;
    public Sprite eyeCloseSprite;

    [Header("错误提示文本")]
    [SerializeField] private TMP_Text userNameErrorText;
    [SerializeField] private TMP_Text confirmPasswordErrorText;
    [SerializeField] private TMP_Text newPasswordErrorText; // 添加新密码强度提示文本
    #endregion

    #region 引用
    [Header("引用")]
    [SerializeField] private LoginPanel loginPanel;
    #endregion

    #endregion

    #region Unity消息

    /// <summary>
    /// 初始化组件和事件监听
    /// </summary>
    private void Start()
    {
        // 初始化隐藏错误提示
        if (userNameErrorText != null)
        {
            userNameErrorText.gameObject.SetActive(false);
        }

        if (confirmPasswordErrorText != null)
        {
            confirmPasswordErrorText.gameObject.SetActive(false);
        }

        if (newPasswordErrorText != null)
        {
            newPasswordErrorText.gameObject.SetActive(false);
        }

        // 初始化禁用修改密码按钮
        if (changePasswordButton != null)
        {
            changePasswordButton.interactable = false;
        }
    }

    /// <summary>
    /// 清理事件监听器
    /// </summary>
    private void OnDestroy()
    {
        // 事件监听器移除操作已移除，因为事件监听完全通过Inspector配置
    }

    #endregion

    #region 按钮事件处理

    /// <summary>
    /// 处理修改密码按钮点击事件
    /// </summary>
    public void OnChangePasswordButtonClicked()
    {
        string username = userNameInputField != null ? userNameInputField.text : "";
        string oldPassword = oldPasswordInputField != null ? oldPasswordInputField.text : "";
        string newPassword = newPasswordInputField != null ? newPasswordInputField.text : "";
        string confirmNewPassword = confirmNewPasswordInputField != null ? confirmNewPasswordInputField.text : "";

        // 检查用户名是否存在（通过MongoDB异步检查）
        StartCoroutine(CheckUserAndChangePassword(username, oldPassword, newPassword, confirmNewPassword));
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }

    /// <summary>
    /// 检查用户是否存在并修改密码
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="oldPassword">旧密码</param>
    /// <param name="newPassword">新密码</param>
    /// <param name="confirmNewPassword">确认新密码</param>
    /// <returns></returns>
    private IEnumerator CheckUserAndChangePassword(string username, string oldPassword, string newPassword, string confirmNewPassword)
    {
        // 检查用户名是否存在
        var userExistsTask = MongoDBManager.Instance.IsUsernameExistsAsync(username);
        yield return new WaitUntil(() => userExistsTask.IsCompleted);

        if (userExistsTask.Exception != null)
        {
            Debug.LogError($"检查用户名时发生错误: {userExistsTask.Exception.Message}");
            ShowPopup("修改失败", "检查用户名时发生错误");
            yield break;
        }

        if (!userExistsTask.Result)
        {
            ShowPopup("修改失败", "用户名不存在");
            yield break;
        }

        // 检查新密码和确认密码是否一致
        if (newPassword != confirmNewPassword)
        {
            ShowPopup("修改失败", "新密码和确认密码不一致");
            yield break;
        }

        // 检查密码强度
        if (!IsPasswordStrongEnough(newPassword))
        {
            ShowPopup("修改失败", "密码必须包含大小写字母以及数字");
            yield break;
        }

        // 调用MongoDBManager修改密码
        var changePasswordTask = MongoDBManager.Instance.ChangePlayerPasswordAsync(username, oldPassword, newPassword);
        yield return new WaitUntil(() => changePasswordTask.IsCompleted);

        if (changePasswordTask.Exception != null)
        {
            Debug.LogError($"修改密码时发生错误: {changePasswordTask.Exception.Message}");
            ShowPopup("修改失败", "修改密码时发生错误");
        }
        else if (!changePasswordTask.Result)
        {
            ShowPopup("修改失败", "旧密码不正确");
        }
        else
        {
            // 修改密码成功逻辑
            Debug.Log("密码修改成功");
            ShowPopup("修改成功", "密码修改成功！");

            // 返回登录界面
            if (loginPanel != null)
            {
                loginPanel.ShowLoginScreen();
            }
        }
    }

    /// <summary>
    /// 处理返回登录按钮点击事件
    /// </summary>
    public void OnReturnLoginButtonClicked()
    {
        // 切换回登录界面
        if (loginPanel != null)
        {
            loginPanel.ShowLoginScreen();
        }
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }

    #endregion

    #region 输入框事件处理

    /// <summary>
    /// 用户名输入框值变化处理
    /// </summary>
    /// <param name="value">用户名输入值</param>
    public void OnUserNameValueChanged(string value)
    {
        CheckUserNameExists(value);
        UpdateChangePasswordButtonState();
    }

    /// <summary>
    /// 旧密码输入框值变化处理
    /// </summary>
    /// <param name="value">旧密码输入值</param>
    public void OnOldPasswordValueChanged(string value)
    {
        UpdateChangePasswordButtonState();
    }

    /// <summary>
    /// 新密码输入框值变化处理
    /// </summary>
    /// <param name="value">新密码输入值</param>
    public void OnNewPasswordValueChanged(string value)
    {
        CheckPasswordStrength(value);
        CheckConfirmPasswordMatch();
        UpdateChangePasswordButtonState();
    }

    /// <summary>
    /// 确认新密码输入框值变化处理
    /// </summary>
    /// <param name="value">确认新密码输入值</param>
    public void OnConfirmNewPasswordValueChanged(string value)
    {
        CheckConfirmPasswordMatch();
        UpdateChangePasswordButtonState();
    }

    #endregion

    #region 私有方法

    #region 验证方法

    /// <summary>
    /// 检查用户名是否存在
    /// </summary>
    /// <param name="username">用户名</param>
    private void CheckUserNameExists(string username)
    {
        if (userNameErrorText == null)
            return;

        // 注意：这里我们不直接检查MongoDB中的用户名是否存在，因为这会引入异步复杂性
        // 用户在点击修改密码按钮时会进行完整的检查
        if (!string.IsNullOrEmpty(username))
        {
            userNameErrorText.gameObject.SetActive(false);
        }
        else
        {
            userNameErrorText.gameObject.SetActive(!string.IsNullOrEmpty(username));
        }
    }

    /// <summary>
    /// 检查密码强度是否满足要求
    /// </summary>
    /// <param name="password">密码</param>
    private void CheckPasswordStrength(string password)
    {
        if (newPasswordInputField == null || newPasswordErrorText == null)
            return;

        // 如果新密码为空，则隐藏错误提示
        if (string.IsNullOrEmpty(password))
        {
            newPasswordErrorText.gameObject.SetActive(false);
            return;
        }

        // 检查密码强度各要素
        bool hasLower = Regex.IsMatch(password, @"[a-z]");
        bool hasUpper = Regex.IsMatch(password, @"[A-Z]");
        bool hasDigit = Regex.IsMatch(password, @"[0-9]");

        // 构建提示信息
        string errorMessage = "密码必须包含:";
        bool needsSeparator = false;

        if (!hasLower)
        {
            errorMessage += " 小写字母";
            needsSeparator = true;
        }

        if (!hasUpper)
        {
            errorMessage += (needsSeparator ? "," : "") + " 大写字母";
            needsSeparator = true;
        }

        if (!hasDigit)
        {
            errorMessage += (needsSeparator ? "," : "") + " 数字";
        }

        // 如果不满足要求，显示具体的错误提示
        if (!hasLower || !hasUpper || !hasDigit)
        {
            newPasswordErrorText.text = errorMessage;
            newPasswordErrorText.gameObject.SetActive(true);
        }
        else
        {
            newPasswordErrorText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 检查确认密码是否匹配
    /// </summary>
    private void CheckConfirmPasswordMatch()
    {
        if (newPasswordInputField == null || confirmNewPasswordInputField == null || confirmPasswordErrorText == null)
            return;

        string newPassword = newPasswordInputField.text;
        string confirmNewPassword = confirmNewPasswordInputField.text;

        // 如果确认密码不为空且与新密码不匹配，则显示错误提示
        if (!string.IsNullOrEmpty(confirmNewPassword) && newPassword != confirmNewPassword)
        {
            confirmPasswordErrorText.gameObject.SetActive(true);
        }
        else
        {
            confirmPasswordErrorText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 更新修改密码按钮状态
    /// </summary>
    private void UpdateChangePasswordButtonState()
    {
        if (changePasswordButton == null)
            return;

        string username = userNameInputField != null ? userNameInputField.text : "";
        string oldPassword = oldPasswordInputField != null ? oldPasswordInputField.text : "";
        string newPassword = newPasswordInputField != null ? newPasswordInputField.text : "";
        string confirmNewPassword = confirmNewPasswordInputField != null ? confirmNewPasswordInputField.text : "";

        // 检查密码强度
        bool isPasswordStrong = IsPasswordStrongEnough(newPassword);

        // 检查所有条件是否满足
        bool isUsernameValid = !string.IsNullOrEmpty(username);
        bool isOldPasswordValid = !string.IsNullOrEmpty(oldPassword);
        bool isPasswordMatch = newPassword == confirmNewPassword && !string.IsNullOrEmpty(newPassword);
        bool isPasswordValid = isPasswordStrong;

        // 只有当所有条件都满足时才启用按钮
        changePasswordButton.interactable = isUsernameValid && isOldPasswordValid && isPasswordMatch && isPasswordValid;
    }


    /// <summary>
    /// 检查密码强度是否足够
    /// </summary>
    /// <param name="password">密码</param>
    /// <returns>密码是否足够强壮</returns>
    private bool IsPasswordStrongEnough(string password)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        // 检查是否包含小写字母
        bool hasLower = Regex.IsMatch(password, @"[a-z]");
        // 检查是否包含大写字母
        bool hasUpper = Regex.IsMatch(password, @"[A-Z]");
        // 检查是否包含数字
        bool hasDigit = Regex.IsMatch(password, @"[0-9]");

        return hasLower && hasUpper && hasDigit;
    }

    #endregion

    #region UI辅助方法

    /// <summary>
    /// 显示弹窗
    /// </summary>
    /// <param name="title">弹窗标题</param>
    /// <param name="content">弹窗内容</param>
    private void ShowPopup(string title, string content)
    {
        if (loginPanel != null)
        {
            var popup = Instantiate(loginPanel.GetLogInPopPanel(), loginPanel.transform);
            var logInPopPanel = popup.GetComponent<LogInPopPanel>();
            if (logInPopPanel != null)
            {
                logInPopPanel.Init(title, content);
            }
        }
    }

    #endregion

    #endregion



    #region 切换密码是否可视

    public void SwitchVisibility()
    {
        if (passwordInputField.Length == 0 || switchPasswordVisibilityButton.Length == 0 || eyeCloseSprite == null || eyeOpenSprite == null)
        {
            Debug.LogWarning("passwordInputField, switchPasswordVisibilityButton, eyeCloseSprite 或 eyeOpenSprite 未赋值");
            return;
        }

        // TMP_InputField 的密码类型切换
        for (int i = 0; i < switchPasswordVisibilityButton.Length; i++)
        {
            // 1. 先判断当前状态（在修改之前）
            bool isPasswordMode = passwordInputField[i].contentType == TMP_InputField.ContentType.Password;

            // 2. 根据原始状态切换图标
            switchPasswordVisibilityButton[i].image.sprite = isPasswordMode ? eyeOpenSprite : eyeCloseSprite;

            // 3. 根据原始状态切换输入框类型
            passwordInputField[i].contentType = isPasswordMode
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;

            // 4. 刷新显示
            passwordInputField[i].ForceLabelUpdate();
        }
    }

    #endregion
}