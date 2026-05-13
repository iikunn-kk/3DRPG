using UnityEngine;
// 移除未使用的命名空间引用，保持文件整洁
using DamageNumbersPro;

public partial class CharacterState : MonoBehaviour, IDamageable
{
    #region 序列化字段 (保留在主文件中以避免 Inspector 引用丢失)
    [SerializeField] private CharacterStateEventSO characterStateEventSo;
    [SerializeField] private GameObjectEventSO playerDeathEventSo; // 保留旧引用避免Inspector丢失
    [SerializeField] private GameObjectEventSO playerRespawnEventSo;
    [SerializeField] private GameObjectEventSO playerBeginRespawnEventSo;
    [SerializeField] private BoolEventSO cameraDeathEventSo;
    [SerializeField] private DamageNumber physicsDamageNumber;
    [SerializeField] private DamageNumber magicDamageNumber;
    [SerializeField] private DamageNumber healthRegenDamageNumber;

    // 升级特效（粒子预制体），播放完后自动移除
    [Header("特效配置")]
    [Tooltip("升级时播放的粒子预制体")]
    [SerializeField] private GameObject levelUpEffectPrefab;
    [Tooltip("如果不为空，特效将从该 Transform 位置播放；否则默认从玩家头顶播放")]
    [SerializeField] private Transform levelUpEffectSpawnPoint;

    // 死亡流程配置
    [Header("死亡流程配置")]
    [Tooltip("死亡惩罚百分比 (0.1=10%)")]
    [Range(0f, 0.9f)]
    [SerializeField] private float deathPenaltyPercent = 0.10f;
    [Tooltip("如果 UIManager 未实现回调时的备用过场等待秒数")]
    [SerializeField] private float fallbackDeathTransitionSeconds = 5f;
    [Tooltip("是否在 Die() 中立即应用惩罚并保存（防止强退刷资源）")]
    [SerializeField] private bool applyPenaltyImmediately = true;
    [SerializeField] private LayerMask deathLayerName; // 玩家死亡后放入的物理层
    #endregion

    #region 核心属性和字段
    public CharacterData PlayerCharacterData { get; private set; }

    // 经验等级上限
    private const int MaxLevel = 100;

    // 基础及当前战斗属性
    public int CurrentHealth { get; set; }
    public int MaxHealth { get; set; }
    public float Speed { get; private set; }
    public int Attack { get; private set; }
    public int Defence { get; private set; }
    public int Exp { get; private set; }
    public int NeedExp { get; private set; }
    public int Level { get; private set; }
    public float HpRecoverySpeed { get; private set; }
    public float PhysicalDamage { get; private set; }
    public float MagicDamage { get; private set; }
    public string CharacterName { get; private set; }
    public CharacterProfession Profession { get; private set; }
    public float CritChancePercent { get; private set; }

    // 装备与 Buff 计算前的基础攻击力（含等级+装备，不含临时 Buff）
    private int _attackBeforeBuffs;

    // 状态与复活相关标记（其它 partial 复用）
    private bool _isDead;
    private Vector3 _plannedRespawnPosition;
    private bool _penaltyApplied;
    private bool _pendingRuntimeRespawn; // 仅运行时标记（不持久化）

    // 新增：标记核心 Init 是否已完成（供装备初始化判断）
    private bool _hasRunCoreInit;
    internal bool HasRunCoreInit => _hasRunCoreInit;

    // 原始组件/层数据
    private int _originalLayer;
    private bool _originalInteractionEnabled;
    private bool _deathPopupSpawned; // 仍保留（未来可能使用）
    private PlayerInteraction _interaction;
    private CharacterAnimationController _anim;
    private MoveMent _movement;

    // --- 可供 UIManager/外部调用的委托（可选，不在 Inspector 绑定事件） ---
    public System.Action OnDeathPopupShouldShow; // Die() 时调用，外部展示弹窗
                                                 //    public System.Action OnRespawnRuntimeDone;   // 实际复活完成后回调

    // 跨 partial 访问需要：对其它文件开放的序列化字段访问器（若未来需要可改成属性）
    internal CharacterStateEventSO CharacterStateEventSo => characterStateEventSo;
    internal GameObjectEventSO PlayerDeathEventSo => playerDeathEventSo;
    internal GameObjectEventSO PlayerRespawnEventSo => playerRespawnEventSo;
    internal GameObjectEventSO PlayerBeginRespawnEventSo => playerBeginRespawnEventSo;
    internal BoolEventSO CameraDeathEventSo => cameraDeathEventSo;
    internal DamageNumber PhysicsDamageNumber => physicsDamageNumber;
    internal DamageNumber MagicDamageNumber => magicDamageNumber;
    internal DamageNumber HealthRegenDamageNumber => healthRegenDamageNumber;
    internal float DeathPenaltyPercent => deathPenaltyPercent;
    internal float FallbackDeathTransitionSeconds => fallbackDeathTransitionSeconds;
    internal bool ApplyPenaltyImmediately => applyPenaltyImmediately;
    internal LayerMask DeathLayerName => deathLayerName;
    #endregion

    #region 生命周期与初始化
    public void Init(CharacterData data, Vector3 pos)
    {
        PlayerCharacterData = data;
        Exp = data.exp;
        Level = data.level;
        CurrentHealth = 0; // 初始化后再赋满血
        CharacterName = data.characterName;
        Profession = data.profession;
        var baseData = GameManager.Instance.playerCharacterStateDataSo.GetPlayerCharacterStateBaseData(data.profession);
        MaxHealth = baseData.GetMaxHp(data.level);
        Attack = baseData.GetAttack(data.level);
        _attackBeforeBuffs = Attack; // 初始化基础攻击（供后续 Buff 计算使用）
        Defence = baseData.GetDefence(data.level);
        NeedExp = baseData.GetNeedExp(data.level);
        HpRecoverySpeed = baseData.GetRegenHp(data.level);
        Speed = baseData.Speed;
        PhysicalDamage = 0f;
        MagicDamage = 0f;
        CritChancePercent = 0f;
        transform.position = pos;
        CharacterRuntimeManager.Instance.SetPlayerCharacter(this);

        // 初始化货币
        PlayerCurrencyManager.Instance.InitializeFromCharacterData(data);

        _interaction = GetComponent<PlayerInteraction>();
        _anim = GetComponent<CharacterAnimationController>();
        _movement = GetComponent<MoveMent>();
        _originalLayer = gameObject.layer;
        _originalInteractionEnabled = _interaction ? _interaction.enabled : false;

        // 直接满血
        CurrentHealth = MaxHealth;
        OnValueChange();
        // 标记核心初始化完成
        _hasRunCoreInit = true;
        // 尝试初始化装备（如果背包已经加载完会立即生效，否则等待 OnInventoryUpdated 事件）
        TryInitializeEquipmentFromInventory();
    }
    #endregion

    #region 公共数据更新通知
    private void OnValueChange()
    {
        characterStateEventSo.RaiseEvent(this, this);
    }
    #endregion

    #region 数据持久化
    public CharacterData GetCharacterDataForSave()
    {
        if (PlayerCharacterData == null) return null;
        PlayerCharacterData.exp = Exp;
        PlayerCharacterData.level = Level;
        // 统一写入满血，忽略当前血量
        PlayerCharacterData.hp = MaxHealth;
        PlayerCharacterData.position = transform.position;
        PlayerCharacterData.characterName = CharacterName;
        PlayerCharacterData.profession = Profession;
        PlayerCharacterData.gold = PlayerCurrencyManager.Instance.Money;
        PlayerCharacterData.gem = PlayerCurrencyManager.Instance.Diamonds;
        PlayerCharacterData.currentScene = SceneLoadManager.Instance.CurrentSceneName;
        TaskManager.Instance?.PopulateCharacterDataTasks(PlayerCharacterData);
        SkillManager.Instance?.PopulateCharacterDataSkills(PlayerCharacterData);
        return PlayerCharacterData;
    }
    #endregion

    #region Buff 汇总入口（由外部 Buff 系统调用）
    public void ApplyBuffTotals(int attackFlat, float critPercent)
    {
        Attack = _attackBeforeBuffs + Mathf.RoundToInt(attackFlat);
        CritChancePercent = Mathf.Max(0f, critPercent);
        OnValueChange();
    }
    #endregion

    // 升级特效播放（在一次 AddExp 调用中只会被触发一次）
    private void PlayLevelUpEffect()
    {
        if (levelUpEffectPrefab == null) return;
        Vector3 spawnPos = levelUpEffectSpawnPoint != null ? levelUpEffectSpawnPoint.position : (transform.position + Vector3.up * 1.8f);
        var instance = Instantiate(levelUpEffectPrefab, spawnPos, Quaternion.identity);
        StartCoroutine(DestroyWhenEffectFinished(instance));
        // 显示升级 toast
        UIManager.Instance.ShowToast("升级！当前等级" + Level);
    }

    private System.Collections.IEnumerator DestroyWhenEffectFinished(GameObject effectInstance)
    {
        if (effectInstance == null)
            yield break;

        var systems = effectInstance.GetComponentsInChildren<ParticleSystem>(true);
        if (systems == null || systems.Length == 0)
        {
            // 如果不是粒子特效，给一个安全的延迟后销毁
            yield return new WaitForSeconds(5f);
            if (effectInstance != null) Destroy(effectInstance);
            yield break;
        }

        bool anyAlive = true;
        // 等待所有子粒子系统播放结束
        while (anyAlive)
        {
            anyAlive = false;
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null) continue;
                if (ps.IsAlive(true))
                {
                    anyAlive = true;
                    break;
                }
            }
            if (anyAlive) yield return null;
        }

        if (effectInstance != null)
            Destroy(effectInstance);
    }
}