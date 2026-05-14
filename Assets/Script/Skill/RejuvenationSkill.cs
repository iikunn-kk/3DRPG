using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 回春术：
/// - 立即治疗施法者一部分生命值（按 SkillSO 的治疗公式）。
/// - Lv5+：额外在5秒内持续恢复“本次基础治疗量”的30%。
/// - Lv10+：在5秒内提高防御力50%（项目未区分物理/魔法防御，此处按单一 Defence 处理）。
/// 说明：本实现基于当前项目的 CharacterState 只有单一 Defence 属性，
/// 因此“物理和魔法防御+50%”被等效为 Defence +50%（持续5秒）。
/// </summary>
public class RejuvenationSkill : Skill
{
    [Header("可选：释放时的VFX（挂在施法者身上）")]
    [SerializeField] private GameObject castVfxPrefab;

    [Header("可选：HoT 持续回血的VFX（挂在玩家身上，通过 CharacterBuffs 管理，仅显示一个最新的Buff特效）")]
    [SerializeField] private GameObject hotVfxPrefab;

    private CharacterState _casterState;
    private CancellationTokenSource _hotCts;

    public override void Execute(Transform caster, PlayerSkill playerSkill)
    {
        base.Execute(caster, playerSkill);

        if (caster == null || playerSkill == null || playerSkill.SkillSO == null)
        {
            Destroy(gameObject);
            return;
        }

        // 播放共享 Buff 释放音效（与奥术智慧共用）
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayWeaponSound(SkillSoundType.Buff释放);
        }

        _casterState = caster.GetComponent<CharacterState>();
        if (_casterState == null)
        {
            Debug.LogWarning("RejuvenationSkill: 施法者缺少 CharacterState 组件");
            Destroy(gameObject);
            return;
        }

        if (castVfxPrefab != null)
        {
            var vfx = Instantiate(castVfxPrefab, caster.position, caster.rotation, caster);
            // 可选：释放后短时间内自动清理
            Destroy(vfx, 3f);
        }

        // 立即治疗：缓存技能SO和SkillManager的计算结果，避免重复访问
        float baseHeal = SkillManager.Instance != null
            ? SkillManager.Instance.GetHealAtLevel(playerSkill.SkillSO, playerSkill.Level)
            : playerSkill.SkillSO.baseHealAmount * (1f + playerSkill.SkillSO.perLevelHealAmountPercent * playerSkill.Level);
        int instantHeal = Mathf.Max(0, Mathf.RoundToInt(baseHeal));
        _casterState.Heal(instantHeal);

        // HoT（Lv5+）：5秒恢复基础治疗量30%
        if (playerSkill.Level >= 5 && baseHeal > 0f)
        {
            // 使用整数总量分配以避免每秒浮点四舍五入误差
            int totalHotInt = Mathf.Max(0, Mathf.RoundToInt(baseHeal * 0.3f));
            if (totalHotInt > 0)
            {
                _hotCts?.Cancel();
                _hotCts?.Dispose();
                _hotCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
                ApplyHoTAsync(_casterState, totalHotInt, 5f, _hotCts.Token).Forget();

                // 注册HoT的Buff特效（只显示最新的一个Buff特效）
                if (hotVfxPrefab != null)
                {
                    var buffs = _casterState.GetComponent<CharacterBuffs>();
                    if (buffs == null) buffs = _casterState.gameObject.AddComponent<CharacterBuffs>();
                    string source = "RejuvenationHoT";
                    buffs.RegisterBuffVisual(source, hotVfxPrefab);
                    // 绑定一个0值的再生Buff，纯粹用于时序管理VFX（5秒后自动过期 -> 自动清理视觉）
                    buffs.ApplyBuff(source, CharacterBuffs.BuffType.RegenPerSecond, 0f, 5f);
                    // 兼容兜底：在HoT结束后尝试手动移除该可视化（5秒）
                    UnregisterVfxAfterAsync(buffs, source, 5f).Forget();
                }
            }
        }

        // Lv10+：5秒内防御力 +50%
        if (playerSkill.Level >= 10)
        {
            _casterState.ApplyTemporaryDefenceBuffPercent(0.5f, 5f);
        }

        // 自行销毁：等待所有持续效果结束（最大等待时间5.1秒）
        SelfDestructAfterAsync(5.1f).Forget();
    }

    private async UniTaskVoid ApplyHoTAsync(CharacterState cs, int totalIntAmount, float duration, CancellationToken token)
    {
        try
        {
            // 将整数总量均匀分配到 ticks 秒，前 remainder 秒多加1以补偿余数
            if (cs == null || totalIntAmount <= 0)
                return;

            int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
            int basePerTick = totalIntAmount / ticks;
            int remainder = totalIntAmount % ticks; // 前 remainder 次多 +1

            for (int i = 0; i < ticks; i++)
        {
            token.ThrowIfCancellationRequested();
            int amount = basePerTick + (i < remainder ? 1 : 0);
            if (amount > 0 && cs != null)
            {
                cs.Heal(amount);
            }
            await UniTask.Delay(TimeSpan.FromSeconds(1f), cancellationToken: token);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async UniTaskVoid UnregisterVfxAfterAsync(CharacterBuffs buffs, string source, float sec)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, sec)));
            if (buffs != null && !string.IsNullOrEmpty(source))
            {
                buffs.UnregisterBuffVisual(source);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async UniTaskVoid SelfDestructAfterAsync(float maxWait)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, maxWait)));
            if (this != null && gameObject != null)
            {
                Destroy(gameObject);
            }
        }
        catch (OperationCanceledException) { }
    }
}
