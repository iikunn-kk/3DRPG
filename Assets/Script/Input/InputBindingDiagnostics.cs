using UnityEngine;
using UnityEngine.InputSystem;


/// <summary>
/// 输入绑定诊断工具 - 用于调试打包后的绑定问题
/// 在 Inspector 中点击按钮来执行诊断
/// </summary>
public class InputBindingDiagnostics : MonoBehaviour
{
    [Header("诊断工具")]
    [Tooltip("要检查的 InputActionAsset")]
    public InputActionAsset targetAsset;

    [Tooltip("要检查的动作引用列表")]
    public InputActionReference[] actionRefs;

    [ContextMenu("1. 诊断 PlayerPrefs 中的绑定")]
    public void DiagnosePlayerPrefs()
    {
        Debug.Log("========== PlayerPrefs 诊断 ==========");

        if (targetAsset != null)
        {
            string key = "Keybindings." + targetAsset.name;
            if (PlayerPrefs.HasKey(key))
            {
                string json = PlayerPrefs.GetString(key);
                Debug.Log($"找到保存的绑定: {key}");
                Debug.Log($"JSON 内容:\n{json}");
            }
            else
            {
                Debug.LogWarning($"未找到保存的绑定: {key}");
            }
        }

        // 列出所有 Keybindings 相关的键
        Debug.Log("\n检查所有可能的 Keybindings 键...");
        // 注意：Unity 的 PlayerPrefs 不支持列出所有键，这是已知限制
        Debug.Log("提示：PlayerPrefs 不支持列举所有键，请手动检查键名是否正确");
    }

    [ContextMenu("2. 诊断 InputActionAsset 当前状态")]
    public void DiagnoseAssetState()
    {
        Debug.Log("========== InputActionAsset 诊断 ==========");

        if (targetAsset == null)
        {
            Debug.LogError("targetAsset 未设置！");
            return;
        }

        Debug.Log($"资产名称: {targetAsset.name}");
        Debug.Log($"资产启用状态: {(targetAsset.enabled ? "启用" : "禁用")}");

        foreach (var actionMap in targetAsset.actionMaps)
        {
            Debug.Log($"\n动作映射: {actionMap.name}");
            foreach (var action in actionMap.actions)
            {
                Debug.Log($"  动作: {action.name}");
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    Debug.Log($"    绑定[{i}]:");
                    Debug.Log($"      path: {binding.path}");
                    Debug.Log($"      overridePath: {binding.overridePath}");
                    Debug.Log($"      effectivePath: {binding.effectivePath}");
                    Debug.Log($"      isComposite: {binding.isComposite}");
                    Debug.Log($"      isPartOfComposite: {binding.isPartOfComposite}");
                }
            }
        }
    }

    [ContextMenu("3. 诊断所有 PlayerInput 实例")]
    public void DiagnosePlayerInputs()
    {
        Debug.Log("========== PlayerInput 实例诊断 ==========");

#if UNITY_2023_1_OR_NEWER
        var players = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
#else
        var players = FindObjectsOfType<PlayerInput>(true);
#endif

        Debug.Log($"找到 {players.Length} 个 PlayerInput 实例");

        foreach (var pi in players)
        {
            Debug.Log($"\nPlayerInput: {pi.gameObject.name}");
            Debug.Log($"  路径: {GetGameObjectPath(pi.gameObject)}");

            InputActionAsset actions = null;

            // #if UNITY_2023_2_OR_NEWER
            //             actions = pi.GetComponent<InputActionAsset>();
            // #else
            try { actions = pi.actions; } catch { }
            // #endif

            if (actions != null)
            {
                Debug.Log($"  Actions 资产: {actions.name}");
                Debug.Log($"  Actions 启用: {actions.enabled}");

                // 显示第一个动作的绑定作为示例
                if (actions.actionMaps.Count > 0)
                {
                    var firstMap = actions.actionMaps[0];
                    if (firstMap.actions.Count > 0)
                    {
                        var firstAction = firstMap.actions[0];
                        Debug.Log($"  示例动作: {firstAction.name}");
                        if (firstAction.bindings.Count > 0)
                        {
                            var firstBinding = firstAction.bindings[0];
                            Debug.Log($"    绑定: path={firstBinding.path}, override={firstBinding.overridePath}");
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("  Actions 为 null！");
            }
        }
    }

    [ContextMenu("4. 强制重新加载绑定")]
    public void ForceReloadBindings()
    {
        Debug.Log("========== 强制重新加载绑定 ==========");

        if (actionRefs != null && actionRefs.Length > 0)
        {
            try
            {
                KeybindingStorage.LoadOverrides(actionRefs);
                Debug.Log("KeybindingStorage.LoadOverrides 完成");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"LoadOverrides 失败: {ex.Message}");
            }

            try
            {
                InputBindingRuntimeSync.ApplySavedOverridesToAllPlayersFor(actionRefs);
                Debug.Log("InputBindingRuntimeSync.ApplySavedOverridesToAllPlayersFor 完成");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ApplySavedOverridesToAllPlayersFor 失败: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning("actionRefs 未设置或为空");
        }

        try
        {
            InputBindingRuntimeSync.ApplySavedOverridesToAllPlayersExisting();
            Debug.Log("InputBindingRuntimeSync.ApplySavedOverridesToAllPlayersExisting 完成");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"ApplySavedOverridesToAllPlayersExisting 失败: {ex.Message}");
        }
    }

    [ContextMenu("5. 清除所有保存的绑定")]
    public void ClearAllSavedBindings()
    {
        Debug.Log("========== 清除保存的绑定 ==========");

        if (targetAsset != null)
        {
            string key = "Keybindings." + targetAsset.name;
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                Debug.Log($"已删除键: {key}");
            }
            else
            {
                Debug.Log($"键不存在: {key}");
            }
        }

        // 同时清除资产的运行时覆盖
        if (targetAsset != null)
        {
            targetAsset.RemoveAllBindingOverrides();
            Debug.Log("已清除资产的运行时覆盖");
        }
    }

    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform current = obj.transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }
}
