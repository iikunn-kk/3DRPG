using VContainer;
using VContainer.Unity;
using UnityEngine;

/// <summary>
/// VContainer 根生命周期范围 — 注册核心 Manager 到 DI 容器。
/// 挂载到 GameManager 所在的 GameObject，作为整个应用的 DI 入口。
/// 保留 Singleton.Instance 作为向后兼容，同时提供 DI 访问通道。
/// </summary>
public class GameLifetimeScope : LifetimeScope
{
    /// <summary>
    /// 全局便捷入口 — 从 GameLifetimeScope 实例获取 Container 来解析服务。
    /// 业务代码可写：GameLifetimeScope.Resolve&lt;AudioManager&gt;()
    /// </summary>
    public static IObjectResolver Resolver { get; private set; }

    protected override void Configure(IContainerBuilder builder)
    {
        // 使用 RegisterInstance 注册已存在的 Singleton — 不依赖实例创建时序。
        RegisterSingletonIfExists<AudioManager>(builder, AudioManager.Instance);
        RegisterSingletonIfExists<UIManager>(builder, UIManager.Instance);
        RegisterSingletonIfExists<InventoryManager>(builder, InventoryManager.Instance);
        RegisterSingletonIfExists<PlayerCurrencyManager>(builder, PlayerCurrencyManager.Instance);
        RegisterSingletonIfExists<SceneLoadManager>(builder, SceneLoadManager.Instance);
        RegisterSingletonIfExists<SaveCoordinator>(builder, SaveCoordinator.Instance);
        RegisterSingletonIfExists<MongoDBManager>(builder, MongoDBManager.Instance);
        RegisterSingletonIfExists<CharacterRuntimeManager>(builder, CharacterRuntimeManager.Instance);
        RegisterSingletonIfExists<SessionManager>(builder, SessionManager.Instance);
        RegisterSingletonIfExists<GuildManager>(builder, GuildManager.Instance);
        RegisterSingletonIfExists<CharacterDataManager>(builder, CharacterDataManager.Instance);
        RegisterSingletonIfExists<SaveManager>(builder, SaveManager.Instance);
        RegisterSingletonIfExists<GameDataConfig>(builder, GameDataConfig.Instance);
        RegisterSingletonIfExists<TaskEventBridge>(builder, TaskEventBridge.Instance);

        // 非 Singleton 的实例 — 运行时自动查找
        builder.RegisterComponentInHierarchy<LoadingScreenController>();
        builder.RegisterComponentInHierarchy<CursorManager>();
        builder.RegisterComponentInHierarchy<MapManager>();
    }

    private static void RegisterSingletonIfExists<T>(IContainerBuilder builder, T instance) where T : class
    {
        if (instance != null)
        {
            builder.RegisterInstance(instance);
        }
        else
        {
            Debug.LogWarning($"[GameLifetimeScope] {typeof(T).Name}.Instance 为空，跳过 DI 注册。");
        }
    }

    protected override void Awake()
    {
        base.Awake();
        Resolver = Container;
        Debug.Log("[GameLifetimeScope] VContainer DI 容器已初始化，注册了核心 Manager。");
    }

    /// <summary>
    /// 从容器解析服务（便捷静态方法）。仅支持引用类型。
    /// </summary>
    public static T Resolve<T>() where T : class => Resolver?.Resolve<T>();

    protected override void OnDestroy()
    {
        Resolver = null;
        base.OnDestroy();
    }
}
