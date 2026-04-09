
using System;
using DG.Tweening;
using MongoDB.Bson;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 普通攻击（奥术射线）的简易 UI：
/// - 不在 SkillQuickButtonBar 中；
/// - 显示“正在施放”状态（可选一个指示图标/条）和提示文本（未面向目标时）；
/// - 通过 BoolEventSO（NormalAttackController 发）或手动事件绑定这些方法。
/// </summary>
public class NormalAttackUI : MonoBehaviour
{
    [SerializeField] private Image highLightImage; // 可选：高亮图标
    // 绑定到 NormalAttackController 的“开始引导”
    private void OnChannelStart()
    {
        transform.DOScale(0.8f, 0.3f);
        highLightImage.gameObject.SetActive(true);
        highLightImage.DOFade(1f, 0.3f);
        highLightImage.transform.DORotate(new Vector3(0, 0, -360), 2f, RotateMode.FastBeyond360).SetLoops(-1, LoopType.Restart);
    }

    // 绑定到 NormalAttackController 的“结束引导”
    private void OnChannelEnd()
    {
        transform.DOScale(1f, 0.3f);
        highLightImage.DOKill();
        highLightImage.gameObject.SetActive(false);
    }

    // 适配 BoolEventSO：true=开始，false=结束
    public void OnChannelState(bool isChanneling)
    {
        if (isChanneling) OnChannelStart();
        else OnChannelEnd();
    }

    private void OnDisable()
    {
        transform.DOKill();
        highLightImage.DOKill();
        highLightImage.transform.DOKill();
    }
}
