using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using System;
using System.Linq;

/// <summary>
/// 注册界面类，处理用户注册逻辑
/// </summary>
public class RegisterScreen : MonoBehaviour
{
    #region 字段

    #region UI组件
    [Header("输入字段")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_InputField confirmPasswordInput;
    [Header("按钮")]
    [SerializeField] private Button registerButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button[] switchPasswordVisibilityButton;
    [Header("密码可视与否")]
    [SerializeField] private TMP_InputField[] passwordInputField;
    public Sprite eyeOpenSprite;
    public Sprite eyeCloseSprite;

    [Header("错误提示文本")]
    [SerializeField] private TMP_Text passwordErrorText;
    [SerializeField] private TMP_Text confirmPasswordErrorText;
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
        // 初始化隐藏确认密码错误提示
        if (confirmPasswordErrorText != null)
        {
            confirmPasswordErrorText.gameObject.SetActive(false);
        }

        // 初始化隐藏密码强度错误提示
        if (passwordErrorText != null)
        {
            passwordErrorText.gameObject.SetActive(false);
        }
    }

    #endregion

    #region 按钮事件处理

    /// <summary>
    /// 处理注册按钮点击事件
    /// </summary>
    public void OnRegisterButtonClicked()
    {
        string username = usernameInput != null ? usernameInput.text : "";
        string password = passwordInput != null ? passwordInput.text : "";
        string confirmPassword = confirmPasswordInput != null ? confirmPasswordInput.text : "";

        // 检查用户名是否为空
        if (string.IsNullOrEmpty(username))
        {
            ShowPopup("注册失败", "用户名不能为空");
            return;
        }

        // 检查两次输入的密码是否一致
        if (password != confirmPassword)
        {
            ShowPopup("注册失败", "两次输入的密码不一致");
            return;
        }

        // 检查密码强度
        if (!IsPasswordStrongEnough(password))
        {
            ShowPopup("注册失败", "密码必须包含大小写字母以及数字");
            return;
        }

        // 使用MongoDB异步创建账户
        StartCoroutine(CreateAccountAsync(username, password));
    }

    /// <summary>
    /// 异步创建账户
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <returns></returns>
    private IEnumerator CreateAccountAsync(string username, string password)
    {
        var createAccountTask = MongoDBManager.Instance.CreatePlayerAccountAsync(username, password);
        yield return new WaitUntil(() => createAccountTask.IsCompleted || createAccountTask.IsFaulted || createAccountTask.IsCanceled);
        if (createAccountTask.Exception != null)
        {
            Debug.LogError($"创建账户时发生错误: {createAccountTask.Exception.Message}");
            ShowPopup("注册失败", "注册过程中发生未知错误");
        }
        else
        {
            // 根据任务返回的结果来显示不同的提示
            switch (createAccountTask.Result)
            {
                case RegistrationResult.Success:
                    Debug.Log("注册成功");
                    ShowPopup("注册成功", "恭喜您注册成功！");
                    if (loginPanel != null)
                    {
                        loginPanel.ShowLoginScreen();
                    }
                    break;
                case RegistrationResult.UsernameExists:
                    ShowPopup("注册失败", "用户名已存在");
                    break;
                case RegistrationResult.DatabaseError:
                    ShowPopup("注册失败", "无法连接到服务器，请稍后重试");
                    break;
                case RegistrationResult.InvalidInput:
                    // 如果你在未来添加了更多输入验证
                    ShowPopup("注册失败", "输入信息不合法");
                    break;
            }
        }
    }

    /// <summary>
    /// 处理返回按钮点击事件
    /// </summary>
    public void OnBackButtonClicked()
    {
        // 切换回登录界面
        if (loginPanel != null)
        {
            loginPanel.ShowLoginScreen();
        }
    }

    #endregion

    #region 输入框事件处理

    /// <summary>
    /// 密码输入框值变化处理
    /// </summary>
    /// <param name="value">密码输入值</param>
    public void OnPasswordValueChanged(string value)
    {
        CheckPasswordStrength();
        CheckConfirmPasswordMatch();
    }

    /// <summary>
    /// 确认密码输入框值变化处理
    /// </summary>
    /// <param name="value">确认密码输入值</param>
    public void OnConfirmPasswordValueChanged(string value)
    {
        CheckConfirmPasswordMatch();
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 检查密码强度是否满足要求
    /// </summary>
    private void CheckPasswordStrength()
    {
        if (passwordInput == null || passwordErrorText == null)
            return;

        string password = passwordInput.text;

        // 如果密码不为空且不满足强度要求，则显示错误提示
        if (!string.IsNullOrEmpty(password) && !IsPasswordStrongEnough(password))
        {
            passwordErrorText.gameObject.SetActive(true);
        }
        else
        {
            passwordErrorText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 检查确认密码是否匹配
    /// </summary>
    private void CheckConfirmPasswordMatch()
    {
        if (passwordInput == null || confirmPasswordInput == null || confirmPasswordErrorText == null)
            return;

        string password = passwordInput.text;
        string confirmPassword = confirmPasswordInput.text;

        // 如果确认密码不为空且与密码不匹配，则显示错误提示
        if (!string.IsNullOrEmpty(confirmPassword) && password != confirmPassword)
        {
            confirmPasswordErrorText.gameObject.SetActive(true);
        }
        else
        {
            confirmPasswordErrorText.gameObject.SetActive(false);
        }
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
public enum RegistrationResult
{
    Success,
    UsernameExists,
    DatabaseError,
    InvalidInput
}