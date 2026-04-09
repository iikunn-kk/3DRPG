using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 统一管理（基于新输入系统）的按键绑定覆盖的保存与加载。
/// 保存位置：PlayerPrefs（每个 InputActionAsset 各自一份 JSON）。
/// 同时提供若干辅助方法，便于对单个 Action 的"单键/组合键(OneModifier)"进行读写。
/// </summary>
public static class KeybindingStorage
{
    private const string Prefix = "Keybindings."; // Key = Keybindings.{assetName}

    public static void SaveOverrides(InputActionReference[] actionRefs)
    {
        if (actionRefs == null || actionRefs.Length == 0) return;
        var assets = CollectAssets(actionRefs);
        
        foreach (var asset in assets)
        {
            if (asset == null) continue;
            
            try
            {
                string json = asset.SaveBindingOverridesAsJson();
                string key = Prefix + asset.name;
                PlayerPrefs.SetString(key, json);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[KeybindingStorage] 保存失败 {asset.name}: {ex.Message}");
            }
        }
        
        PlayerPrefs.Save();
    }

    public static void LoadOverrides(InputActionReference[] actionRefs)
    {
        if (actionRefs == null || actionRefs.Length == 0) return;
        var assets = CollectAssets(actionRefs);
        
        foreach (var asset in assets)
        {
            if (asset == null) continue;
            string key = Prefix + asset.name;
            
            if (!PlayerPrefs.HasKey(key))
            {
                asset.RemoveAllBindingOverrides();
                continue;
            }
            
            string json = PlayerPrefs.GetString(key, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    asset.LoadBindingOverridesFromJson(json);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[KeybindingStorage] 加载失败 {asset.name}: {ex.Message}");
                }
            }
            else
            {
                asset.RemoveAllBindingOverrides();
            }
        }
    }

    public static void ClearOverrides(InputActionReference[] actionRefs)
    {
        if (actionRefs == null || actionRefs.Length == 0) return;
        var assets = CollectAssets(actionRefs);
        foreach (var asset in assets)
        {
            if (asset == null) continue;
            asset.RemoveAllBindingOverrides();
        }
    }

    private static HashSet<InputActionAsset> CollectAssets(InputActionReference[] actionRefs)
    {
        var set = new HashSet<InputActionAsset>();
        foreach (var r in actionRefs)
        {
            var a = r != null ? r.action?.actionMap?.asset : null;
            if (a != null) set.Add(a);
        }
        return set;
    }

    // ======== 以下为单个 Action 的读写辅助（用于设置面板）========

    [Serializable]
    public class BindingConfig
    {
        public bool isComposite;
        public string singlePath; // 单键
        public string keyPath1;   // 组合：修饰键
        public string keyPath2;   // 组合：目标键
    }

    public static BindingConfig ReadCurrentConfig(InputAction action)
    {
        if (action == null) return new BindingConfig();
        GetCompositePartIndices(action, out var modIdx, out var bindIdx);
        string mod = modIdx >= 0 ? action.bindings[modIdx].effectivePath : null;
        string btn = bindIdx >= 0 ? action.bindings[bindIdx].effectivePath : null;
        if (!string.IsNullOrEmpty(mod) && !string.IsNullOrEmpty(btn))
        {
            return new BindingConfig { isComposite = true, keyPath1 = mod, keyPath2 = btn };
        }
        int singleIdx = GetSingleBindingIndex(action);
        string single = singleIdx >= 0 ? action.bindings[singleIdx].effectivePath : null;
        return new BindingConfig { isComposite = false, singlePath = single };
    }

    public static int GetSingleBindingIndex(InputAction action)
    {
        if (action == null) return -1;
        var bs = action.bindings;
        for (int i = 0; i < bs.Count; i++)
        {
            var b = bs[i];
            if (!b.isComposite && !b.isPartOfComposite)
                return i;
        }
        return -1;
    }

    public static void GetCompositePartIndices(InputAction action, out int modifierPartIndex, out int bindingPartIndex)
    {
        modifierPartIndex = -1;
        bindingPartIndex = -1;
        if (action == null) return;
        var bs = action.bindings;
        for (int i = 0; i < bs.Count; i++)
        {
            if (!bs[i].isComposite) continue;
            // 找到一个包含两个 part（modifier/binding）的复合
            int mod = -1, bind = -1;
            for (int j = i + 1; j < bs.Count && bs[j].isPartOfComposite; j++)
            {
                var part = bs[j];
                if (string.Equals(part.name, "modifier", StringComparison.OrdinalIgnoreCase)) mod = j;
                else if (string.Equals(part.name, "binding", StringComparison.OrdinalIgnoreCase)) bind = j;
            }
            if (mod >= 0 && bind >= 0)
            {
                modifierPartIndex = mod;
                bindingPartIndex = bind;
                return;
            }
        }
    }

    public static void SetSingleBindingPath(InputAction action, string path)
    {
        if (action == null) return;
        int idx = GetSingleBindingIndex(action);
        if (idx < 0) return;
        
        action.ApplyBindingOverride(idx, new InputBinding { overridePath = string.IsNullOrEmpty(path) ? null : path });
    }

    public static void SetCompositePaths(InputAction action, string modifierPath, string bindingPath)
    {
        if (action == null) return;
        GetCompositePartIndices(action, out var modIdx, out var bindIdx);
        if (modIdx >= 0)
        {
            action.ApplyBindingOverride(modIdx, new InputBinding { overridePath = string.IsNullOrEmpty(modifierPath) ? null : modifierPath });
        }
        if (bindIdx >= 0)
        {
            action.ApplyBindingOverride(bindIdx, new InputBinding { overridePath = string.IsNullOrEmpty(bindingPath) ? null : bindingPath });
        }
    }
}
