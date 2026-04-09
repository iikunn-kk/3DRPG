using UnityEngine;

public class SaveManager : Singleton<SaveManager>
{
    private const string PLAYER_SETTING_KEY = "PlayerSetting";

    public void SavePlayerSetting(PlayerSetting setting)
    {
        string json = JsonUtility.ToJson(setting);
        PlayerPrefs.SetString(PLAYER_SETTING_KEY, json);
        PlayerPrefs.Save();
    }

    public PlayerSetting LoadPlayerSetting()
    {
        PlayerSetting defaultSetting = new PlayerSetting();

        if (PlayerPrefs.HasKey(PLAYER_SETTING_KEY))
        {
            try
            {
                string json = PlayerPrefs.GetString(PLAYER_SETTING_KEY);
                PlayerSetting loadedSetting = JsonUtility.FromJson<PlayerSetting>(json);
                // 保留用户原有的 openBgm / openSound，不再强制设为 true
                if (loadedSetting != null)
                {
                    return loadedSetting;
                }
                else
                {
                    Debug.LogWarning("PlayerPrefs中存在设置键，但解析JSON失败，使用默认设置");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"加载玩家设置时发生异常: {ex.Message}");
            }
        }
        else
        {
            Debug.Log("没有找到已保存的玩家设置，使用默认设置");
        }

        return defaultSetting;
    }
    [ContextMenu("ClearSetting")]
    public void DeletePlayerSetting()
    {
        PlayerPrefs.DeleteKey(PLAYER_SETTING_KEY);
        PlayerPrefs.Save();
    }
    [ContextMenu("清理全部存档")]
    public void DeleteAllSave()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}