using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

/// <summary>
/// Addressables 资源缓存工具，提供同步加载能力。
/// 用法：AddressableCache.Load<GameObject>("PlayingUI/Toast") 替代 Resources.Load<GameObject>("PlayingUI/Toast")
/// </summary>
public static class AddressableCache
{
    private static readonly Dictionary<string, object> _cache = new();
    private static readonly Dictionary<string, AsyncOperationHandle> _handles = new();

    /// <summary>
    /// 同步加载资源（先查缓存，缓存没有则 WaitForCompletion）。
    /// </summary>
    public static T Load<T>(string address) where T : Object
    {
        if (_cache.TryGetValue(address, out var cached))
            return cached as T;

        try
        {
            // 统一以 GameObject 类型加载，避免 Component 类型不匹配
            var handle = Addressables.LoadAssetAsync<GameObject>(address);
            var go = handle.WaitForCompletion();
            if (go == null)
            {
                Addressables.Release(handle);
                return null;
            }

            T result;
            if (typeof(Component).IsAssignableFrom(typeof(T)))
            {
                result = go.GetComponent<T>();
                if (result == null)
                {
                    Debug.LogError($"[AddressableCache] Prefab 上未找到 {typeof(T).Name} 组件 [{address}]");
                    Addressables.Release(handle);
                    return null;
                }
            }
            else
            {
                result = go as T;
            }

            _cache[address] = result;
            _handles[address] = handle;
            return result;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[AddressableCache] 加载失败 [{address}]: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 释放指定地址的缓存（通常在销毁时不需主动调用，Addressables 会随场景卸载释放）。
    /// </summary>
    public static void Release(string address)
    {
        if (_cache.TryGetValue(address, out _))
            _cache.Remove(address);

        if (_handles.TryGetValue(address, out var handle))
        {
            Addressables.Release(handle);
            _handles.Remove(address);
        }
    }

    /// <summary>
    /// 释放全部缓存。
    /// </summary>
    public static void ReleaseAll()
    {
        foreach (var kv in _handles)
            Addressables.Release(kv.Value);
        _handles.Clear();
        _cache.Clear();
    }
}
