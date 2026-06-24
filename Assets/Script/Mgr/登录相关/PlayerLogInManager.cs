using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 玩家登录流程管理器 — 合并了旧的 PlayerLogInUIManager。
/// 负责登录/创建角色/选角/选服务器面板切换 + 进入游戏场景。
/// </summary>
public class PlayerLogInManager : Singleton<PlayerLogInManager>
{
    // ===== UI 面板引用（来自旧 PlayerLogInUIManager）=====
    [Header("UI面板")]
    [SerializeField] private LoginPanel loginPanel;
    [SerializeField] private CreateCharacterPanel createCharacterPanel;
    [SerializeField] private CharacterSelectPanel characterSelectPanel;
    [SerializeField] private ServerSelectionPanel serverSelectionPanel;

    [Header("默认进入的游戏场景")]
    [SerializeField] private string defaultGameplayScene = "Village";

    private PlayerLoginData _currentPlayerLoginData;
    private int _currentServerId = -1;

    private void Start()
    {
        ShowLoginPanel();
    }

    // ===== 面板切换（来自旧 PlayerLogInUIManager）=====

    public void ShowLoginPanel()
    {
        if (loginPanel != null) { loginPanel.gameObject.SetActive(true); loginPanel.ShowLoginScreen(); }
        if (createCharacterPanel != null) createCharacterPanel.gameObject.SetActive(false);
        if (characterSelectPanel != null) characterSelectPanel.gameObject.SetActive(false);
        if (serverSelectionPanel != null) serverSelectionPanel.gameObject.SetActive(false);
    }

    public void ShowCreateCharacterPanel()
    {
        if (loginPanel != null) loginPanel.gameObject.SetActive(false);
        if (createCharacterPanel != null) createCharacterPanel.gameObject.SetActive(true);
        if (characterSelectPanel != null) characterSelectPanel.gameObject.SetActive(false);
        if (serverSelectionPanel != null) serverSelectionPanel.gameObject.SetActive(false);
    }

    public void ShowCharacterSelectPanel()
    {
        if (loginPanel != null) loginPanel.gameObject.SetActive(false);
        if (createCharacterPanel != null) createCharacterPanel.gameObject.SetActive(false);
        if (characterSelectPanel != null) { characterSelectPanel.gameObject.SetActive(true); characterSelectPanel.Init(_currentPlayerLoginData.uid, _currentServerId); }
        if (serverSelectionPanel != null) serverSelectionPanel.gameObject.SetActive(false);
    }

    public void ShowServerSelectionPanel()
    {
        if (loginPanel != null) loginPanel.gameObject.SetActive(false);
        if (createCharacterPanel != null) createCharacterPanel.gameObject.SetActive(false);
        if (characterSelectPanel != null) characterSelectPanel.gameObject.SetActive(false);
        if (serverSelectionPanel != null) { serverSelectionPanel.gameObject.SetActive(true); serverSelectionPanel.Init(); }
    }

    // ===== Getter =====

    public LoginPanel GetLoginPanel() => loginPanel;
    public CreateCharacterPanel GetCreateCharacterPanel() => createCharacterPanel;
    public CharacterSelectPanel GetCharacterSelectPanel() => characterSelectPanel;
    public ServerSelectionPanel GetServerSelectionPanel() => serverSelectionPanel;

    // ===== 游戏流程方法 =====

    public void GotoGameScene(CharacterData characterData)
    {
        SessionManager.Instance.SetCurrentCharacterData(characterData);

        var scene = characterData.currentScene;
        if (string.IsNullOrEmpty(scene) || scene == "LoginScene" || scene == "LoadingScene")
        {
            if (string.IsNullOrEmpty(defaultGameplayScene)) defaultGameplayScene = "Village";
            scene = defaultGameplayScene;
            Debug.LogWarning($"[PlayerLogInManager] currentScene 无效，使用默认场景: {scene}");
        }

        LoadingScreenController.TargetSceneAddress = scene;
        SceneManager.LoadScene("LoadingScene");
    }

    public void OnLoginSuccess() => ShowServerSelectionPanel();

    public void SetCurrentPlayerData(PlayerLoginData loginData) => _currentPlayerLoginData = loginData;
    public void SetCurrentServerId(int serverId) => _currentServerId = serverId;

    public string GetLoggedInUsername() => _currentPlayerLoginData?.username ?? "";
    public int GetCurrentServerId() => _currentServerId;
    public PlayerLoginData GetCurrentPlayerData() => _currentPlayerLoginData;

    public void ReselectCharacter() => ShowCharacterSelectPanel();
    public void ReselectServer() => ShowServerSelectionPanel();

    public void Logout()
    {
        _currentPlayerLoginData = null;
        _currentServerId = -1;
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
