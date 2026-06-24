using UnityEngine;

/// <summary>
/// 玩家设置服务 — 取代旧 SaveManager。
/// 通过 PlayerPrefs 读写玩家设置（音频、分辨率等），不涉及角色存档。
/// 角色存档请用 SaveCoordinator。
/// </summary>
public class SettingsService : Singleton<SettingsService>
{
    private const string PLAYER_SETTING_KEY = "PlayerSetting";

    public void Save(PlayerSetting setting)
    {
        string json = JsonUtility.ToJson(setting);
        PlayerPrefs.SetString(PLAYER_SETTING_KEY, json);
        PlayerPrefs.Save();
    }

    public PlayerSetting Load()
    {
        var defaultSetting = new PlayerSetting();

        if (PlayerPrefs.HasKey(PLAYER_SETTING_KEY))
        {
            try
            {
                string json = PlayerPrefs.GetString(PLAYER_SETTING_KEY);
                var loaded = JsonUtility.FromJson<PlayerSetting>(json);
                if (loaded != null) return loaded;
                Debug.LogWarning("PlayerPrefs 设置解析失败，使用默认设置");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"加载玩家设置异常: {ex.Message}");
            }
        }

        return defaultSetting;
    }

    [ContextMenu("ClearSetting")]
    public void Delete()
    {
        PlayerPrefs.DeleteKey(PLAYER_SETTING_KEY);
        PlayerPrefs.Save();
    }

    [ContextMenu("清理全部存档")]
    public void DeleteAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}
