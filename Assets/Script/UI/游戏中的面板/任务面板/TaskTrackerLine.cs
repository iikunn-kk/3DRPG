using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TaskTrackerLine : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private TMP_Text nameText;
    [Header("追踪中的任务图标")]
    [SerializeField] private Sprite targetIcon;

    [SerializeField] private Image icon;
    
    private Sprite _defaultIconSprite; // 保存行初始 icon 以便恢复

    private BaseTask _task;
    private bool _isTracked;
    private Action<int> _onClickAction;

    /// <summary>该行对应的任务ID，未绑定则为 -1</summary>
    public int TaskId => _task != null ? _task.id : -1;

    // 支持外部注入点击回调
    public void SetData(BaseTask task, Action<int> onClick = null)
    {
        _task = task;
        _onClickAction = onClick;
        if (nameText != null && task != null)
            nameText.text = task.taskName;
        // 记录默认 icon（仅首次）
        if (_defaultIconSprite == null && icon != null)
            _defaultIconSprite = icon.sprite;
        // 初始追踪状态由外部（Panel/Service）设置，尝试保持与服务同步作为回退
        if (TaskTrackingService.Instance != null)
            _isTracked = TaskTrackingService.Instance.CurrentTrackedTaskId == TaskId;
        // 根据当前追踪状态更新 icon
        UpdateIconByTrackedState();
        Refresh();
    }

    /// <summary>
    /// 外部显式设置该行是否为当前被追踪的任务
    /// </summary>
    public void SetTracked(bool tracked)
    {
        _isTracked = tracked;
        UpdateIconByTrackedState();
        Refresh();
    }

    /// <summary>
    /// 根据 _isTracked 更新 icon 显示（如果配置了 targetIcon）
    /// </summary>
    private void UpdateIconByTrackedState()
    {
        if (icon == null) return;
        if (_isTracked)
        {
            if (targetIcon != null)
                icon.sprite = targetIcon;
        }
        else
        {
            if (_defaultIconSprite != null)
                icon.sprite = _defaultIconSprite;
        }
    }

    /// <summary>
    /// 刷新显示（被外部批量刷新时调用）
    /// </summary>
    public void Refresh()
    {
        if (_task == null || targetText == null)
            return;
        // 确保 icon 与追踪状态一致（防止外部直接修改后不同步）
        UpdateIconByTrackedState();
        // 注意：_isTracked 由外部通过 SetTracked 管理，这里只依据该标记显示距离/状态
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        // 任务名第一行 (追加距离/状态)
        sb.Append(_task.taskName);
        if (_isTracked)
        {
            if (TaskTrackingService.Instance != null && TaskTrackingService.Instance.TryGetArrowTargetFast(out _, out bool inside, out float distSqr, out _, out _, out _))
            {
                if (inside)
                    sb.Append(" [已到达]");
                else
                {
                    // 只在需要显示时做开方计算，避免每次刷新都调用 Mathf.Sqrt
                    int disp = Mathf.RoundToInt(Mathf.Sqrt(distSqr));
                    sb.AppendFormat(" ({0}m)", disp);
                }
            }
        }
        // 逐行列出目标
        foreach (var obj in _task.objectives)
        {
            sb.Append("\n  • ");
            switch (obj.objectiveType)
            {
                case ObjectiveType.击杀敌人: sb.Append("击杀:"); break;
                case ObjectiveType.收集物品: sb.Append("收集:"); break;
                case ObjectiveType.和Npc对话: sb.Append("对话:"); break;
                default: sb.Append("目标:"); break;
            }
            sb.Append(' ').Append(obj.currentAmount).Append('/').Append(obj.requiredAmount);
        }
        targetText.text = sb.ToString();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_task == null) return;
        _onClickAction?.Invoke(_task.id);
    }
}
