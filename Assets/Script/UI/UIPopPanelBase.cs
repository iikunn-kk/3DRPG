using UnityEngine;
using DG.Tweening;
using System;

/// <summary>
/// 所有UI面板的动画基类。
/// 要求挂载此脚本的对象上必须有一个 CanvasGroup 组件用于控制透明度。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public abstract class UIPopPanelBase : MonoBehaviour
{
    protected CanvasGroup canvasGroup;
    public float animationDuration = 0.3f;

    protected virtual void Awake()
    {
        EnsureCanvasGroup();
    }


    // 保障在某些极端调用顺序（例如外部脚本在本脚本 Awake 前反射或直接调用 Hide/Show）下也不会出现空引用
    protected void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                // 理论上 [RequireComponent] 会保证存在；若依旧为空（动态生成/编辑器异常删除），则补一个
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
                Debug.LogWarning($"[{GetType().Name}] CanvasGroup 丢失，已在运行时自动补加。");
            }
        }
    }

    public virtual void Show(Action onComplete = null)
    {
        EnsureCanvasGroup();
        if (canvasGroup == null) return; // 兜底
        transform.DOKill();
        canvasGroup.DOKill();

        gameObject.SetActive(true);
        transform.localScale = Vector3.one * 0.8f;
        transform.DOScale(Vector3.one, animationDuration).SetEase(Ease.OutBack);

        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, animationDuration).OnComplete(() =>
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            onComplete?.Invoke();
        });
    }

    /// <summary>
    /// 隐藏面板（带动画），并在动画结束后销毁GameObject。
    /// </summary>
    /// <param name="isDestroy">是否要销毁自己</param>
    /// <param name="onComplete">在销毁前执行的回调函数</param>
    public virtual void Hide(bool isDestroy = true, Action onComplete = null)
    {
        EnsureCanvasGroup();
        if (canvasGroup == null)
        {
            // 无法做动画，直接执行回调及销毁逻辑
            onComplete?.Invoke();
            if (isDestroy) Destroy(gameObject);
            return;
        }
        // 在动画开始前就杀死所有旧的动画，确保安全
        transform.DOKill();
        canvasGroup.DOKill();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUISound(UISoundType.关闭面板);
        }
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        transform.DOScale(Vector3.zero, animationDuration).SetEase(Ease.InBack);

        // 在动画完成的回调中执行销毁操作
        canvasGroup.DOFade(0f, animationDuration).OnComplete(() =>
        {
            // 先执行外部传入的回调
            onComplete?.Invoke();
            if (isDestroy)
            {
                Destroy(gameObject);
            }
        });
    }

    protected virtual void OnDisable()
    {
        // 若在编辑器切换场景等阶段，有可能还没初始化就被 Disable，做一次保障
        if (canvasGroup == null) return;
        transform.DOKill();
        canvasGroup.DOKill();
    }

    private void OnDestroy()
    {
        if (canvasGroup == null) return;
        transform.DOKill();
        canvasGroup.DOKill();
    }
}