using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using DG.Tweening;
using UnityEngine.UI;

/// <summary>
/// 登录面板管理类，负责管理登录、注册和修改密码界面的切换
/// </summary>
public class LoginPanel : MonoBehaviour
{
    #region 字段
    
    #region UI引用
    [Header("界面引用")]
    [SerializeField] private LoginScreen loginScreen;
    [SerializeField] private RegisterScreen registerScreen;
    [SerializeField] private ChangePasswordScreen changePasswordScreen;
    [SerializeField] private GameObject logInPopPanel;
    #endregion

    #endregion

    #region Unity消息
    
    /// <summary>
    /// 初始化面板状态
    /// </summary>
    private void Start()
    {
        // 初始化显示登录界面
        ShowLoginScreen();
    }
    
    #endregion

    #region 公共方法
    
    /// <summary>
    /// 获取登录弹窗预制体
    /// </summary>
    /// <returns>登录弹窗GameObject</returns>
    public GameObject GetLogInPopPanel()
    {
        return logInPopPanel;
    }
    
    #region 面板切换
    
    /// <summary>
    /// 显示登录界面
    /// </summary>
    public void ShowLoginScreen()
    {
        if (loginScreen != null) loginScreen.gameObject.SetActive(true);
        if (registerScreen != null) registerScreen.gameObject.SetActive(false);
        if (changePasswordScreen != null) changePasswordScreen.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// 显示注册界面
    /// </summary>
    public void ShowRegisterScreen()
    {
        if (loginScreen != null) loginScreen.gameObject.SetActive(false);
        if (registerScreen != null) registerScreen.gameObject.SetActive(true);
        if (changePasswordScreen != null) changePasswordScreen.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// 显示修改密码界面
    /// </summary>
    public void ShowChangePasswordScreen()
    {
        if (loginScreen != null) loginScreen.gameObject.SetActive(false);
        if (registerScreen != null) registerScreen.gameObject.SetActive(false);
        if (changePasswordScreen != null) changePasswordScreen.gameObject.SetActive(true);
    }
    
    
    #endregion
    
    #endregion
}