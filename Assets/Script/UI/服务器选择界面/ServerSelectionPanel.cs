using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.Events;
using Cysharp.Threading.Tasks;

public class ServerSelectionPanel : MonoBehaviour
{
    #region 字段

    #region UI组件
    [Header("服务器按钮父物体")]
    [SerializeField] private Transform serverSelectionParent;
    [Header("服务器分类按钮父物体")]
    [SerializeField] private Transform serverCategoryParent;
    [Header("服务器按钮")]
    [SerializeField] private GameObject serverSelectionButton;
    [Header("服务器分类按钮")]
    [SerializeField] private GameObject serverCategoryButton;
    #endregion

    [Header("服务器配置文件")]
    [SerializeField] private TextAsset serverDataJson;
    [Header("登录管理器")]
    [SerializeField] private PlayerLogInManager playerLogInManager;
    [Header("登录面板")]
    [SerializeField] private LoginPanel loginPanel; // 添加登录面板引用

    // 服务器分类数据列表
    private List<ServerCategoryData> _serverCategories;

    // 当前选中的服务器分类索引
    private int _selectedCategoryIndex = -1;

    // 当前显示的服务器按钮列表
    private List<ServerSelectionMod> _currentServerMods = new List<ServerSelectionMod>();

    // 服务器选择事件
    public UnityAction<ServerData> onServerSelected;

    #endregion

    #region 公共方法

    /// <summary>
    /// 初始化服务器选择面板
    /// </summary>
    public void Init()
    {
        // 重新加载服务器数据
        LoadServerData();

        // 清除现有的分类按钮
        ClearCategoryButtons();
        // 同时清除现有的服务器按钮，避免旧数据残留
        ClearCurrentServerMods();
        // 重置选中分类索引，确保后续 OnCategorySelected 不会因为索引相同而提前 return
        _selectedCategoryIndex = -1;

        // 重新初始化分类按钮
        InitializeCategoryButtons();
    }

    #endregion

    #region 数据加载与初始化

    /// <summary>
    /// 加载服务器数据
    /// </summary>
    private void LoadServerData()
    {
        try
        {
            if (serverDataJson == null)
            {
                Debug.LogError("未指定服务器配置文件");
                return;
            }

            // 解析JSON数据
            ServerCategoryData[] categories = JsonHelper.FromJson<ServerCategoryData>(serverDataJson.text);
            _serverCategories = categories.ToList();
        }
        catch (System.Exception e)
        {
            Debug.LogError("加载服务器数据失败: " + e.Message);
        }
    }

    /// <summary>
    /// 初始化分类按钮
    /// </summary>
    private void InitializeCategoryButtons()
    {
        if (_serverCategories == null || _serverCategories.Count == 0)
        {
            Debug.LogWarning("没有服务器分类数据");
            return;
        }

        // 创建分类按钮
        for (int i = 0; i < _serverCategories.Count; i++)
        {
            ServerCategoryData categoryData = _serverCategories[i];

            GameObject categoryButtonObj = Instantiate(serverCategoryButton, serverCategoryParent);
            ServerCategoryMod categoryMod = categoryButtonObj.GetComponent<ServerCategoryMod>();

            if (categoryMod != null)
            {
                categoryMod.Init(categoryData, OnCategorySelected);
            }
        }

        // 默认选择第一个分类
        if (_serverCategories.Count > 0)
        {
            OnCategorySelected(_serverCategories[0].categoryId);
        }
    }

    /// <summary>
    /// 清除分类按钮
    /// </summary>
    private void ClearCategoryButtons()
    {
        foreach (Transform child in serverCategoryParent)
        {
            Destroy(child.gameObject);
        }
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// 处理分类选择事件
    /// </summary>
    /// <param name="categoryId">选中的分类ID</param>
    private void OnCategorySelected(int categoryId)
    {
        // 根据分类ID查找对应的索引
        int categoryIndex = _serverCategories.FindIndex(c => c.categoryId == categoryId);

        // 如果当前已经是选中的分类索引，则不重复刷新
        if (_selectedCategoryIndex == categoryIndex)
        {
            return;
        }

        if (categoryIndex < 0 || categoryIndex >= _serverCategories.Count)
        {
            Debug.LogWarning("无效的分类ID: " + categoryId);
            return;
        }

        // 更新选中状态
        _selectedCategoryIndex = categoryIndex;

        // 销毁当前的服务器按钮
        ClearCurrentServerMods();

        // 创建新的服务器按钮
        CreateServerModsForCategory(categoryIndex);
    }

    /// <summary>
    /// 为指定分类创建服务器按钮
    /// </summary>
    /// <param name="categoryIndex">分类索引</param>
    private void CreateServerModsForCategory(int categoryIndex)
    {
        ServerCategoryData categoryData = _serverCategories[categoryIndex];

        var sortedServers = categoryData.servers.OrderBy(s => GetServerStatePriority(s.serverState)).ToList();

        foreach (ServerData serverData in sortedServers)
        {
            GameObject serverButtonObj = Instantiate(serverSelectionButton, serverSelectionParent);
            ServerSelectionMod serverMod = serverButtonObj.GetComponent<ServerSelectionMod>();

            if (serverMod != null)
            {
                // 防护：playerLogInManager 或 当前玩家数据 可能为空，传入空字符串作为 uid 回退
                string uid = "";
                if (playerLogInManager != null && playerLogInManager.GetCurrentPlayerData() != null)
                {
                    uid = playerLogInManager.GetCurrentPlayerData().uid;
                }
                serverMod.Init(serverData, OnServerSelected, uid).Forget();
                _currentServerMods.Add(serverMod);
            }
        }
    }

    /// <summary>
    /// 获取服务器状态优先级（用于排序）
    /// </summary>
    /// <param name="state">服务器状态</param>
    /// <returns>优先级数值</returns>
    private int GetServerStatePriority(ServerState state)
    {
        switch (state)
        {
            case ServerState.维护: return 4;
            case ServerState.流畅: return 3;
            case ServerState.良好: return 2;
            case ServerState.拥挤: return 1;
            case ServerState.爆满: return 0;
            default: return 0;
        }
    }

    /// <summary>
    /// 处理服务器选择事件
    /// </summary>
    /// <param name="serverData">选中的服务器数据</param>
    public void OnServerSelected(ServerData serverData)
    {
        // 设置当前服务器ID
        if (playerLogInManager != null)
        {
            playerLogInManager.SetCurrentServerId(serverData.serverId);

            // 显示角色选择面板
            if (playerLogInManager != null)
            {
                playerLogInManager.ShowCharacterSelectPanel();
            }
        }

        // 触发服务器选择事件
        onServerSelected?.Invoke(serverData);

        // 隐藏服务器选择面板
        gameObject.SetActive(false);

        Debug.Log("选择了服务器: " + serverData.serverName + " (ID: " + serverData.serverId + ")");
    }

    #endregion

    #region 服务器按钮管理

    /// <summary>
    /// 清除当前的服务器按钮
    /// </summary>
    private void ClearCurrentServerMods()
    {
        foreach (var serverMod in _currentServerMods)
        {
            if (serverMod != null)
            {
                Destroy(serverMod.gameObject);
            }
        }

        _currentServerMods.Clear();
    }

    #endregion

    #region 面板控制方法

    /// <summary>
    /// 退出登录，返回登录界面
    /// </summary>
    public void Logout()
    {
        // 优先使用 PlayerLogInManager 来处理登出和界面切换
        if (playerLogInManager != null)
        {
            // 清理登录管理器状态
            playerLogInManager.Logout();

            // 让 UI 管理器显示登录面板
            playerLogInManager.ShowLoginPanel();
        }
        else if (loginPanel != null)
        {
            // 回退方案：直接显示 LoginPanel
            loginPanel.ShowLoginScreen();
        }

        // 隐藏服务器选择面板并清理当前显示的按钮和分类
        gameObject.SetActive(false);
        ClearCurrentServerMods();

        // 清除分类按钮并重置内部状态
        if (serverCategoryParent != null)
        {
            ClearCategoryButtons();
        }

        _selectedCategoryIndex = -1;
        _serverCategories = null;

        Debug.Log("已退出登录，返回登录界面");
    }

    #endregion

    #region 辅助类

    /// <summary>
    /// JSON辅助类，用于解析数组格式的JSON
    /// </summary>
    public static class JsonHelper
    {
        public static T[] FromJson<T>(string json)
        {
            string newJson = "{ \"array\": " + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
            return wrapper.array;
        }

        [System.Serializable]
        private class Wrapper<T>
        {
            public T[] array;
        }
    }

    #endregion
}