using UnityEngine;

/// <summary>
/// 简化版泛型单例模式基类（强制手动挂载版），适用于微信小游戏等性能敏感项目
/// </summary>
/// <typeparam stationName="T">继承此基类的类型</typeparam>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    // 静态实例
    private static T _instance;

    // 是否在场景切换时保留
    [SerializeField] protected bool dontDestroyOnLoad = true;

    /// <summary>
    /// 单例实例属性（不再自动查找或创建实例）
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