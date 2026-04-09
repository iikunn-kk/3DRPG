using System;
using System.Collections;
using System.Collections.Generic;
using Michsky.MUIP;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 登录界面类，处理用户登录逻辑和界面切换
/// </summary>
public class LoginScreen : MonoBehaviour
{
    #region 字段

    #region UI组件
    [Header("输入字段")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField passwordInput;
    [Header("密码可视与否")]
    [SerializeField] private TMP_InputField passwordInputField;
    public Sprite eyeOpenSprite;
    public Sprite eyeCloseSprite;



    [Header("切换控件")]
    [SerializeField] private Toggle rememberMeToggle;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button registerButton;
    [SerializeField] private Button changePasswordButton;
    [SerializeField] private Button switchPasswordVisibilityButton;
    #endregion

    #region 引用
    [Header("引用")]
    [SerializeField] private LoginPanel loginPanel;
    private PlayerSetting playerSetting;
    private PlayerLoginData loggedInPlayerData; // 保存登录成功的玩家数据
    #endregion

    #endregion

    #region Unity消息

    /// <summary>
    /// 初始化组件和事件监听
    /// </summary>
    private void Start()
    {
        // 加载保存的设置
        LoadPlayerSettings();

        if (usernameInput.text == string.Empty || passwordInput.text == string.Empty)
        {
            loginButton.interactable = false;
        }
        else
        {
            loginButton.interactable = true;
        }
    }


    #endregion

    #region 按钮事件处理

    /// <summary>
    /// 处理登录按钮点击事件
    /// </summary>
    public void OnLoginButtonClicked()
    {
        string username = usernameInput != null ? usernameInput.text : "";
        string password = passwordInput != null ? passwordInput.text : "";

        if (string.IsNullOrEmpty(username))
        {
            // 用户名为空，提示用户注册
            ShowPopup("用户名为空", "用户名为空请注册");
        }
        else
        {
            // 使用MongoDB进行异步登录验证
            StartCoroutine(ValidateLoginAsync(username, password));
        }
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }

    /// <summary>
    /// 异步验证登录信息
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <returns></returns>
    private IEnumerator ValidateLoginAsync(string username, string password)
    {
        var findPlayerTask = MongoDBManager.Instance.AuthenticatePlayerAsync(username, password);
        yield return new WaitUntil(() => findPlayerTask.IsCompleted);

        if (findPlayerTask.Exception != null)
        {
            Debug.LogError($"登录验证时发生错误: {findPlayerTask.Exception.Message}");
            ShowPopup("登录失败", "验证过程中发生错误");
        }
        else if (findPlayerTask.Result == null)
        {
            // 用户名或密码错误
            ShowPopup("验证失败", "无法验证用户名和密码");
        }
        else
        {
            // 登录成功，保存玩家数据
            loggedInPlayerData = findPlayerTask.Result;

            // 设置当前玩家数据
            PlayerLogInManager.Instance.SetCurrentPlayerData(loggedInPlayerData);

            // 保存设置（如果需要）
            SavePlayerSettings();

            // 通知PlayerLogInManager登录成功
            if (PlayerLogInManager.Instance != null)
            {
                PlayerLogInManager.Instance.OnLoginSuccess();
            }

            Debug.Log($"登录成功，欢迎 {loggedInPlayerData.username}");
        }
    }

    /// <summary>
    /// 处理注册按钮点击事件
    /// </summary>
    public void OnRegisterButtonClicked()
    {
        // 隐藏当前界面并显示注册界面
        if (loginPanel != null)
        {
            loginPanel.ShowRegisterScreen();
        }
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }

    /// <summary>
    /// 处理修改密码按钮点击事件
    /// </summary>
    public void OnChangePasswordButtonClicked()
    {
        // 隐藏当前界面并显示修改密码界面
        if (loginPanel != null)
        {
            loginPanel.ShowChangePasswordScreen();
        }
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }
    public void OnRememberMeToggleChanged()
    {
        // 保存设置（如果需要）
        SavePlayerSettings();
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }
    #endregion

    #region 私有方法

    #region 设置管理

    /// <summary>
    /// 加载玩家设置
    /// </summary>
    private void LoadPlayerSettings()
    {
        playerSetting = SaveManager.Instance.LoadPlayerSetting();
        if (rememberMeToggle != null)
        {
            rememberMeToggle.isOn = playerSetting.RememberPassword;
        }

        // 如果记住密码为true，加载保存的用户名和密码
        if (playerSetting.RememberPassword && usernameInput != null && passwordInput != null)
        {
            usernameInput.text = playerSetting.Username ?? "";
            passwordInput.text = playerSetting.Password ?? "";
        }
    }

    /// <summary>
    /// 保存玩家设置
    /// </summary>
    private void SavePlayerSettings()
    {
        if (playerSetting != null && rememberMeToggle != null)
        {
            playerSetting.RememberPassword = rememberMeToggle.isOn;

            // 如果记住密码为true，保存用户名和密码
            if (playerSetting.RememberPassword && usernameInput != null && passwordInput != null)
            {
                playerSetting.Username = usernameInput.text;
                playerSetting.Password = passwordInput.text;
            }
            else
            {
                // 如果不记住密码，清空保存的用户名和密码
                playerSetting.Username = "";
                playerSetting.Password = "";
            }

            SaveManager.Instance.SavePlayerSetting(playerSetting);
        }
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

    #region Ui事件

    public void OnInputFieldChanged(string value)
    {
        if (usernameInput.text == string.Empty || passwordInput.text == string.Empty)
        {
            loginButton.interactable = false;
        }
        else
        {
            loginButton.interactable = true;
        }
    }

    #endregion

    #endregion

    #region 切换密码是否可视

    public void SwitchVisibility()
    {
        if (passwordInputField == null || switchPasswordVisibilityButton == null || eyeCloseSprite == null || eyeOpenSprite == null)
        {
            Debug.LogWarning("passwordInputField, switchPasswordVisibilityButton, eyeCloseSprite 或 eyeOpenSprite 未赋值");
            return;
        }
        // 1. 先判断当前状态（在修改之前）
        bool isPasswordMode = passwordInputField.contentType == TMP_InputField.ContentType.Password;

        // 2. 根据原始状态切换图标
        switchPasswordVisibilityButton.image.sprite = isPasswordMode ? eyeOpenSprite : eyeCloseSprite;

        // 3. 根据原始状态切换输入框类型
        passwordInputField.contentType = isPasswordMode
            ? TMP_InputField.ContentType.Standard
            : TMP_InputField.ContentType.Password;

        // 4. 刷新显示
        passwordInputField.ForceLabelUpdate();

    }

    #endregion
}