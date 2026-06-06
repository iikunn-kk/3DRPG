using UnityEngine;

// 受击、死亡与复活相关逻辑
public partial class CharacterState
{
    #region 伤害处理
    // IDamageable 接口实现（带攻击类型）
    public void TakeDamage(int damage, AttackType attackType)
    {
        if (_isDead || Time.time < _invincibleUntil) return;
        int final = Mathf.Max(0, damage);

        // 物理 / 魔法 / 回血 预处理
        if (attackType == AttackType.物理攻击)
        {
            float reduction = CalculateArmorReduction(Defence);
            final = Mathf.Max(0, Mathf.RoundToInt(damage * (1f - reduction)));
            // 确保有最小伤害（否则高防或小数四舍五入后为0会没有数字显示）
            if (damage > 0 && final <= 0) final = 1;
        }
        else if (attackType == AttackType.魔法攻击)
        {
            final = Mathf.Max(0, damage);
            if (damage > 0 && final <= 0) final = 1;
        }
        else if (attackType == AttackType.回血技能)
        {
            Heal(damage);
            return;
        }

        int before = CurrentHealth;
        CurrentHealth -= final;
        OnValueChange();
        if (CurrentHealth > 0 && final > 0)
        {
            _anim?.PlayHurt();
        }

        if (final > 0)
        {
            if (attackType == AttackType.物理攻击 && physicsDamageNumber != null)
            {
                var dn = physicsDamageNumber.Spawn(transform.position + Vector3.up * 1.5f, final);
                dn.SetFollowedTarget(transform);
                dn.SetColor(new Color(1f, 0.6f, 0.5f));
                dn.SetScale(1f);
            }
            else if (attackType == AttackType.魔法攻击 && magicDamageNumber != null)
            {
                var dn = magicDamageNumber.Spawn(transform.position + Vector3.up * 1.6f, final);
                dn.SetFollowedTarget(transform);
                dn.SetColor(new Color(0.8f, 0.8f, 1f));
                dn.SetScale(1.05f);
            }
            else if (physicsDamageNumber == null && magicDamageNumber == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[Player.TakeDamage] 未绑定 DamageNumber 预制体，无法显示伤害数字");
#endif
            }
        }

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    #endregion

    #region 死亡与复活
    public void Die()
    {
        if (_isDead) return;
        _isDead = true;
        CurrentHealth = 0;
        OnValueChange();
        // 动画/碰撞层/移动锁定 → 由 PlayerDeathState.Enter() 处理
        _plannedRespawnPosition = ResolveNearestSpawnPoint();
        _pendingRuntimeRespawn = true;
        PlayerDeathEventSo?.RaiseEvent(gameObject, this);
        CameraDeathEventSo?.RaiseEvent(false, this);
        if (ApplyPenaltyImmediately)
            ApplyDeathPenaltyAndPersist();
    }

    public void OnDeathPopupConfirmed()
    {
        if (!_isDead) return;
        if (!_penaltyApplied)
        {
            ApplyDeathPenaltyAndPersist();
        }
    }
    /// <summary>
    /// 复活
    /// </summary>
    private void PerformRuntimeRespawn()
    {
        if (!_pendingRuntimeRespawn) return;
        transform.position = _plannedRespawnPosition;
        CurrentHealth = MaxHealth;
        _isDead = false;
        _pendingRuntimeRespawn = false;
        _penaltyApplied = false;
        _invincibleUntil = Time.time + respawnInvincibleDuration;  // 复活无敌保护
        RestoreLayerAndInteraction();// 恢复碰撞层+解锁移动
        CameraDeathEventSo?.RaiseEvent(true, this);  // 解锁相机
        OnValueChange();
        PlayerRespawnEventSo.RaiseEvent(gameObject, this);
    }

    public void ApplyDeathPenaltyAndPersist()
    {
        if (_penaltyApplied) return;
        int newExp = Mathf.RoundToInt(Exp * (1f - DeathPenaltyPercent));
        newExp = Mathf.Max(0, newExp);
        int expLost = Exp - newExp;
        Exp = newExp;
        int backupHp = CurrentHealth;
        CurrentHealth = MaxHealth; // 存档写满血
        var saveData = GetCharacterDataForSave();
        if (saveData != null)
        {
            saveData.position = _plannedRespawnPosition;
            saveData.hp = MaxHealth;
            _ = MongoDBManager.Instance.CreateAndSaveCharacterData(saveData);
        }
        CurrentHealth = backupHp;
        var panel = UIManager.Instance.OpenPanel<DeathPopupPanel>(out bool isOpen);
        if (isOpen)
            panel.GetComponent<DeathPopupPanel>().Init(expLost, PlayerBeginRuntimeRespawn);
        _penaltyApplied = true;
        OnValueChange();
    }

    public void PlayerBeginRuntimeRespawn()
    {
        if (!_pendingRuntimeRespawn) return;
        PlayerBeginRespawnEventSo?.RaiseEvent(gameObject, this);
        var panel = UIManager.Instance.OpenPanel<PlayerRespawnPanel>(out bool isOpen);
        if (isOpen)
        {
            // 等面板完全淡入（不透明）后才执行复活，避免透视看到角色瞬移
            panel.Init(() =>
            {
                PerformRuntimeRespawn();
                panel.OnPlayerRespawn(gameObject);
            });
        }
    }

    public void ApplyDeadLayerAndDisableInteraction()
    {
        gameObject.layer = DeathLayerName;
        if (_interaction)
        {
            _originalInteractionEnabled = _interaction.enabled;
            _interaction.enabled = false;
        }
        if (_movement)
        {
            _movement.LockPlayerControl();
        }
    }

    public void RestoreLayerAndInteraction()
    {
        gameObject.layer = _originalLayer;
        if (_interaction) _interaction.enabled = _originalInteractionEnabled;
        if (_movement) _movement.UnlockPlayerControl();
    }
    #endregion

    #region 工具函数
    private float CalculateArmorReduction(float armour)
    {
        if (Mathf.Approximately(armour, 0f)) return 0f;
        float reduction = (0.06f * armour) / (1f + 0.06f * armour);
        return Mathf.Clamp(reduction, -0.9f, 0.95f);
    }

    private Vector3 ResolveNearestSpawnPoint()
    {
        var mapManager = Object.FindFirstObjectByType<MapManager>();
        if (mapManager != null)
            return mapManager.GetSpawnPosition(transform.position);
        return transform.position;
    }
    #endregion
}
