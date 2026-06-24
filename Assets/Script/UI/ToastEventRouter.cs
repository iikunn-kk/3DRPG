using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Toast 事件路由器：集中把各类游戏事件转换成带图标的 Toast 提示。
/// - 通过 ScriptableObject IntEventSO 在 Inspector 绑定 OnMoneyChangedSO / OnDiamondsChangedSO 方法。
/// - 通过 CharacterState 静态事件监听消耗品回血与攻击力加成。
/// - 预留公共方法供任务完成、通用奖励等在 Inspector / 代码中直接调用。
/// 注意：此脚本只做“增量”提示（金币/钻石获得），支出不提示。
/// </summary>
public class ToastEventRouter : MonoBehaviour
{
    [Header("Sprite 配置（在 Inspector 拖入）")]
    [Tooltip("获得金币时的图标")] public Sprite goldGainSprite;
    [Tooltip("获得钻石时的图标")] public Sprite diamondGainSprite;
    [Tooltip("回血消耗品的图标")] public Sprite healConsumableSprite;
    [Tooltip("攻击力增益消耗品的图标")] public Sprite attackBuffConsumableSprite;
    [Tooltip("防御力增益消耗品的图标")] public Sprite defendBuffConsumableSprite;
    [Tooltip("魔法攻击力增益消耗品的图标")] public Sprite magicBuffConsumableSprite;
    [Tooltip("任务完成的图标")] public Sprite questCompleteSprite;
    [Tooltip("通用奖励/物品的图标 (备用)")] public Sprite genericRewardSprite;

    [Header("显示配置")]
    [Tooltip("金币、钻石获得 Toast 显示自定义时长（<=0 使用 Toast 默认值）")] public float currencyToastDuration = 0f;
    [Tooltip("回血/攻击力消耗品/防御力消耗品/魔法攻击力消耗品 Toast 显示自定义时长（<=0 使用默认值）")] public float consumableToastDuration = 0f;
    [Tooltip("任务完成 Toast 显示自定义时长（<=0 使用默认值）")] public float questToastDuration = 0f;

    [Header("格式设置")]
    [Tooltip("金币提示格式，{delta} 会被替换为获得的数量")] public string goldFormat = "获得金币 +{delta}";
    [Tooltip("钻石提示格式，{delta} 会被替换为获得的数量")] public string diamondFormat = "获得钻石 +{delta}";
    [Tooltip("回血提示格式，{value} 会被替换为实际治疗量")] public string healFormat = "恢复生命 +{value}";
    [Tooltip("攻击力增益提示格式，{value} 会被替换为数值或百分比")] public string attackBuffFormat = "攻击力提升 +{value}";
    [Tooltip("防御力增益提示格式，{value} 会被替换为数值或百分比")] public string defendBuffFormat = "防御力提升 +{value}";
    [Tooltip("魔法攻击力增益提示格式，{value} 会被替换为数值或百分比")] public string magicBuffFormat = "魔法攻击力提升 +{value}";
    [Tooltip("任务完成提示格式，{name} 会被替换为任务名")] public string questCompleteFormat = "完成任务：{name}";

    // 记录上一帧（或上次事件）金币与钻石，用于计算增量
    private int _lastMoney = -1;
    private int _lastDiamonds = -1;

    private void Start()
    {
        // 初始化基准值，避免第一次事件被识别为大量正增量
        if (CharacterService.Instance != null)
        {
            _lastMoney = CharacterService.Instance.Money;
            _lastDiamonds = CharacterService.Instance.Diamonds;
        }
    }

    private void OnEnable()
    {
        CharacterState.OnConsumableHealed += HandleConsumableHealed;
        CharacterState.OnAttackBuffItemUsed += HandleAttackBuffItemUsed;
        //七月进行编写
        CharacterState.OnDefenceBuffItemUsed += HandleDefenceBuffItemUsed;
        CharacterState.OnMagicAttackBuffItemUsed += HandleMagicAttackBuffItemUsed;
    }

    private void OnDisable()
    {
        CharacterState.OnConsumableHealed -= HandleConsumableHealed;
        CharacterState.OnAttackBuffItemUsed -= HandleAttackBuffItemUsed;
        //七月进行编写
        CharacterState.OnDefenceBuffItemUsed -= HandleDefenceBuffItemUsed;
        CharacterState.OnMagicAttackBuffItemUsed -= HandleMagicAttackBuffItemUsed;
    }

    #region ScriptableObject IntEventSO 绑定方法
    // 这些方法签名需要与 IntEventSO(onEventRaised Action<int>) 匹配
    // 在 Inspector 中把 moneyChangedEvent / diamondsChangedEvent 的 onEventRaised 绑定到这两个方法。

    public void OnMoneyChangedSO(int newAmount)
    {
        if (_lastMoney < 0)
        {
            _lastMoney = newAmount;
            return; // 首次不提示
        }
        int delta = newAmount - _lastMoney;
        _lastMoney = newAmount;
        if (delta > 0)
        {
            string msg = goldFormat.Replace("{delta}", delta.ToString());
            UIManager.Instance?.ShowToast(msg, goldGainSprite, currencyToastDuration);
        }
    }

    public void OnDiamondsChangedSO(int newAmount)
    {
        if (_lastDiamonds < 0)
        {
            _lastDiamonds = newAmount;
            return; // 首次不提示
        }
        int delta = newAmount - _lastDiamonds;
        _lastDiamonds = newAmount;
        if (delta > 0)
        {
            string msg = diamondFormat.Replace("{delta}", delta.ToString());
            UIManager.Instance?.ShowToast(msg, diamondGainSprite, currencyToastDuration);
        }
    }
    #endregion

    #region 消耗品事件处理
    private void HandleConsumableHealed(int healed)
    {
        if (healed <= 0) return;
        string msg = healFormat.Replace("{value}", healed.ToString());
        UIManager.Instance?.ShowToast(msg, healConsumableSprite, consumableToastDuration);
    }

    private void HandleAttackBuffItemUsed(float value, bool isPercent)
    {
        string display;
        if (isPercent)
        {
            // value 已经是百分比数值，如 10 表示 10%
            display = value.ToString("0.#") + "%";
        }
        else
        {
            display = value.ToString("0.#");
        }
        string msg = attackBuffFormat.Replace("{value}", display);
        UIManager.Instance?.ShowToast(msg, attackBuffConsumableSprite, consumableToastDuration);
    }


    private void HandleDefenceBuffItemUsed(float value, bool isPercent)
    {
        string display;
        if (isPercent)
        {
            // value 已经是百分比数值，如 10 表示 10%
            display = value.ToString("0.#") + "%";
        }
        else
        {
            display = value.ToString("0.#");
        }
        string msg = defendBuffFormat.Replace("{value}", display);
        UIManager.Instance?.ShowToast(msg, defendBuffConsumableSprite, consumableToastDuration);
    }
    private void HandleMagicAttackBuffItemUsed(float value, bool isPercent)
    {
        string display;
        if (isPercent)
        {
            // value 已经是百分比数值，如 10 表示 10%
            display = value.ToString("0.#") + "%";
        }
        else
        {
            display = value.ToString("0.#");
        }
        string msg = attackBuffFormat.Replace("{value}", display);
        UIManager.Instance?.ShowToast(msg, magicBuffConsumableSprite, consumableToastDuration);
    }

    #endregion

    #region 任务 / 其它外部调用接口（供 Inspector 或 代码直接调用）
    /// <summary>
    /// 任务完成时手动调用（或通过 UnityEvent / SO 事件绑定）。
    /// </summary>
    public void OnQuestCompleted(string questName)
    {
        if (string.IsNullOrEmpty(questName)) questName = "任务";
        string msg = questCompleteFormat.Replace("{name}", questName);
        UIManager.Instance?.ShowToast(msg, questCompleteSprite, questToastDuration);
    }

    /// <summary>
    /// 汎用奖励接口：可用于自定义内容（比如副本结算）。
    /// </summary>
    public void ShowGenericRewardToast(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        UIManager.Instance?.ShowToast(message, genericRewardSprite);
    }
    #endregion
}

