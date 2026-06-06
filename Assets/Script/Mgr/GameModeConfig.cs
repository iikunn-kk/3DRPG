using UnityEngine;

/// <summary>
/// 全局游戏模式配置 —— 挂在场景中一个 DontDestroyOnLoad 的 GameObject 上。
/// 统一管理单机模式 / MMO 模式的切换，所有脚本通过 GameModeConfig.IsMmoMode 查询。
/// 
/// 用法：
///   - 在场景中创建一个 GameObject 命名为 "GameConfig"（或任意名称）
///   - 挂上本组件，勾选/取消 isMmoMode
///   - 其他脚本直接 `GameModeConfig.IsMmoMode` 判断当前模式
/// </summary>
public class GameModeConfig : MonoBehaviour
{
    [Header("游戏模式")]
    [Tooltip("勾选 = MMO 联机模式（需要 Docker 服务端运行）；取消 = 单机模式")]
    [SerializeField] private bool _isMmoMode = false;

    /// <summary>静态全局访问——任意脚本可直接调用</summary>
    public static bool IsMmoMode { get; private set; }

    private void Awake()
    {
        IsMmoMode = _isMmoMode;
        // 跨场景保留（避免切换场景丢失）
        DontDestroyOnLoad(gameObject);
        Debug.Log($"[GameModeConfig] 当前模式: {(_isMmoMode ? "MMO 联机模式" : "单机模式")}");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 编辑器中修改即生效，方便调试
        IsMmoMode = _isMmoMode;
    }
#endif
}
