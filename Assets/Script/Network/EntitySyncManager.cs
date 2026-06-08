using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 从 WorldServer 接收 AOI 快照，创建/更新/销毁远程实体。
/// 挂载到场景中的空 GameObject 上（建议和 NetworkManager 同级）。
/// </summary>
public partial class EntitySyncManager : MonoBehaviour
{
    public static EntitySyncManager Instance { get; private set; }
    void Awake() { Instance = this; }
    [Header("远程实体 Prefab（兜底）")]
    [SerializeField] private GameObject _remotePlayerPrefab;
    [SerializeField] private GameObject _remoteMonsterPrefab;

    [Header("角色模型映射（按 modelType 选远程玩家 Prefab）")]
    [SerializeField] private CharacterSelectDataSO _characterSelectData;

    [Header("本地玩家引用（用于过滤自身）")]
    [SerializeField] private Transform _localPlayer;

    private readonly Dictionary<uint, GameObject> _entities = new();
    private readonly Dictionary<uint, byte> _remoteAtkCache = new();  // entityId → lastAtkTrigger
    private readonly Dictionary<uint, byte> _remoteSkillCache = new(); // entityId → lastSkillTrigger
    private uint _localPlayerEntityId;

    public void SetLocalPlayerEntityId(uint id) => _localPlayerEntityId = id;
    public uint GetLocalPlayerEntityId() => _localPlayerEntityId;

    /// <summary>收到服务端快照数据（JSON）时调用</summary>
    public void ApplySnapshot(string json)
    {
        // 限制日志频率，避免刷屏
        _snapshotCount++;
#if UNITY_EDITOR
        if (_snapshotCount % 1000 == 1)
            Debug.Log($"[EntitySync] 快照累计 #{_snapshotCount}");
#endif

        try
        {
            // 快速过滤非快照消息（不含 entities 字段就不是快照）
            if (!json.Contains("\"entities\"")) return;

            var snapshot = JsonUtility.FromJson<PlayerSnapshot>(json);
            if (snapshot == null || snapshot.entities == null) return;

            // 首次收到快照时自动设置本地玩家 entityId
            if (_localPlayerEntityId == 0 && !string.IsNullOrEmpty(NetworkManager.Instance.PlayerUid))
            {
                foreach (var e in snapshot.entities)
                {
                    if (e.entityType == 0 && e.uid == NetworkManager.Instance.PlayerUid)
                    {
                        _localPlayerEntityId = e.entityId;
                        Debug.Log($"[EntitySync] 自动识别本地玩家 entityId={_localPlayerEntityId} (uid={NetworkManager.Instance.PlayerUid})");
                        break;
                    }
                }
            }

            foreach (var e in snapshot.entities)
            {
                // 本地玩家：同步服务端权威 HP + guildId（公会创建/加入/离开）
                if (e.entityId == _localPlayerEntityId || (e.entityType == 0 && e.uid == NetworkManager.Instance.PlayerUid))
                {
                    var localState = CharacterRuntimeManager.Instance?.CurrentPlayerCharacter();
                    if (localState != null)
                    {
                        if (e.hp != localState.CurrentHealth)
                        {
                            localState.CurrentHealth = e.hp;
                            if (e.hp <= 0) localState.Die();
                        }
                    }
                    // MMO 公会同步：更新本地角色的 guildId
                    var cd = SessionManager.Instance?.CurrentCharacter;
                    if (cd != null && !string.IsNullOrEmpty(e.uid) && cd.guildId != e.guildId)
                    {
                        cd.guildId = e.guildId ?? "";
                        SessionManager.Instance.SetCurrentCharacterData(cd);
                    }
                    continue;
                }

#if UNITY_EDITOR
                // Phase 5 诊断：每 200 帧统计实体类型（仅 Editor）
                if (_snapshotCount % 200 == 1)
                {
                    int playerCount = 0, monsterCount = 0;
                    foreach (var ee in snapshot.entities)
                    {
                        if (ee.entityType == 0) playerCount++; else monsterCount++;
                    }
                    Debug.Log($"[EntitySync] 快照#{_snapshotCount}: 玩家={playerCount} 怪物={monsterCount}");
                }
                if (!_loggedMonsterJson)
                {
                    foreach (var ee in snapshot.entities)
                    {
                        if (ee.entityType == 1)
                        {
                            Debug.Log($"[EntitySync] 首条怪物快照: {json.Substring(0, Mathf.Min(json.Length, 200))}");
                            _loggedMonsterJson = true;
                            break;
                        }
                    }
                }
#endif

                ApplyEntity(e);
            }
        }
        catch (Exception ex) { Debug.LogWarning($"[EntitySync] 解析快照失败: {ex.Message}"); }
    }

    private int _snapshotCount;
    private bool _loggedMonsterJson;

    void ApplyEntity(EntitySnapshot e)
    {
        // 怪物（entityType=1）：服务端权威 HP/位置/状态
        if (e.entityType == 1)
        {
            var localMonster = MonsterBase.FindByNetworkId(e.entityId);
            if (localMonster == null)
            {
                // 首次匹配：位置接近的本地怪物 → 绑定服务端 entityId
                var snapPos = new Vector3(e.posX, e.posY, e.posZ);
                localMonster = MonsterBase.FindByPositionProximity(snapPos, 15f);
                if (localMonster != null && localMonster.NetworkId != e.entityId)
                {
                    MonsterBase.UnregisterNetwork(localMonster.NetworkId);
                    localMonster.NetworkId = e.entityId;
                    MonsterBase.RegisterNetwork(localMonster);
                }
            }
            if (localMonster != null)
            {
                // MMO 复活：快照 HP>0 但怪物处于休眠状态 → 唤醒
                if (e.hp > 0 && !localMonster.gameObject.activeInHierarchy)
                {
                    localMonster.GetComponent<MonsterCombat>()?.MmoRevive(e.maxHp);
                }

                // 权威 HP 同步
                var combat = localMonster.GetComponent<MonsterCombat>();
                if (combat != null)
                {
                    if (combat.CurrentHealth != e.hp)
                    {
                        combat.CurrentHealth = e.hp;
                        if (e.hp <= 0) combat.Die();
                    }
                }
                // 权威位置同步（通过插值平滑，避免 VSpeed/HSpeed 因位置突变而抖动）
                var newPos = new Vector3(e.posX, e.posY, e.posZ);
                var interp = localMonster.GetComponent<PositionInterpolator>();
                if (interp == null) interp = localMonster.gameObject.AddComponent<PositionInterpolator>();
                interp.RenderDelay = 0.066f;
                interp.SetTarget(newPos);
                var nav = localMonster.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (nav != null)
                {
                    // MMO: 禁止 agent 控制位置/旋转，防止与 PositionInterpolator 冲突造成抖动
                    if (nav.updatePosition) nav.updatePosition = false;
                    if (nav.updateRotation) nav.updateRotation = false;
                    if (nav.hasPath) nav.ResetPath();
                }
                // 权威 AI 状态同步 + MMO 动画驱动
                var sm = localMonster.GetComponent<MonsterStateMachine>();
                var serverState = (MonsterState)e.animState;
                if (sm != null && sm.currentState != serverState && serverState != MonsterState.Death)
                {
                    sm.currentState = serverState;
                    SyncMmoAnimation(localMonster, sm, serverState);
                }
                return; // 本地怪物已存在，不创建 RemoteMonster
            }
            return; // 找不到本地怪物，也不创建 RemoteMonster（服务端才管理怪物实体）
        }

        GameObject go;
        if (!_entities.TryGetValue(e.entityId, out go))
        {
            try
            {
                // 远程玩家：按 modelType 选择角色模型 Prefab
                GameObject prefab = null;
                if (e.entityType == 0)
                {
                    prefab = GetRemotePlayerPrefab(e.modelType);
                }
                if (prefab == null) prefab = e.entityType == 0 ? _remotePlayerPrefab : _remoteMonsterPrefab;
                if (prefab == null) return;
                go = Instantiate(prefab);
                go.transform.position = new Vector3(e.posX, e.posY, e.posZ);
                go.transform.rotation = Quaternion.Euler(0, e.rotY, 0);

                // 远程玩家：剥离游戏逻辑组件，只保留视觉模型 + Animator
                if (e.entityType == 0 && go != _remotePlayerPrefab)
                {
                    StripGameplayComponents(go);
                    if (go.GetComponent<MonsterLocomotionDriver>() == null)
                        go.AddComponent<MonsterLocomotionDriver>();
                }

                _entities[e.entityId] = go;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EntitySync] 创建远程实体{e.entityId} 异常: {ex}");
                return; // 创建失败，跳过本次更新
            }
        }

        var pos = new Vector3(e.posX, e.posY, e.posZ);
        var rot = Quaternion.Euler(0, e.rotY, 0);

        var interpolator = go.GetComponent<PositionInterpolator>();
        if (interpolator)
        {
            interpolator.RenderDelay = 0.03f;
            interpolator.SetTarget(pos);
        }
        else go.transform.position = pos;

        go.transform.rotation = rot;

        var proxy = go.GetComponent<NetworkEntityProxy>();
        if (proxy) proxy.SetHp(e.hp, e.maxHp);

        // 远程玩家攻击动画：atkTrigger 变化时播放攻击
        if (e.entityType == 0 && e.atkTrigger > 0)
        {
            var lastAtk = _remoteAtkCache.GetValueOrDefault(e.entityId);
            if (e.atkTrigger != lastAtk)
            {
                _remoteAtkCache[e.entityId] = e.atkTrigger;
                go.GetComponent<Animator>()?.SetTrigger("Attack");
            }
        }

        // 公会数据同步：guildId 变更时更新本地 GuildManager
        if (e.entityType == 0 && !string.IsNullOrEmpty(e.uid))
        {
            var gm = GuildManager.Instance;
            if (gm != null)
            {
                gm.ApplyRemoteGuildId(e.uid, e.guildId ?? "");
            }
        }

        // 远程玩家技能特效：skillTrigger 变化时实例化技能 VFX
        if (e.entityType == 0 && e.skillTrigger > 0 && !string.IsNullOrEmpty(e.skillId))
        {
            var lastSkill = _remoteSkillCache.GetValueOrDefault(e.entityId);
            if (e.skillTrigger != lastSkill)
            {
                _remoteSkillCache[e.entityId] = e.skillTrigger;
                PlayRemoteSkillVfx(go, e.skillId, new Vector3(e.skillTargetX, e.skillTargetY, e.skillTargetZ));
            }
        }
    }

    /// <summary>
    /// MMO 模式：根据服务端状态变化触发对应的客户端动画。
    /// 仅当状态发生切换时调用，避免每帧重复触发。
    /// </summary>
    void SyncMmoAnimation(MonsterBase monster, MonsterStateMachine sm, MonsterState newState)
    {
        if (!GameModeConfig.IsMmoMode) return;

        var animCtrl = monster.GetComponent<MonsterAnimationController>();
        var loco = monster.GetComponent<MonsterLocomotionDriver>();
        var playerTf = sm.PlayerRef;

        switch (newState)
        {
            case MonsterState.Attack:
                // 服务端每 2s 攻击一次，只在状态首次切换时触发单次攻击动画
                animCtrl?.PlayAttack();
                if (loco != null) loco.FaceTarget = playerTf;
                break;

            case MonsterState.Alert:
                animCtrl?.PlayAlert();
                if (loco != null) loco.FaceTarget = playerTf;
                break;

            case MonsterState.Chase:
                // 追击时锁定朝向玩家
                if (loco != null) loco.FaceTarget = playerTf;
                break;

            case MonsterState.Patrol:
            case MonsterState.Idle:
            case MonsterState.ReturnToSpawn:
                // 非战斗状态取消面向玩家，由移动方向决定朝向
                if (loco != null) loco.FaceTarget = null;
                break;
        }
    }

    /// <summary>移除远程玩家身上的游戏逻辑组件，避免和本地玩家产生输入/动画/物理冲突</summary>
    void StripGameplayComponents(GameObject go)
    {
        // 彻底销毁游戏逻辑组件（enabled=false 不够，方法仍可被 IDamageable 等接口调用）
        Destroy(go.GetComponent<CharacterState>());
        Destroy(go.GetComponent<MoveMent>());
        Destroy(go.GetComponent<CharacterAnimationController>());
        Destroy(go.GetComponent<PlayerFSM.PlayerStateMachine>());
        Destroy(go.GetComponent<LockOnController>());
        Destroy(go.GetComponent<PlayerInteraction>());
        Destroy(go.GetComponent<SkillController>());

        // 物理隔离：远程玩家不应和本地玩家碰撞
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; }

        var col = go.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 禁用音效（避免远程玩家的脚步/攻击音效在本地播放）
        var audioSources = go.GetComponentsInChildren<AudioSource>();
        foreach (var src in audioSources) src.enabled = false;

        // 移除 Player 标签（防止技能通过 FindWithTag("Player") 找到远程玩家）
        if (go.CompareTag("Player")) go.tag = "Untagged";

        // 放入 "RemotePlayer" 层避免物理碰撞
        int remoteLayer = LayerMask.NameToLayer("RemotePlayer");
        if (remoteLayer != -1) go.layer = remoteLayer;
    }

    /// <summary>根据 modelType 查找远程玩家的角色模型 Prefab</summary>
    GameObject GetRemotePlayerPrefab(byte modelType)
    {
        if (_characterSelectData == null || _characterSelectData.data == null) return null;
        var profession = (CharacterProfession)modelType;
        var entry = _characterSelectData.data.FirstOrDefault(x => x.job == profession);
        return entry?.model;
    }

    /// <summary>实体离开视野</summary>
    public void RemoveEntity(uint entityId)
    {
        if (_entities.TryGetValue(entityId, out var go))
        {
            Destroy(go);
            _entities.Remove(entityId);
        }
        _remoteAtkCache.Remove(entityId);
        _remoteSkillCache.Remove(entityId);
    }

    void OnDestroy()
    {
        foreach (var go in _entities.Values) Destroy(go);
        _entities.Clear();
        _remoteAtkCache.Clear();
        _remoteSkillCache.Clear();
    }

    /// <summary>在远程玩家身上实例化技能 VFX（只播放视觉效果，不造成伤害）</summary>
    void PlayRemoteSkillVfx(GameObject remotePlayer, string skillId, Vector3 targetPos)
    {
        var sm = SkillManager.Instance;
        if (sm == null) return;
        var so = sm.GetSkillSo(skillId);
        if (so == null || so.skillPrefab == null) return;

        var go = Instantiate(so.skillPrefab, remotePlayer.transform.position,
            targetPos != remotePlayer.transform.position
                ? Quaternion.LookRotation(targetPos - remotePlayer.transform.position)
                : remotePlayer.transform.rotation);
        var comp = go.GetComponent<Skill>();
        if (comp != null)
        {
            comp.SetFirePoint(remotePlayer.transform);
            // 用最小等级 PlayerSkill 驱动 Execute——只触发 VFX，远程玩家没有本地怪物/目标所以不会造成伤害
            var dummySkill = new PlayerSkill(so, 1);
            comp.Execute(remotePlayer.transform, dummySkill);
        }
        else
        {
            Destroy(go, 6f);
        }
    }

    [Serializable]
    public class PlayerSnapshot
    {
        public uint playerEntityId;
        public List<EntitySnapshot> entities = new();
    }

    [Serializable]
    public class EntitySnapshot
    {
        public uint entityId;
        public byte entityType;
        public string uid;
        public byte modelType;
        public byte atkTrigger;
        public byte skillTrigger;
        public string skillId;
        public float skillTargetX, skillTargetY, skillTargetZ;
        public string guildId;
        public float posX, posY, posZ;
        public float rotY;
        public int hp, maxHp;
        public byte animState;
    }
}
