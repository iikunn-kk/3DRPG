using UnityEngine;

/// <summary>
/// [已废弃] 面板切换逻辑已合并到 PlayerLogInManager，此类无任何调用方。
/// </summary>
[System.Obsolete("Use PlayerLogInManager instead.", false)]
public class PlayerLogInUIManager : MonoBehaviour
{
    #region 字段
    
    [Header("UI面板引用")]
    [SerializeField] private LoginPanel loginPanel;
    [SerializeField] private CreateCharacterPanel createCharacterPanel;
    [SerializeField] private CharacterSelectPanel characterSelectPanel;
    [SerializeField] private ServerSelectionPanel serverSelectionPanel;
    
    #endregion

    #region Unity消息
    
    private void Start()
    {
        // 初始化显示登录界面
        ShowLoginScreen();
    }
    
    #endregion

    #region 公共方法
    /// <summary>
    /// 显示登录界面
    /// </summary>
    public void ShowLoginScreen()
    {
        if (loginPanel != null)
        {
            loginPanel.gameObject.SetActive(true);
            loginPanel.ShowLoginScreen();
        }
        if (createCharacterPanel != null) createCharacterPanel.gameObject.SetActive(false);
        if (characterSelectPanel != null) characterSelectPanel.gameObject.SetActive(false);
        if (serverSelectionPanel != null) serverSelectionPanel.gameObject.SetActive(false);
    }

    /// <summary>
    /// 显示创建角色面板
    /// </summary>
    public void ShowCreateCharacterPanel()
    {
        if (loginPanel != null) loginPanel.gameObject.SetActive(false); // 隐藏所有登录相关面板
        if (createCharacterPanel != null) createCharacterPanel.gameObject.SetActive(true);
        if (characterSelectPanel != null) characterSelectPanel.gameObject.SetActive(false);
        if (serverSelectionPanel != null) serverSelectionPanel.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// 显示角色选择面板
    /// </summary>
    public void ShowCharacterSelectPanel(int serverId,string uid)
    {
        if (loginPanel != null) loginPanel.gameObject.SetActive(false);// 隐藏所有登录相关面板
        if (createCharacterPanel != null) createCharacterPanel.gameObject.SetActive(false);
        if (characterSelectPanel != null) characterSelectPanel.gameObject.SetActive(true);
        if (serverSelectionPanel != null) serverSelectionPanel.gameObject.SetActive(false);
        // 刷新角色列表
        if (characterSelectPanel != null)
        {
            characterSelectPanel.Init(uid,serverId);
        }
    }
    
    /// <summary>
    /// 显示服务器选择面板
    /// </summary>
    public void ShowServerSelectionPanel()
    {
        if (loginPanel != null) loginPanel.gameObject.SetActive(false); // 隐藏所有登录相关面板
        if (createCharacterPanel != null) createCharacterPanel.gameObject.SetActive(false);
        if (characterSelectPanel != null) characterSelectPanel.gameObject.SetActive(false);
        
        // 显示服务器选择面板
        if (serverSelectionPanel != null)
        {
            Debug.Log("激活服务器面板");
            serverSelectionPanel.gameObject.SetActive(true);
            serverSelectionPanel.Init();
            Debug.Log("成功激活了服务器面板");
        }
    }
    
    /// <summary>
    /// 获取登录面板
    /// </summary>
    /// <returns>LoginPanel实例</returns>
    public LoginPanel GetLoginPanel()
    {
        return loginPanel;
    }
    
    /// <summary>
    /// 获取创建角色面板
    /// </summary>
    /// <returns>CreateCharacterPanel实例</returns>
    public CreateCharacterPanel GetCreateCharacterPanel()
    {
        return createCharacterPanel;
    }
    
    /// <summary>
    /// 获取角色选择面板
    /// </summary>
    /// <returns>CharacterSelectPanel实例</returns>
    public CharacterSelectPanel GetCharacterSelectPanel()
    {
        return characterSelectPanel;
    }
    
    /// <summary>
    /// 获取服务器选择面板
    /// </summary>
    /// <returns>ServerSelectionPanel实例</returns>
    public ServerSelectionPanel GetServerSelectionPanel()
    {
        return serverSelectionPanel;
    }
    
    #endregion
}