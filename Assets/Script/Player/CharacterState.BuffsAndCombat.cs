using UnityEngine;
using System.Collections;

// 临时 Buff / 暴击 / 伤害输出相关
public partial class CharacterState
{
    #region 暴击 & 伤害事件
    public event System.Action OnPlayerCriticalHit;                     // 暴击触发
    public event System.Action<Transform, int, bool> OnDamageDealt;     // 造成伤害 (目标, 数值, 是否暴击)
    #endregion

    #region 暴击率操作
    public void AddCritChance(float percent)
    {
        CritChancePercent = Mathf.Max(0f, CritChancePercent + percent);
        OnValueChange();
    }

    public void RaiseCriticalHit()
    {
        OnPlayerCriticalHit?.Invoke();
    }
    #endregion

    #region 临时防御 Buff
    /// <summary>
    /// 持续一定时间的百分比防御提升（可叠加 / 独立计时）。
    /// </summary>
    public void ApplyTemporaryDefenceBuffPercent(float percent, float duration)
    {
        StartCoroutine(TempDefBuffRoutine(percent, duration));
    }

    private IEnumerator TempDefBuffRoutine(float percent, float duration)
    {
        int add = Mathf.Max(0, Mathf.RoundToInt(Defence * percent));
        if (add > 0)
        {
            Defence += add;
            OnValueChange();
        }
        yield return new WaitForSeconds(Mathf.Max(0f, duration));
        if (add > 0)
        {
            Defence -= add;
            OnValueChange();
        }
    }
    #endregion

    #region 造成伤害接口
    /// <summary>
    /// 统一的玩家对外伤害调用（含暴击判定）。
    /// baseDamage 通常由技能 / 普通攻击流程计算后传入。
    /// </summary>
    public void DealDamageTo(Transform target, float baseDamage, bool forceCrit = false, AttackType attackType = AttackType.物理攻击)
    {
        if (target == null) return;
        bool isCrit = forceCrit;
        if (!isCrit && CritChancePercent > 0f)
        {
            isCrit = Random.value < (CritChancePercent / 100f);
        }
        float critMultiplier = 2f; // 可拓展：由装备 / Buff 决定
        int finalDamage = Mathf.Max(0, Mathf.RoundToInt(baseDamage * (isCrit ? critMultiplier : 1f)));

        var dmgable = target.GetComponent<IDamageable>();
        if (dmgable != null)
        {
            dmgable.TakeDamage(finalDamage, attackType);
        }
        else
        {
            var monsterCombat = target.GetComponent<MonsterCombat>();
            if (monsterCombat != null)
            {
                monsterCombat.TakeDamage(finalDamage);
            }
        }

        if (isCrit) RaiseCriticalHit();
        OnDamageDealt?.Invoke(target, finalDamage, isCrit);
    }
    #endregion
}

