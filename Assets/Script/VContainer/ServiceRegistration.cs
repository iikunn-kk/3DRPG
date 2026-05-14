using VContainer;
using UnityEngine;

/// <summary>
/// VContainer 服务注册与解析扩展，提供便捷的 DI 获取方法。
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// 从 GameLifetimeScope 容器中解析指定类型的服务。
    /// 等价于 GameLifetimeScope.Resolve&lt;T&gt;()，但加上了空值防御。
    /// </summary>
    public static T GetService<T>() where T : class
    {
        var service = GameLifetimeScope.Resolve<T>();
        if (service == null)
            Debug.LogWarning($"[DI] 无法解析 {typeof(T).Name}，回退到 Singleton.Instance。");
        return service;
    }
}
