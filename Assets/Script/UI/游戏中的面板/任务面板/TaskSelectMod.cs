using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TaskSelectMod : MonoBehaviour
{
    [SerializeField] private TMP_Text taskName;
    [SerializeField] private Image taskImage;
    [SerializeField] private Sprite taskNotComplete;
    [SerializeField] private Sprite taskComplete;
    // 新增：选中显示（可选）
    [Header("选中状态可视化")] [SerializeField] private Color normalNameColor = Color.white;
    [SerializeField] private Color selectedNameColor = Color.yellow;

    private BaseTask _taskData;
    private Action<BaseTask> _onTaskSelect;
    private bool _selected;

    public int TaskId => _taskData != null ? _taskData.id : -1;

    public void Init(BaseTask taskData, Action<BaseTask> onTaskSelect)
    {
        _taskData = taskData;
        _onTaskSelect = onTaskSelect;
        // 设置任务名称/图标
        taskName.text = taskData.taskName;
        taskImage.sprite = taskData.isCompleted ? taskComplete : taskNotComplete;
        // 安全注册按钮点击（若预制上有 Button 未在 Inspector 绑定）
        var btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveListener(OnTaskSelected); // 防重
            btn.onClick.AddListener(OnTaskSelected);
        }
        SetSelected(false);
    }

    /// <summary>
    /// 更新显示状态（完成进度改变时调用）
    /// </summary>
    public void UpdateDisplay()
    {
        if (_taskData == null) return;
        taskName.text = _taskData.taskName;
        taskImage.sprite = _taskData.isCompleted ? taskComplete : taskNotComplete;
    }

    public void OnTaskSelected()
    {
        _onTaskSelect?.Invoke(_taskData);
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (taskName != null)
        {
            taskName.color = _selected ? selectedNameColor : normalNameColor;
        }
    }
}