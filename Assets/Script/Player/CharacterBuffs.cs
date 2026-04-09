using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

/// <summary>
/// 管理挂在角色上的临时/持续 Buff（数值型）与 Buff 可视化（仅保留一个可见特效）。
/// 提供：ApplyBuff/RemoveBuffBySource/RemoveBuffBySourceAndType 与 RegisterBuffVisual/UnregisterBuffVisual。
/// </summary>
[DisallowMultipleComponent]
public class CharacterBuffs : MonoBehaviour
{
    public enum BuffType
    {
        AttackFlat,
        CritPercent,
        RegenPerSecond
    }

    // === 对外快照结构 ===
    [Serializable]
    public struct BuffSnapshot
    {
        public string Source;
        public BuffType Type;
        public float Value;
        public float RemainingDuration; // 剩余持续秒
    }

    [Serializable]
    public struct VisualSnapshot
    {
        public string Source;
        public GameObject Prefab; // 仅引用（注意：需跨场景的 Prefab 应为可访问资源，通常在Addressables或Resources中）
    }

    private class ActiveBuff
    {
        public string Source;
        public BuffType Type;
        public float Value;
        public float Duration; // 初始时长
        public float ExpireAt; // Time.time + Duration
        public Coroutine ExpireCoroutine;
    }

    private readonly List<ActiveBuff> _activeBuffs = new List<ActiveBuff>();

    // Regen
    private float _totalRegenPerSecond;
    private Coroutine _regenCoroutine;

    [Header("Buff VFX（只显示最新添加的1个）")]
    [Tooltip("Buff特效的父节点（为空则挂到玩家对象上）")]
    [SerializeField] private Transform vfxAnchor;

    private class VisualEntry
    {
        public string Source;
        public GameObject Prefab;
        public GameObject Instance;
        public int Order; // 越大越新
    }
    private readonly List<VisualEntry> _visuals = new();
    private int _visualOrder;

    private CharacterState _cs;

    private void Awake()
    {
        _cs = GetComponent<CharacterState>();
        if (_cs == null)
        {
            Debug.LogError("CharacterBuffs 需要与 CharacterState 同挂在一个 GameObject 上。");
            enabled = false;
        }
        // 启动时做一次孤儿可视化清理，避免残留
        CleanupOrphanVisuals();
    }

    // ========== 数值 Buff ==========
    /// <summary>
    /// 应用一个 Buff，会替换相同 source+type 的旧 Buff（若存在）。
    /// </summary>
    public void ApplyBuff(string source, BuffType type, float value, float duration)
    {
        if (string.IsNullOrEmpty(source) || duration <= 0f) return;
        RemoveBuffBySourceAndType(source, type);

        var b = new ActiveBuff { Source = source, Type = type, Value = value, Duration = duration, ExpireAt = Time.time + duration };
        b.ExpireCoroutine = StartCoroutine(BuffDurationRoutine(b));
        _activeBuffs.Add(b);

        RecalculateAndApply();
        // 每次数值变化后都尝试清理孤儿可视化
        CleanupOrphanVisuals();
    }

    private IEnumerator BuffDurationRoutine(ActiveBuff b)
    {
        float remaining = Mathf.Max(0f, b.ExpireAt - Time.time);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
        RemoveBuffBySourceAndType(b.Source, b.Type);
    }

    public void RemoveBuffBySource(string source)
    {
        if (string.IsNullOrEmpty(source)) return;
        var toRemove = _activeBuffs.Where(x => x.Source == source).ToList();
        foreach (var b in toRemove)
        {
            if (b.ExpireCoroutine != null) StopCoroutine(b.ExpireCoroutine);
            _activeBuffs.Remove(b);
        }
        RecalculateAndApply();
        // 若该来源已无任何数值型Buff，清理其可视化
        if (!_activeBuffs.Any(x => x.Source == source))
        {
            UnregisterBuffVisual(source);
        }
        // 额外清理孤儿可视化
        CleanupOrphanVisuals();
    }

    public void RemoveBuffBySourceAndType(string source, BuffType type)
    {
        if (string.IsNullOrEmpty(source)) return;
        var b = _activeBuffs.FirstOrDefault(x => x.Source == source && x.Type == type);
        if (b != null)
        {
            if (b.ExpireCoroutine != null) StopCoroutine(b.ExpireCoroutine);
            _activeBuffs.Remove(b);
            RecalculateAndApply();
            if (!_activeBuffs.Any(x => x.Source == source))
            {
                UnregisterBuffVisual(source);
            }
            // 额外清理孤儿可视化
            CleanupOrphanVisuals();
        }
    }

    public void RecalculateAndApply()
    {
        if (_cs == null) return;

        int attackFlat = 0;
        float critPercent = 0f;
        float regenPerSecond = 0f;
        foreach (var b in _activeBuffs)
        {
            switch (b.Type)
            {
                case BuffType.AttackFlat:
                    attackFlat += Mathf.RoundToInt(b.Value);
                    break;
                case BuffType.CritPercent:
                    critPercent += b.Value;
                    break;
                case BuffType.RegenPerSecond:
                    regenPerSecond += b.Value;
                    break;
            }
        }

        _cs.ApplyBuffTotals(attackFlat, critPercent);

        if (Mathf.Approximately(regenPerSecond, 0f))
        {
            if (_regenCoroutine != null)
            {
                StopCoroutine(_regenCoroutine);
                _regenCoroutine = null;
            }
        }
        else
        {
            _totalRegenPerSecond = regenPerSecond;
            if (_regenCoroutine == null)
            {
                _regenCoroutine = StartCoroutine(RegenRoutine());
            }
        }
    }

    private IEnumerator RegenRoutine()
    {
        float acc = 0f;
        while (true)
        {
            float dt = Time.deltaTime;
            acc += _totalRegenPerSecond * dt;
            if (acc >= 1f)
            {
                int heal = Mathf.FloorToInt(acc);
                acc -= heal;
                _cs?.Heal(heal);
            }
            yield return null;
        }
    }

    // ========== Buff 快照 ==========
    public List<BuffSnapshot> GetBuffSnapshots()
    {
        var list = new List<BuffSnapshot>(_activeBuffs.Count);
        foreach (var b in _activeBuffs)
        {
            float remaining = Mathf.Max(0f, b.ExpireAt - Time.time);
            if (remaining <= 0f) continue; // 即将过期则忽略
            list.Add(new BuffSnapshot
            {
                Source = b.Source,
                Type = b.Type,
                Value = b.Value,
                RemainingDuration = remaining
            });
        }
        return list;
    }

    public List<VisualSnapshot> GetVisualSnapshots()
    {
        var list = new List<VisualSnapshot>(_visuals.Count);
        foreach (var v in _visuals)
        {
            if (v == null || v.Prefab == null) continue;
            list.Add(new VisualSnapshot { Source = v.Source, Prefab = v.Prefab });
        }
        return list;
    }

    public void ApplyBuffSnapshots(IEnumerable<BuffSnapshot> snapshots)
    {
        if (snapshots == null) return;
        foreach (var s in snapshots)
        {
            if (string.IsNullOrEmpty(s.Source) || s.RemainingDuration <= 0f) continue;
            ApplyBuff(s.Source, s.Type, s.Value, s.RemainingDuration);
        }
        // 快照应用后，清理可能存在的孤儿可视化
        CleanupOrphanVisuals();
    }

    public void RestoreVisualSnapshots(IEnumerable<VisualSnapshot> visualSnapshots)
    {
        if (visualSnapshots == null) return;
        foreach (var v in visualSnapshots)
        {
            if (v.Prefab == null || string.IsNullOrEmpty(v.Source)) continue;
            // 只有当存在同源的数值型Buff时才恢复其可视化，避免无主视觉残留
            if (HasBuffSource(v.Source))
            {
                RegisterBuffVisual(v.Source, v.Prefab);
            }
        }
        // 恢复后再对齐一遍，确保只展示最新
        RefreshVisualDisplay();
        CleanupOrphanVisuals();
    }

    // ========== Buff VFX ==========
    public void RegisterBuffVisual(string source, GameObject vfxPrefab)
    {
        if (string.IsNullOrEmpty(source) || vfxPrefab == null) return;
        // 若同源已有，先移除旧的，避免残留
        UnregisterBuffVisual(source);

        var anchor = vfxAnchor != null ? vfxAnchor : this.transform;
        var inst = Instantiate(vfxPrefab, anchor.position, anchor.rotation, anchor);
        inst.SetActive(false); // 先不显示，稍后统一激活最新

        _visuals.Add(new VisualEntry
        {
            Source = source,
            Prefab = vfxPrefab,
            Instance = inst,
            Order = ++_visualOrder
        });

        RefreshVisualDisplay();
    }

    public void UnregisterBuffVisual(string source)
    {
        if (string.IsNullOrEmpty(source)) return;
        for (int i = _visuals.Count - 1; i >= 0; i--)
        {
            var v = _visuals[i];
            if (v.Source == source)
            {
                if (v.Instance != null)
                {
                    Destroy(v.Instance);
                }
                _visuals.RemoveAt(i);
            }
        }
        RefreshVisualDisplay();
    }

    private void RefreshVisualDisplay()
    {
        // 全部先关
        foreach (var v in _visuals)
        {
            if (v.Instance != null && v.Instance.activeSelf)
                v.Instance.SetActive(false);
        }
        // 找到最新添加的一个开启
        var toShow = _visuals.OrderBy(v => v.Order).LastOrDefault();
        if (toShow != null && toShow.Instance != null)
        {
            toShow.Instance.SetActive(true);
        }
    }

    // 额外：清理没有对应数值型Buff来源的可视化（跨场景或异常终止时的兜底）
    private void CleanupOrphanVisuals()
    {
        for (int i = _visuals.Count - 1; i >= 0; i--)
        {
            var v = _visuals[i];
            if (v == null) { _visuals.RemoveAt(i); continue; }
            if (!HasBuffSource(v.Source))
            {
                if (v.Instance != null) Destroy(v.Instance);
                _visuals.RemoveAt(i);
            }
        }
        RefreshVisualDisplay();
    }

    // Expose for other systems if needed
    public int GetAttackFlatTotal() => _activeBuffs.Where(b => b.Type == BuffType.AttackFlat).Sum(b => Mathf.RoundToInt(b.Value));
    public float GetCritPercentTotal() => _activeBuffs.Where(b => b.Type == BuffType.CritPercent).Sum(b => b.Value);
    public float GetRegenPerSecondTotal() => _activeBuffs.Where(b => b.Type == BuffType.RegenPerSecond).Sum(b => b.Value);

    // 检查是否存在某来源的任意数值Buff
    public bool HasBuffSource(string source)
    {
        if (string.IsNullOrEmpty(source)) return false;
        return _activeBuffs.Any(b => b.Source == source);
    }
}
