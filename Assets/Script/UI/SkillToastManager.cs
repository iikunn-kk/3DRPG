using System;
using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 技能提示专用的 Toast（单条、可自定义位置、无动画）。
/// - 将该组件挂到 MainCanvas 下任意位置，通过 RectTransform 手动配置位置/对齐；
/// - 仅显示一条提示；
/// - 可配置显示时长（小于等于 0 表示常显）；
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SkillToastManager : MonoBehaviour
{
    [Header("UI 引用")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("默认显示时长（秒），小于等于 0 表示常显")]
    [SerializeField] private float defaultDuration = 1.6f;

    private CancellationTokenSource _autoHideCts;
    // 如果在该 GameObject（或父物体）处于非激活状态时调用 Init，则延迟启动自动隐藏
    private float _pendingAutoHideDuration = -1f;

    private void Awake()
    {
        if (messageText == null)
        {
#if UNITY_2023_2_OR_NEWER
            messageText = GetComponentInChildren<TextMeshProUGUI>(true);
#else
            messageText = GetComponentInChildren<TextMeshProUGUI>();
#endif
        }
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        // 如果在该物体（或其父物体）处于非激活状态时调用了 Init 方法并请求自动隐藏，
        // 则在该物体启用时启动异步任务。
        if (_pendingAutoHideDuration > 0f)
        {
            if (_autoHideCts == null || _autoHideCts.IsCancellationRequested)
            {
                _autoHideCts?.Dispose();
                _autoHideCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
                AutoHideAfterAsync(_pendingAutoHideDuration, _autoHideCts.Token).Forget();
            }
            _pendingAutoHideDuration = -1f;
        }
    }

    public void Show(string message, float duration = -1f)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
        gameObject.SetActive(true);

        // 取消之前的自动隐藏任务
        if (_autoHideCts != null)
        {
            _autoHideCts.Cancel();
            _autoHideCts.Dispose();
            _autoHideCts = null;
        }

        float dur = duration > 0f ? duration : defaultDuration;
        if (dur > 0f)
        {
            // 仅在该 GameObject 在层级视图中处于激活状态时启动任务；否则延迟到 OnEnable 时启动。
            if (gameObject.activeInHierarchy && this.isActiveAndEnabled)
            {
                _autoHideCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
                AutoHideAfterAsync(dur, _autoHideCts.Token).Forget();
                _pendingAutoHideDuration = -1f;
            }
            else
            {
                _pendingAutoHideDuration = dur;
            }
        }
    }

    public void Hide()
    {
        if (_autoHideCts != null)
        {
            _autoHideCts.Cancel();
            _autoHideCts.Dispose();
            _autoHideCts = null;
        }
        // 清除任何延迟的自动隐藏请求
        _pendingAutoHideDuration = -1f;
        gameObject.SetActive(false);
    }

    private async UniTaskVoid AutoHideAfterAsync(float sec, CancellationToken token)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(sec), cancellationToken: token);
            Hide();
        }
        catch (OperationCanceledException)
        {
            // 取消操作时无需处理
        }
    }
}
