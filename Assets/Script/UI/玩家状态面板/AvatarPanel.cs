using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening; // 1. 引入 DOTween 命名空间

public class AvatarPanel : MonoBehaviour
{
    [Header("UI组件引用")]
    [SerializeField] private Image headImage;
    [SerializeField] private Image hpBarFill;
    [SerializeField] private Image expBarFill;
    [SerializeField] private Image damagePreviewImage; // 用于伤害预览的背景血条
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text expText;

    [Header("职业头像")]
    [Tooltip("头像列表，请按照 CharacterState 中的 Profession 枚举顺序排列")]
    [SerializeField] private List<Sprite> professionSprites;

    // 2. 用于管理和清理动画的 Tween 变量
    private Tween _damagePreviewTween;

    private void OnDestroy()
    {
        // 8. 在对象销毁时，安全地清理所有动画，防止内存泄漏或编辑器报错
        _damagePreviewTween?.Kill();
    }

    /// <summary>
    /// 初始化玩家面板的静态信息
    /// </summary>
    public void Init(CharacterState characterState)
    {
        headImage.sprite = professionSprites[(int)characterState.Profession];

        // 初始化时，让所有UI元素瞬间到达初始状态，不播放动画
        float initialHpFill = (float)characterState.CurrentHealth / characterState.MaxHealth;
        hpBarFill.fillAmount = initialHpFill;
        damagePreviewImage.fillAmount = initialHpFill; // 保证预览条和血条初始位置一致

        UpdateAvatar(characterState);
    }

    /// <summary>
    /// 核心更新函数，负责处理所有动态数据的显示和动画
    /// </summary>
    public void UpdateAvatar(CharacterState characterState)
    {
        // --- 更新文本信息 ---
        nameText.text = characterState.CharacterName;
        levelText.text = "LV" + characterState.Level;
        hpText.text = $"{characterState.CurrentHealth} / {characterState.MaxHealth}";
        if (characterState.Level >= 100)
        {
            expBarFill.fillAmount = 1f;
            expText.text = "MAX";

        }
        else
        {
            expText.text = $"{characterState.Exp} / {characterState.NeedExp}";
            expBarFill.fillAmount = (float)characterState.Exp / characterState.NeedExp;
        }
        // --- 更新血条逻辑 ---
        float targetHpFill = (float)characterState.CurrentHealth / characterState.MaxHealth;

        // 3. 先清理上一次未完成的动画，确保动画不会冲突
        _damagePreviewTween?.Kill();

        // 4. 判断是受伤还是治疗
        if (targetHpFill < damagePreviewImage.fillAmount) // 受伤了
        {
            // 5. 前景血条立即变化，给玩家最快的反馈
            hpBarFill.fillAmount = targetHpFill;

            // 6. 伤害预览条（背景）使用 DOTween 缓动到目标血量
            //    可以加入一个短暂的延迟，让前景血条先变化，效果更明显
            _damagePreviewTween = damagePreviewImage.DOFillAmount(targetHpFill, 0.8f)
                .SetEase(Ease.OutQuad)
                .SetDelay(0.2f);
        }
        else // 治疗或血量无变化
        {
            // 7. 治疗时，让两个血条都立即更新，可以根据需要改为缓动效果
            damagePreviewImage.fillAmount = targetHpFill;
            hpBarFill.fillAmount = targetHpFill;
        }
    }

    #region "辅助和测试功能"

    public void GrantPlayerMaxLevel()
    {
        CharacterState cs = CharacterRuntimeManager.Instance.CurrentPlayerCharacter();
        if (cs == null)
        {
            Debug.LogWarning("[AvatarPanel] 无法获取 CharacterState 实例，无法升满级。");
            return;
        }

        const int targetLevel = 100;
        if (cs.Level >= targetLevel)
        {
            return;
        }

        int safetyCounter = 0;
        while (cs.Level < targetLevel && safetyCounter < 1000)
        {
            int need = cs.NeedExp - cs.Exp;
            if (need <= 0) need = cs.NeedExp > 0 ? cs.NeedExp : 1;
            cs.AddExp(need);
            safetyCounter++;
        }
        SaveCoordinator.Instance.SaveCurrentCharacterData();
        UpdateAvatar(cs);
    }

    public void TestTakeHalfDamage()
    {
        CharacterState cs = CharacterRuntimeManager.Instance.CurrentPlayerCharacter();
        if (cs == null)
        {
            Debug.LogWarning("[AvatarPanel] 无法获取 CharacterState 实例，无法测试掉血。");
            return;
        }

        int damage = cs.MaxHealth / 2;
        if (cs.CurrentHealth - damage <= 0)
        {
            Debug.Log("[AvatarPanel] 玩家血量不足，无法承受一半伤害。");
            return;
        }

        cs.TakeDamage(damage, AttackType.物理攻击);

        // 伤害计算后，只需要调用一次 UpdateAvatar 即可自动处理所有UI和动画
        UpdateAvatar(cs);
    }

    #endregion
}