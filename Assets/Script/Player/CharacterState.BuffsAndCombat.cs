using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

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
        TempDefBuffAsync(percent, duration, this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid TempDefBuffAsync(float percent, float duration, CancellationToken token)
    {
        try
        {
            int add = Mathf.Max(0, Mathf.RoundToInt(Defence * percent));
            if (add > 0)
            {
                Defence += add;
                OnValueChange();
            }
            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, duration)), cancellationToken: token);
            if (add > 0)
            {
                Defence -= add;
                OnValueChange();
            }
        }
        catch (OperationCanceledException) { }
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

        // Phase 5: 发送攻击到 MMO 服务端
        SendAttackToMMO(target, baseDamage);
    }

    private void SendAttackToMMO(Transform target, float baseDamage)
    {
        // MMO 模式下 MonsterCombat.TakeDamage 已发 monster_attack，跳过旧格式
        if (GameModeConfig.IsMmoMode) return;

        var nm = FindFirstObjectByType<NetworkManager>();
        if (nm == null || !nm.IsConnected) return;

        var monster = target.GetComponent<MonsterBase>();
        if (monster == null || monster.NetworkId == 0) return;

        var mc = target.GetComponent<MonsterCombat>();
        var sync = FindFirstObjectByType<EntitySyncManager>();
        var entityId = sync != null ? sync.GetLocalPlayerEntityId() : 0;
        if (entityId == 0) return;

        var skillMultiplier = Attack > 0 ? baseDamage / Attack : 1f;
        float dist = Vector3.Distance(transform.position, target.position);

        nm.SendAttack(entityId, monster.NetworkId, Attack,
            mc != null ? mc.CurrentHealth : 100, Defence, CritChancePercent,
            skillMultiplier, dist);
    }
    #endregion
}

