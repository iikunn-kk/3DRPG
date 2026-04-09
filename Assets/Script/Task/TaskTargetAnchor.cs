using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可放置在动态目标（NPC、怪物刷新点、交互物体等）上，供任务追踪系统查询世界坐标。
/// 通过 (ObjectiveType, targetId) 索引。
/// </summary>
[DisallowMultipleComponent]
public class TaskTargetAnchor : MonoBehaviour
{
    private static readonly Dictionary<(ObjectiveType,int), List<TaskTargetAnchor>> _anchors = new();

    [Header("绑定的任务目标信息")] public ObjectiveType objectiveType;
    public int targetId;

    private void OnEnable()
    {
        var key = (objectiveType, targetId);
        if (!_anchors.TryGetValue(key, out var list))
        {
            list = new List<TaskTargetAnchor>();
            _anchors[key] = list;
        }
        if (!list.Contains(this)) list.Add(this);
    }

    private void OnDisable()
    {
        var key = (objectiveType, targetId);
        if (_anchors.TryGetValue(key, out var list))
        {
            list.Remove(this);
            if (list.Count == 0) _anchors.Remove(key);
        }
    }

    public static List<TaskTargetAnchor> GetAnchors(ObjectiveType type, int targetId)
    {
        _anchors.TryGetValue((type, targetId), out var list);
        return list;
    }
}

