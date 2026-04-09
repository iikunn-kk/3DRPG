using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 运行时同步热键绑定到所有 PlayerInput 实例
/// </summary>
public static class InputBindingRuntimeSync
{
    private const string KeyPrefix = "Keybindings."; // Key = Keybindings.{assetName}

    // 统一的查找 PlayerInput 助手，规避 API 废弃警告
    private static PlayerInput[] FindAllPlayerInputs()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<PlayerInput>(true);
#endif
    }

    /// <summary>
    /// 将保存的覆盖应用到所有 PlayerInput.actions 实例和传入的 actionRefs 资产
    /// </summary>
    public static void ApplySavedOverridesToAllPlayersFor(InputActionReference[] actionRefs)
    {
        if (actionRefs == null || actionRefs.Length == 0) return;

        // 收集相关资产名 -> JSON
        var assetNameToJson = new Dictionary<string, string>();
        foreach (var r in actionRefs)
        {
            var asset = r != null ? r.action?.actionMap?.asset : null;
            if (asset == null) continue;
            var name = asset.name;
            if (string.IsNullOrEmpty(name)) continue;
            if (assetNameToJson.ContainsKey(name)) continue;
            string key = KeyPrefix + name;
            if (!PlayerPrefs.HasKey(key)) continue;
            string json = PlayerPrefs.GetString(key, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                assetNameToJson[name] = json;
            }
        }
        
        if (assetNameToJson.Count == 0) return;

        // 应用到所有 PlayerInput 实例
        var players = FindAllPlayerInputs();
        foreach (var pi in players)
        {
            if (pi == null) continue;

            InputActionAsset actions = null;
#if UNITY_2023_2_OR_NEWER
            actions = pi.GetComponent<InputActionAsset>();
#else
            try { actions = pi.actions; } catch { }
#endif
            if (actions == null) continue;
            if (!assetNameToJson.TryGetValue(actions.name, out var json)) continue;
            
            try
            {
                bool wasEnabled = actions.enabled;
                if (wasEnabled) actions.Disable();
                actions.LoadBindingOverridesFromJson(json);
                if (wasEnabled) actions.Enable();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[InputBindingRuntimeSync] 同步失败 {pi.name}: {ex.Message}");
            }
        }

        // 应用到传入的 actionRefs 资产
        var processedAssets = new HashSet<string>();
        foreach (var r in actionRefs)
        {
            var asset = r != null ? r.action?.actionMap?.asset : null;
            if (asset == null || processedAssets.Contains(asset.name)) continue;
            if (!assetNameToJson.TryGetValue(asset.name, out var json)) continue;
            
            processedAssets.Add(asset.name);
            try
            {
                asset.LoadBindingOverridesFromJson(json);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[InputBindingRuntimeSync] 资产同步失败 {asset.name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 遍历所有 PlayerInput，读取并应用保存的绑定覆盖
    /// </summary>
    public static void ApplySavedOverridesToAllPlayersExisting()
    {
        var players = FindAllPlayerInputs();
        foreach (var pi in players)
        {
            if (pi == null) continue;

            InputActionAsset actions = null;
#if UNITY_2023_2_OR_NEWER
            actions = pi.GetComponent<InputActionAsset>();
#else
            try { actions = pi.actions; } catch { }
#endif
            if (actions == null) continue;
            string key = KeyPrefix + actions.name;
            if (!PlayerPrefs.HasKey(key)) continue;
            string json = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(json)) continue;
            
            try
            {
                bool wasEnabled = actions.enabled;
                if (wasEnabled) actions.Disable();
                actions.LoadBindingOverridesFromJson(json);
                if (wasEnabled) actions.Enable();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[InputBindingRuntimeSync] 加载失败 {pi.name}: {ex.Message}");
            }
        }
    }
}
