using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using Slider = UnityEngine.UI.Slider;

public class PlayerRespawnPanel : MonoBehaviour
{
    [SerializeField] private Slider respawnSlider;
    [SerializeField] private CanvasGroup canvas;
    [Header("respawnTime 是黑屏保护期，防止复活太快面板闪一下就没了。值越大黑屏越久，1.5s 是合理的过渡感")]
    [SerializeField] private float respawnTime = 1.5f;
    [Header("0.3秒淡入：画布alpha 0→1")]
    [SerializeField] private float durationCanvasTime = 0.3f;
    [Header("1.2秒进度条slider 0→1")]
    [SerializeField] private float durationSliderTime = 0.3f;
    private bool isRespawn;
    private CancellationTokenSource _respawnCts;
    private Action _onFadeInComplete;

    public void Init(Action onFadeInComplete = null)
    {
        _onFadeInComplete = onFadeInComplete;
        isRespawn = false;
        canvas.alpha = 0;
        respawnSlider.maxValue = 1;
        canvas.DOFade(1, durationCanvasTime).onComplete = () =>
        {
            respawnSlider.DOValue(1, durationSliderTime);
            _onFadeInComplete?.Invoke();
        };

        _respawnCts?.Cancel();
        _respawnCts?.Dispose();
        _respawnCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        WaitForRespawnAsync(_respawnCts.Token).Forget();
    }

    private async UniTaskVoid WaitForRespawnAsync(CancellationToken token)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(respawnTime), cancellationToken: token);
            await UniTask.WaitUntil(() => isRespawn, cancellationToken: token);

            canvas.DOFade(0f, 0.3f).onComplete = () =>
            {
                transform.DOKill();
                respawnSlider.DOKill();
                UIManager.Instance.ClosePanel<PlayerRespawnPanel>();
                Destroy(gameObject);
            };
        }
        catch (OperationCanceledException)
        {
            // 取消操作时无需处理
        }
    }

    public void OnPlayerRespawn(GameObject player)
    {
        isRespawn = true;
    }

    private void OnDestroy()
    {
        _respawnCts?.Cancel();
        _respawnCts?.Dispose();
    }
}
