// SceneLoadManager.cs
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// 统一的场景过渡与传送管理器，完全基于 Addressables。
/// 支持三类场景：LoginScene、LoadingScene、Gameplay 场景（任意自定义 key/label）。
/// 用法：
/// - LoadLoginScene();
/// - LoadGameplayScene(sceneKey, useLoadingScreen: true/false, callback);
/// 功能：
/// - 可选加载过渡(LoadingScene)；
/// - 保存并销毁旧玩家对象 (GameManager API)；
/// - 统一回调与事件；
/// - 防重入；
/// - 兼容旧 TeleportManager（其已包装转调）。
/// </summary>
public class SceneLoadManager : Singleton<SceneLoadManager>
{
    public delegate void SceneLoadCallback();

    // Addressables 场景 Key 常量
    private const string LoginSceneKey = "LoginScene";    // 登录主界面场景
    private const string LoadingSceneKey = "LoadingScene"; // 过渡加载场景（显示进度）

    /// <summary>当前激活（已完成加载并激活）的场景地址/名称。</summary>
    public string CurrentSceneName { get; private set; }

    /// <summary>是否正在进行场景加载（含过渡）。</summary>
    public bool IsLoading => _isLoading;

    // 内部状态
    private bool _isLoading; // 去除冗余的 = false 初始化
    private string _pendingTargetScene;          // 记录即将加载的目标 scene key
    private SceneLoadCallback _pendingCallback;  // 目标场景激活后的回调
    private AsyncOperationHandle<SceneInstance> _directLoadHandle; // 直接加载句柄（可用于后续扩展卸载）
    private CancellationTokenSource _watchdogCts;
    private const float DefaultLoadWatchdogSeconds = 30f;

    // 事件：外部可订阅（可选）
    public event Action<string> OnSceneLoadStarted;       // 目标场景地址
    public event Action<string, float> OnSceneLoadProgress; // 仅 direct 模式简单进度（0-1）；loading 场景模式下建议 UI 由 LoadingScreenController 负责
    public event Action<string> OnSceneActivated;         // 激活完成

    // Subscribe to sceneLoaded as a fallback to detect activation when other controllers forget to callback
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_isLoading)
        {
            Debug.LogWarning("[SceneLoadManager] 场景卸载时未正确完成加载流程，强制重置状态");
            FailAndReset();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_isLoading || string.IsNullOrEmpty(_pendingTargetScene)) return;
        // Try to match by exact, filename-without-ext, or contains (case-insensitive)
        var pendingName = Path.GetFileNameWithoutExtension(_pendingTargetScene);
        if (string.Equals(scene.name, _pendingTargetScene, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(scene.name, pendingName, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(pendingName) && pendingName.IndexOf(scene.name, StringComparison.OrdinalIgnoreCase) >= 0))
        {
            Debug.Log($"[SceneLoadManager] Detected scene loaded via SceneManager: {scene.name}");
            HandleSceneActivated(scene.name);
        }
    }

    #region Public API

    /// <summary>
    /// 加载登录场景（默认不需要再显示 LoadingScene）。
    /// </summary>
    public void LoadLoginScene(bool useLoadingScreen = false, SceneLoadCallback onComplete = null)
    {
        LoadGameplayScene(LoginSceneKey, useLoadingScreen, onComplete, treatAsLogin: true);
    }

    /// <summary>
    /// 加载任意游戏（或登录）场景。所有非 Login/Loading 的都视为 Gameplay。
    /// </summary>
    /// <param name="sceneKey">Addressables 场景 key 或 label。</param>
    /// <param name="useLoadingScreen">是否先切换到 LoadingScene，再在该场景内部异步加载目标。</param>
    /// <param name="onComplete">目标场景激活后回调。</param>
    /// <param name="treatAsLogin">内部使用：为 true 时不做玩家保存销毁（登陆阶段通常没有玩家或逻辑不同）。</param>
    public void LoadGameplayScene(string sceneKey, bool useLoadingScreen = true, SceneLoadCallback onComplete = null, bool treatAsLogin = false)
    {
        if (string.IsNullOrEmpty(sceneKey))
        {
            Debug.LogWarning("[SceneLoadManager] 场景 key 为空");
            return;
        }
        if (_isLoading)
        {
            // 如果已经在加载，尝试检测当前激活 scene 是否就是目标（允许重复请求的快速容错）
            var active = SceneManager.GetActiveScene().name;
            if (string.Equals(active, sceneKey, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("[SceneLoadManager] 请求加载的场景已激活，直接回调: " + sceneKey);
                onComplete?.Invoke();
                return;
            }
            Debug.LogWarning("[SceneLoadManager] 正在加载其它场景，请稍后再试");
            return;
        }
        if (SceneManager.GetActiveScene().name == sceneKey)
        {
            Debug.Log("[SceneLoadManager] 请求加载的场景与当前相同，忽略: " + sceneKey);
            onComplete?.Invoke();
            return;
        }

        _isLoading = true;
        _pendingTargetScene = sceneKey;
        _pendingCallback = onComplete;
        // start watchdog to avoid permanent stuck
        CancelCts(ref _watchdogCts);
        _watchdogCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        LoadingWatchdogAsync(DefaultLoadWatchdogSeconds, _watchdogCts.Token).Forget();

        OnSceneLoadStarted?.Invoke(sceneKey);

        // 登录场景或普通 gameplay 进入前，把玩家数据保存并销毁（登录除外）
        if (!treatAsLogin)
        {
            SaveAndDestroyCurrentPlayer();
        }

        if (useLoadingScreen)
        {
            // 通过过渡场景加载：设置目标，先加载 LoadingScene (Single)，再由 LoadingScreenController 执行真正的目标加载。
            LoadingScreenController.TargetSceneAddress = sceneKey;
            LoadLoadingSceneThenWaitAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }
        else
        {
            // 直接加载 Addressables 场景（Single, activateOnLoad = true）
            DirectLoadAsync(sceneKey, this.GetCancellationTokenOnDestroy()).Forget();
        }
    }

    /// <summary>
    /// 由 LoadingScreenController 在目标场景完成激活时调用。（或 direct 模式加载成功后内部调用）
    /// </summary>
    public void HandleSceneActivated(string sceneAddress)
    {
        // Guard: ignore duplicate activations or spurious calls
        if (!_isLoading)
        {
            Debug.Log($"[SceneLoadManager] HandleSceneActivated ignored — not in loading state. Scene: {sceneAddress}");
            CurrentSceneName = sceneAddress; // still update current scene for correctness
            return;
        }

        CurrentSceneName = sceneAddress;
        _isLoading = false; // 标记完成
        CancelCts(ref _watchdogCts);
        var cb = _pendingCallback; // 缓存后清空，防止回调里再触发加载造成意外覆盖
        _pendingCallback = null;
        _pendingTargetScene = null;
        OnSceneActivated?.Invoke(sceneAddress);
        cb?.Invoke();
    }

    #endregion

    #region Internal Coroutines

    /// <summary>
    /// 加载 LoadingScene，然后等待它内部（LoadingScreenController）完成目标场景加载。
    /// </summary>
    private async UniTaskVoid LoadLoadingSceneThenWaitAsync(CancellationToken token)
    {
        try
        {
            // 预检查：确认 Addressables 中存在 LoadingScene 的位置，避免 InvalidKeyException 抛出
            var locationsHandle = Addressables.LoadResourceLocationsAsync(LoadingSceneKey);
            await locationsHandle.ToUniTask(cancellationToken: token);
            bool hasLocation = locationsHandle.Status == AsyncOperationStatus.Succeeded && locationsHandle.Result != null && locationsHandle.Result.Count > 0;
            Addressables.Release(locationsHandle);

            if (!hasLocation)
            {
                // Addressables 中没有 LoadingScene，则尝试加载"本地（Build Settings 中）"的 LoadingScene
                Debug.LogWarning($"[SceneLoadManager] 未在 Addressables 中找到 {LoadingSceneKey}，尝试通过 SceneManager 加载本地 Loading 场景。");

                var opLocal = SceneManager.LoadSceneAsync(LoadingSceneKey, LoadSceneMode.Single);
                if (opLocal == null)
                {
                    Debug.LogError($"[SceneLoadManager] 无法通过 SceneManager 加载本地 LoadingScene: {LoadingSceneKey}。将回退为直接加载目标场景。");
                    DirectLoadAsync(_pendingTargetScene, token).Forget();
                    return;
                }

                // 等待本地 LoadingScene 加载完成并激活
                await opLocal.ToUniTask(cancellationToken: token);
                // 本地 LoadingScene 激活后，由 LoadingScreenController 继续加载目标场景
                return;
            }

            // 加载过渡场景（Single 模式自动卸载当前）
            try
            {
                var loadingHandle = Addressables.LoadSceneAsync(LoadingSceneKey);
                await loadingHandle.ToUniTask(cancellationToken: token);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Debug.LogError($"[SceneLoadManager] 无法加载 Addressables 的 LoadingScene: {LoadingSceneKey}, 错误: {ex.Message}");
                Debug.LogWarning("[SceneLoadManager] 回退尝试本地加载或直接加载目标场景");
                var op = SceneManager.LoadSceneAsync(LoadingSceneKey, LoadSceneMode.Single);
                if (op == null)
                {
                    DirectLoadAsync(_pendingTargetScene, token).Forget();
                    return;
                }
                await op.ToUniTask(cancellationToken: token);
                return;
            }
            // 此时 Addressables 的 LoadingScene 已激活，LoadingScreenController 会自动开始加载 _pendingTargetScene。
            // 剩余逻辑（最终激活、回调）在 HandleSceneActivated 中触发。
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// 直接加载目标 Addressables 场景（Single）。
    /// </summary>
    private async UniTaskVoid DirectLoadAsync(string sceneKey, CancellationToken token)
    {
        try
        {
            _directLoadHandle = Addressables.LoadSceneAsync(sceneKey);
            var handle = _directLoadHandle;
            if (!handle.IsValid())
            {
                Debug.LogError($"[SceneLoadManager] Addressables 句柄无效: {sceneKey}");
                FailAndReset();
                return;
            }
            while (!handle.IsDone)
            {
                token.ThrowIfCancellationRequested();
                OnSceneLoadProgress?.Invoke(sceneKey, handle.PercentComplete);
                await UniTask.Yield(token);
            }
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[SceneLoadManager] 直接加载场景失败: {sceneKey}, 错误: {handle.OperationException}");
                FailAndReset();
                return;
            }
            // 已激活 - try to use actual loaded scene name to avoid address/name mismatch
            try
            {
                var loadedSceneName = handle.Result.Scene.name;
                HandleSceneActivated(loadedSceneName);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SceneLoadManager] 无法从句柄获取已加载场景名，回退使用 key: {sceneKey}. 错误: {ex.Message}");
                HandleSceneActivated(sceneKey);
            }
        }
        catch (OperationCanceledException) { }
    }

    #endregion

    #region Helpers

    private void SaveAndDestroyCurrentPlayer()
    {
        try
        {
            var player = CharacterRuntimeManager.Instance.CurrentPlayerCharacter();
            if (player != null)
            {
                CharacterRuntimeManager.Instance.SaveSceneTransitionPlayerState(player);
                UnityEngine.Object.Destroy(player.gameObject);
                CharacterRuntimeManager.Instance.UnsetPlayerInstance();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SceneLoadManager] 保存销毁玩家时出现异常: " + ex.Message);
        }
    }

    private async UniTaskVoid LoadingWatchdogAsync(float timeout, CancellationToken token)
    {
        try
        {
            float t = 0f;
            while (t < timeout)
            {
                token.ThrowIfCancellationRequested();
                if (!_isLoading) return;
                t += Time.unscaledDeltaTime;
                await UniTask.Yield(token);
            }
            if (_isLoading)
            {
                Debug.LogError($"[SceneLoadManager] Scene load timed out after {timeout} seconds for target: {_pendingTargetScene}. Resetting state.");
                FailAndReset();
            }
        }
        catch (OperationCanceledException) { }
    }

    private void FailAndReset()
    {
        _isLoading = false;
        CancelCts(ref _watchdogCts);
        _pendingCallback = null;
        _pendingTargetScene = null;
    }

    private void CancelCts(ref CancellationTokenSource cts)
    {
        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }
    }

    #endregion

    #region Backward Compatibility Aliases
    /// <summary>
    /// 兼容旧接口：带过渡加载场景的加载（默认使用 LoadingScene）。
    /// </summary>
    public void LoadLevelWithTransition(string sceneKey, SceneLoadCallback onLoadComplete = null)
    {
        LoadGameplayScene(sceneKey, true, onLoadComplete);
    }

    /// <summary>
    /// 兼容 TeleportManager 的语义：传送到一个游戏场景（默认使用 LoadingScene）。
    /// </summary>
    public void TeleportToScene(string sceneKey, bool useLoadingScreen = true, SceneLoadCallback onComplete = null)
    {
        LoadGameplayScene(sceneKey, useLoadingScreen, onComplete);
    }
    #endregion
}
