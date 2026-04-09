using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;

public class SkillQuickMod : MonoBehaviour
{
    [Header("UI组件")]
    [SerializeField] private Image iconImage; // 技能图标
    [SerializeField] private Image cooldownOverlay; // 冷却遮罩（fillAmount 表示进度）
    [SerializeField] private TextMeshProUGUI cooldownText; // 冷却剩余时间文本（可为空）
    [SerializeField] private Button skillButton; // 槽位按钮（用于点击施放）
    [SerializeField] private TextMeshProUGUI hotkeyLabel; // 可选：显示热键的文本标签
    [SerializeField] private TMP_Text skillName;
    private SkillController _skillController; // 所属玩家的 SkillController（由 SkillQuickButtonBar 注入）
    private string _skillID; // 当前槽位绑定的技能 ID

    // 状态跟踪
    private bool _isSkillOnCooldown; // 新增：跟踪技能自身的冷却状态

    // GCD（全局冷却）显示状态缓存
    private bool _gcdActive;
    private float _gcdRemaining;
    private float _gcdTotal;

    /// <summary>
    /// 初始化槽位并绑定数据/回调。
    /// - skillID: 要绑定显示的技能 ID
    /// - controller: 玩家 SkillController（用于施放/获取 PlayerSkill 数据）
    /// - onShowDetails/onHideDetails: 指针悬停时显示/隐藏详情面板的回调
    /// 注意：本方法不会订阅冷却事件，冷却更新由外部统一广播到各 mod（SkillQuickButtonBar 管理订阅）。
    /// </summary>
    public void Init(string skillID, SkillController controller)
    {
        _skillID = skillID;
        _skillController = controller;
        
        var skillSO = SkillManager.Instance.GetSkillSo(_skillID);
        if (skillSO == null)
        {
            // 找不到对应的技能资源时，将该槽位隐藏以避免展示空数据
            gameObject.SetActive(false);
            return;
        }

        // 设置图标（已不再考虑拖拽隐藏状态）
        if (iconImage) iconImage.sprite = skillSO.icon;
        if (cooldownOverlay) cooldownOverlay.sprite = skillSO.icon;
        // 绑定按钮点击回调：点击时委托给 SkillController.CastSkill
        if (skillButton != null)
        {
            skillButton.onClick.RemoveAllListeners();
            skillButton.onClick.AddListener(OnSkillButtonClicked);
        }

        // 隐藏冷却显示的初始状态
        if (cooldownOverlay) cooldownOverlay.gameObject.SetActive(false);
        if (cooldownText) cooldownText.gameObject.SetActive(false);
        skillName.text = skillSO.skillName.ToString();
        // GCD 订阅由 SkillQuickButtonBar 管理，确保只有一个来源在转发事件
    }

    // ========== GCD（全局冷却）相关（由 SkillQuickButtonBar 转发） ===========

    /// <summary>
    /// 公共冷却开始：duration 为 GCD 总时长，显示 GCD 覆盖与数字
    /// </summary>
    public void OnGcdStarted(float duration)
    {
        _gcdActive = true;
        _gcdTotal = duration;
        _gcdRemaining = duration;
        UpdateGcdDisplay(); // 统一调用显示更新
    }

    /// <summary>
    /// 公共冷却进度更新：remaining 为剩余秒数
    /// </summary>
    public void OnGcdUpdated(float remaining)
    {
        _gcdActive = remaining > 0f;
        _gcdRemaining = remaining;
        UpdateGcdDisplay(); // 统一调用显示更新
    }

    /// <summary>
    /// 公共冷却结束：隐藏 GCD 显示（若技能本身无冷却）
    /// </summary>
    public void OnGcdEnded()
    {
        _gcdActive = false;
        _gcdRemaining = 0f;
        _gcdTotal = 0f;
        // 如果技能自身不在冷却中，则隐藏UI
        if (!_isSkillOnCooldown)
        {
            if (cooldownOverlay) cooldownOverlay.gameObject.SetActive(false);
            if (cooldownText) cooldownText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 根据当前 GCD 状态更新覆盖层与文本显示。
    /// 核心规则：仅当技能自身不在冷却时，才显示 GCD。
    /// </summary>
    public void UpdateGcdDisplay()
    {
        if (_isSkillOnCooldown) return; // 技能正在冷却，不显示GCD

        if (_gcdActive && _gcdTotal > 0f)
        {
            if (cooldownOverlay)
            {
                cooldownOverlay.gameObject.SetActive(true);
                cooldownOverlay.fillAmount = Mathf.Clamp01(_gcdRemaining / _gcdTotal);
            }
            if (cooldownText)
            {
                cooldownText.gameObject.SetActive(true);
                cooldownText.text = _gcdRemaining.ToString("F1");
            }
        }
    }

    private void OnDestroy()
    {
        // Slot 的事件订阅由 SkillQuickButtonBar 管理，因此此处不做额外清理以避免重复移除。
    }

    // ========== 技能冷却更新（由 SkillController 广播到 UI） =============

    /// <summary>
    /// 当某一技能冷却状态更新时调用（由 SkillController 的事件分发），
    /// payload 包含 SkillID、Remaining 与 Total，用于更新遮罩与文本。
    /// </summary>
    public void OnCooldownUpdate(SkillCooldownUpdatePayload payload)
    {
        // 拖拽相关检查已移除；始终响应对应技能的冷却更新
        if (payload.SkillID != _skillID) return;
        if (payload.Total <= 0f) return;

        _isSkillOnCooldown = true; // 标记技能进入冷却

        if (cooldownOverlay)
        {
            cooldownOverlay.gameObject.SetActive(true);
            cooldownOverlay.fillAmount = Mathf.Clamp01(payload.Remaining / payload.Total);
        }
        if (cooldownText)
        {
            cooldownText.gameObject.SetActive(true);
            cooldownText.text = payload.Remaining.ToString("F1");
        }
    }

    /// <summary>
    /// 当技能冷却结束时调用（由 SkillController 广播）。
    /// </summary>
    public void OnSkillReady(string skillID)
    {
        // 拖拽相关检查已移除；始终响应对应技能的准备事件
        if (skillID != _skillID) return;

        _isSkillOnCooldown = false; // 标记技能冷却结束

        // 如果GCD也已结束，则隐藏UI，否则让GCD的显示逻辑接管
        if (!_gcdActive)
        {
            if (cooldownOverlay) cooldownOverlay.gameObject.SetActive(false);
            if (cooldownText) cooldownText.gameObject.SetActive(false);
        }
        else
        {
            UpdateGcdDisplay(); // 技能冷却好了，但GCD还在，立即更新为GCD显示
        }
    }

    // ========== 辅助与回调方法 =============

    /// <summary>
    /// 返回当前槽位绑定的技能 ID（用于布局保存/交换）。
    /// </summary>
    public string GetSkillID()
    {
        return _skillID ?? string.Empty;
    }

    /// <summary>
    /// 设置并显示快捷键标签文本（可为空表示隐藏）。
    /// </summary>
    public void SetHotkeyLabel(string text)
    {
        if (hotkeyLabel == null) return;
        hotkeyLabel.text = string.IsNullOrEmpty(text) ? string.Empty : text;
        hotkeyLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    /// <summary>
    /// 按钮点击回调：委托 SkillController 进行施放逻辑（CastSkill）。
    /// </summary>
    private void OnSkillButtonClicked()
    {
        if (_skillController == null || string.IsNullOrEmpty(_skillID)) return;
        _skillController.CastSkill(_skillID);
    }
    
}
