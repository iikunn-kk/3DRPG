using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 从 WorldServer 接收 AOI 快照，创建/更新/销毁远程实体。
/// 挂载到场景中的空 GameObject 上（建议和 NetworkManager 同级）。
/// </summary>
public class EntitySyncManager : MonoBehaviour
{
    public static EntitySyncManager Instance { get; private set; }
    void Awake() { Instance = this; }
    [Header("远程实体 Prefab")]
    [SerializeField] private GameObject _remotePlayerPrefab;
    [SerializeField] private GameObject _remoteMonsterPrefab;

    [Header("本地玩家引用（用于过滤自身）")]
    [SerializeField] private Transform _localPlayer;

    private readonly Dictionary<uint, GameObject> _entities = new();
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
                // 过滤自身：按 entityId 或 uid 匹配
                if (e.entityId == _localPlayerEntityId) continue;
                if (e.entityType == 0 && e.uid == NetworkManager.Instance.PlayerUid) continue;

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
        // 怪物：优先匹配本地 MonsterBase（已由 MonsterSpawner 生成），直接更新 HP
        if (e.entityType == 1)
        {
            var localMonster = MonsterBase.FindByNetworkId(e.entityId);
            if (localMonster != null)
            {
                var combat = localMonster.GetComponent<MonsterCombat>();
                if (combat != null)
                {
                    if (combat.CurrentHealth != e.hp)
                    {
                        combat.CurrentHealth = e.hp;
                        if (e.hp <= 0) combat.Die();
                    }
                }
                // 该实体无本地 MonsterCombat，无需同步
                return; // 不创建 RemoteMonster Cube
            }
        }

        GameObject go;
        if (!_entities.TryGetValue(e.entityId, out go))
        {
            var prefab = e.entityType == 0 ? _remotePlayerPrefab : _remoteMonsterPrefab;
            if (prefab == null) return;
            go = Instantiate(prefab);
            _entities[e.entityId] = go;
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
    }

    /// <summary>实体离开视野</summary>
    public void RemoveEntity(uint entityId)
    {
        if (_entities.TryGetValue(entityId, out var go))
        {
            Destroy(go);
            _entities.Remove(entityId);
        }
    }

    void OnDestroy()
    {
        foreach (var go in _entities.Values) Destroy(go);
        _entities.Clear();
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
        public float posX, posY, posZ;
        public float rotY;
        public int hp, maxHp;
        public byte animState;
    }
}
