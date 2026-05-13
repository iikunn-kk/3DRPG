using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerLogInManager : Singleton<PlayerLogInManager>
{
    [Header("UI管理器")]
    [SerializeField] private PlayerLogInUIManager uiManager;

    [Header("默认进入的游戏场景(当存档场景无效时使用)")]
    // [SerializeField] private string defaultGameplayScene = "Level_1"; // 可在 Inspector 中修改
    [SerializeField] private string defaultGameplayScene = "Village"; // 可在 Inspector 中修改

    private PlayerLoginData _currentPlayerLoginData; // 当前登录玩家的数据
    private int _currentServerId = -1; // 当前选择的服务器ID (renamed from currentServerId)

    public void GotoGameScene(CharacterData characterData)
    {
        SessionManager.Instance.SetCurrentCharacterData(characterData);
        // 防止存档中 currentScene 丢失导致 scene key 为空

        // 2. 确定目标场景地址
        var scene = characterData.currentScene;
        if (string.IsNullOrEmpty(scene) || scene == "LoginScene" || scene == "LoadingScene")
        {
            if (string.IsNullOrEmpty(defaultGameplayScene))
            {
                defaultGameplayScene = "Village";
            }
            scene = defaultGameplayScene;
            Debug.LogWarning($"[PlayerLogInManager] 角色存档的 currentScene 无效，使用默认场景: {scene}");
        }

        // 3. 将目标场景地址传递给 LoadingScreenController
        //    因为 LoadingScreenController 在下一个场景，所以用 static 变量传递是完美的
        LoadingScreenController.TargetSceneAddress = scene;

        // 4. (重要) 不再调用 SceneLoadManager，而是直接加载本地的 LoadingScene
        //    注意：这里的 "LoadingScene" 必须是你在 Build Settings 中添加的场景的名称
        SceneManager.LoadScene("LoadingScene");
    }

    // 登录成功处理方法
    public void OnLoginSuccess()
    {
        // 显示服务器选择面板
        if (uiManager != null)
        {
            uiManager.ShowServerSelectionPanel();
        }
    }

    public void ShowCreateCharacterPanel()
    {
        if (uiManager != null)
        {
            uiManager.ShowCreateCharacterPanel();
        }
    }

    public void ShowCharacterSelectPanel()
    {
        if (uiManager != null)
        {
            string playerUid = _currentPlayerLoginData.uid;
            int serverId = _currentServerId;
            uiManager.ShowCharacterSelectPanel(serverId, playerUid);
        }
    }

    public void ShowLoginPanel()
    {
        if (uiManager != null)
        {
            uiManager.ShowLoginScreen();
        }
    }

    public string GetLoggedInUsername()
    {
        if (_currentPlayerLoginData != null)
        {
            return _currentPlayerLoginData.username;
        }
        return "";
    }

    // 设置当前玩家数据
    public void SetCurrentPlayerData(PlayerLoginData loginData)
    {
        _currentPlayerLoginData = loginData;
    }

    // 设置当前服务器ID
    public void SetCurrentServerId(int serverId)
    {
        _currentServerId = serverId;
    }

    // 获取当前服务器ID
    public int GetCurrentServerId()
    {
        return _currentServerId;
    }

    // 获取当前玩家数据
    public PlayerLoginData GetCurrentPlayerData()
    {
        return _currentPlayerLoginData;
    }

    // 重新选择角色
    public void ReselectCharacter()
    {
        // 显示角色选择面板
        ShowCharacterSelectPanel();
    }

    // 重新选择服务器
    public void ReselectServer()
    {
        // 显示服务器选择面板
        if (uiManager != null)
        {
            uiManager.ShowServerSelectionPanel();
        }
    }

    // 退出登录
    public void Logout()
    {
        _currentPlayerLoginData = null;
        _currentServerId = -1;
    }

    // 退出游戏
    public void ExitGame()
    {
#if UNITY_EDITOR
        // 在编辑器中停止播放
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 在构建版本中退出游戏
        Application.Quit();
#endif
    }
}