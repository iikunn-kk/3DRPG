using UnityEngine;

/// <summary>
/// 泛型单例模式基类，适用于微信小游戏等性能敏感项目。
/// 默认通过 FindFirstObjectByType 查找场景中的实例，找不到则自动创建。
/// 可通过 SetInstance() 显式注入实例（用于测试或手动初始化）。
/// </summary>
/// <typeparam name="T">继承此基类的类型</typeparam>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    // 静态实例
    private static T _instance;

    // 是否在场景切换时保留
    [SerializeField] protected bool dontDestroyOnLoad = true;

    /// <summary>
    /// 单例实例属性。优先返回通过 SetInstance 注入的实例，
    /// 其次查找场景中已有实例，均未找到则自动创建。
    /// </summary>
    public static T Instance
    {
        get
        {
            // 增加实例有效性检查（处理热重载等情况）
            if (_instance == null || _instance.gameObject == null)
            {
                _instance = FindFirstObjectByType<T>() ?? new GameObject(typeof(T).Name).AddComponent<T>();
            }
            return _instance;
        }
    }

    /// <summary>
    /// 显式注入单例实例（用于单元测试 Mock、或脚本生命周期外手动初始化）。
    /// 注入后 Instance 将直接返回该对象，不再自动查找或创建。
    /// </summary>
    /// <param name="instance">要注入的实例，传入 null 可清除当前实例</param>
    public static void SetInstance(T instance)
    {
        if (_instance != null && _instance != instance && _instance.gameObject != null)
        {
            Debug.LogWarning($"[Singleton] {typeof(T).Name} 已有实例，将被 SetInstance 替换");
        }
        _instance = instance;
    }

    // 标记应用程序是否正在退出
    //private static bool ApplicationIsQuitting;

    /// <summary>
    /// 确保只有一个实例存在
    /// </summary>
    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;

            if (dontDestroyOnLoad)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }

            OnSingletonAwake();
        }
        else if (_instance != this)
        {
            Debug.LogWarning($"[Singleton] 检测到重复的 {typeof(T)} 实例，销毁: {gameObject.name}");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 在单例初始化完成后调用，可在子类中重写
    /// </summary>
    protected virtual void OnSingletonAwake() { }


    /// <summary>
    /// 销毁单例实例
    /// </summary>
    public static void DestroyInstance()
    {
        if (_instance != null)
        {
            Destroy(_instance.gameObject);
            _instance = null;
        }
    }
    protected virtual void OnDestroy()
    {
        // 仅当自己是当前实例时才执行清理
        if (_instance == this)
        {
            _instance = null;
        }
    }
}