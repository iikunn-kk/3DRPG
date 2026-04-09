using UnityEngine;

/// <summary>
/// 输入绑定引导脚本 - 在游戏启动时确保绑定正确加载
/// </summary>
public class InputBindingBootstrap : MonoBehaviour
{
    private void Awake()
    {
        // 游戏启动时同步绑定
        InputBindingRuntimeSync.ApplySavedOverridesToAllPlayersExisting();
    }
}
