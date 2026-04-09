using UnityEngine;

// 受击、死亡与复活相关逻辑
public partial class CharacterState
{
    #region 伤害处理
    // IDamageable 接口实现（带攻击类型）
    public void TakeDamage(int damage, AttackType attackType)
    {
        if (_isDead) return;
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
        _anim?.PlayDeath();
        _anim?.ForceLockAfterDeath();
        ApplyDeadLayerAndDisableInteraction();
        _movement?.LockPlayerControl();
        _plannedRespawnPosition = ResolveNearestSpawnPoint();
        _pendingRuntimeRespawn = true;
        try { PlayerDeathEventSo?.RaiseEvent(gameObject,this); } catch (System.Exception ex) { Debug.LogWarning($"playerDeathEventSo Raise 失败: {ex.Message}"); }
        CameraDeathEventSo?.RaiseEvent(false,this);
        if (ApplyPenaltyImmediately) { ApplyDeathPenaltyAndPersist(); }
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
        RestoreLayerAndInteraction();
        OnValueChange();
        PlayerRespawnEventSo.RaiseEvent(gameObject,this);
    }

    private void ApplyDeathPenaltyAndPersist()
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
        var panel= UIManager.Instance.OpenPanel<DeathPopupPanel>(out bool isOpen);
        if (isOpen)
        {
            panel.GetComponent<DeathPopupPanel>().Init(expLost,PlayerBeginRuntimeRespawn);
        }
        _penaltyApplied = true;
        OnValueChange();
    }

    public void PlayerBeginRuntimeRespawn()
    {
        if (!_pendingRuntimeRespawn) return;
        PlayerBeginRespawnEventSo?.RaiseEvent(gameObject,this);
        var panel= UIManager.Instance.OpenPanel<PlayerRespawnPanel>(out bool isOpen);
        if (isOpen)
        {
            panel.Init();
        }
        ExecuteRespawnUsingMapManager();
    }

    private void ExecuteRespawnUsingMapManager()
    {
        if (!_pendingRuntimeRespawn) return;
        _pendingRuntimeRespawn = false;
        var data = GetCharacterDataForSave();
        if (data != null)
        {
            data.position = _plannedRespawnPosition;
        }
        var mapManager = GameObject.FindGameObjectWithTag("MapManager").GetComponent<MapManager>();
        GameObject newPlayer = null;
        if (mapManager != null && data != null)
        {
            mapManager.SpawnPlayer(data);
            var current = GameManager.Instance.CurrentPlayerCharacter();
            if (current != null)
            {
                newPlayer = current.gameObject;
                current.CurrentHealth = current.MaxHealth;
            }
        }
        else
        {
            PerformRuntimeRespawn();
            newPlayer = gameObject;
        }
        if (newPlayer != null)
        {
            PlayerRespawnEventSo?.RaiseEvent(newPlayer,this);
        }
        if (newPlayer != null && newPlayer != this.gameObject)
        {
            Object.Destroy(this.gameObject);
        }
    }

    private void ApplyDeadLayerAndDisableInteraction()
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

    private void RestoreLayerAndInteraction()
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
        {
            return mapManager.GetNearestSpawnPoint(transform.position);
        }
        return transform.position;
    }
    #endregion
}
