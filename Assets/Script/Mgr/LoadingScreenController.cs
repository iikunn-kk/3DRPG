using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

/// <summary>
/// 控制加载界面，异步加载Addressable场景。
/// 特性：
/// - 平滑的进度条动画。
/// - 保证加载界面最少显示一定时间，提升体验。
/// - 加载完成后自动激活场景并通知管理器。
/// </summary>
public class LoadingScreenController : MonoBehaviour
{
    /// <summary>
    /// 静态变量，用于从上一个场景接收要加载的目标场景地址。
    /// </summary>
    public static string TargetSceneAddress { get; set; }

    [Header("UI 组件")]
    [SerializeField] private Slider progressBar; // 进度条
    [SerializeField] private TMP_Text progressText;  // 进度文本

    [Header("加载体验设置")]
    [Tooltip("无论加载多快，加载界面最少显示的总时长（秒）。这可以防止加载过快导致界面闪烁。")]
    [SerializeField] private float minimumTotalDisplayTime = 1.5f;

    [Tooltip("进度条UI追赶实际加载进度的速度。值越大，追赶越快。")]
    [SerializeField] private float smoothSpeed = 1.2f;

    private AsyncOperationHandle<SceneInstance> _loadOperationHandle;
    private float _displayedProgress; // 用于UI显示的平滑进度值

    void Start()
    {
        // 启动时重置UI
        _displayedProgress = 0f;
        UpdateUI();

        if (string.IsNullOrEmpty(TargetSceneAddress))
        {
            Debug.LogError("[LoadingScreenController] 目标场景地址(TargetSceneAddress)未设置！无法进行加载。");
            // 可以在此添加返回主菜单或错误处理的逻辑
            return;
        }

        StartCoroutine(LoadTargetSceneAsync());
    }

    /// <summary>
    /// 异步加载场景的核心协程。
    /// </summary>
    private IEnumerator LoadTargetSceneAsync()
    {
        // 1. 记录加载开始的时间点
        float startTime = Time.unscaledTime;

        // 预检查：目标场景是否存在于 Addressables
        var locationsHandle = Addressables.LoadResourceLocationsAsync(TargetSceneAddress);
        yield return locationsHandle;
        bool addressableExists = locationsHandle.Status == AsyncOperationStatus.Succeeded && locationsHandle.Result != null && locationsHandle.Result.Count > 0;
        Addressables.Release(locationsHandle);

        if (addressableExists)
        {
            // Addressables 路径（不立即激活）
            _loadOperationHandle = Addressables.LoadSceneAsync(TargetSceneAddress, activateOnLoad: false);

            // 等待资源加载过程完成并平滑 UI
            while (!_loadOperationHandle.IsDone)
            {
                if (_loadOperationHandle.Status == AsyncOperationStatus.Failed)
                {
                    Debug.LogError($"[LoadingScreenController] 加载场景 '{TargetSceneAddress}' 失败: {_loadOperationHandle.OperationException}");
                    yield break;
                }

                float realProgress = Mathf.Clamp01(_loadOperationHandle.PercentComplete);
                _displayedProgress = Mathf.MoveTowards(_displayedProgress, realProgress, smoothSpeed * Time.unscaledDeltaTime);
                UpdateUI();
                yield return null;
            }

            // 资源加载已完成，现在让UI动画播放到100%
            while (_displayedProgress < 1f)
            {
                _displayedProgress = Mathf.MoveTowards(_displayedProgress, 1f, smoothSpeed * Time.unscaledDeltaTime);
                UpdateUI();
                yield return null;
            }

            UpdateUI(true);

            // 最低显示时间
            float elapsedTime = Time.unscaledTime - startTime;
            if (elapsedTime < minimumTotalDisplayTime)
            {
                yield return new WaitForSecondsRealtime(minimumTotalDisplayTime - elapsedTime);
            }

            // 激活新场景
            var activateOp = _loadOperationHandle.Result.ActivateAsync();
            yield return activateOp;

            SceneLoadManager.Instance.HandleSceneActivated(TargetSceneAddress);

            TargetSceneAddress = null;
            if (_loadOperationHandle.IsValid())
            {
                Addressables.Release(_loadOperationHandle);
            }
            yield break;
        }
        else
        {
            // 非 Addressables 路径：通过 SceneManager 加载本地场景（延迟激活以确保最短显示时间与平滑进度）
            Debug.LogWarning($"[LoadingScreenController] 目标场景不在 Addressables 中，使用 SceneManager 加载: {TargetSceneAddress}");
            var op = SceneManager.LoadSceneAsync(TargetSceneAddress, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError($"[LoadingScreenController] 无法通过 SceneManager 加载场景: {TargetSceneAddress}");
                yield break;
            }

            // 推迟激活来控制展示时长
            op.allowSceneActivation = false;

            // 0 ~ 0.9 的加载阶段
            while (op.progress < 0.9f)
            {
                float realProgress = Mathf.Clamp01(op.progress);
                _displayedProgress = Mathf.MoveTowards(_displayedProgress, realProgress, smoothSpeed * Time.unscaledDeltaTime);
                UpdateUI();
                yield return null;
            }

            // 资源已准备好（~0.9），将UI缓慢推进到 100%
            while (_displayedProgress < 1f)
            {
                _displayedProgress = Mathf.MoveTowards(_displayedProgress, 1f, smoothSpeed * Time.unscaledDeltaTime);
                UpdateUI();
                yield return null;
            }
            UpdateUI(true);

            // 最低显示时间
            float elapsedTime = Time.unscaledTime - startTime;
            if (elapsedTime < minimumTotalDisplayTime)
            {
                yield return new WaitForSecondsRealtime(minimumTotalDisplayTime - elapsedTime);
            }

            // 允许激活场景；此后本对象会因切换场景而被销毁
            op.allowSceneActivation = true;

            // 等待真正完成，避免在某些平台上发生竞态
            while (!op.isDone)
            {
                yield return null;
            }

            // 不再显式调用 HandleSceneActivated，这会通过 SceneLoadManager.sceneLoaded 回调被检测到
        }
    }

    /// <summary>
    /// 根据当前进度更新UI元素。
    /// </summary>
    /// <param name="isComplete">是否强制显示为加载完成状态。</param>
    private void UpdateUI(bool isComplete = false)
    {
        if (isComplete)
        {
            if (progressBar != null) progressBar.value = 1f;
            if (progressText != null) progressText.text = "加载完成!";
        }
        else
        {
            if (progressBar != null) progressBar.value = _displayedProgress;
            if (progressText != null)
            {
                progressText.text = $"加载中... {Mathf.RoundToInt(_displayedProgress * 100)}%";
            }
        }
    }
}
