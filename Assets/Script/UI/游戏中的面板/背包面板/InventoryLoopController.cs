using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包 LoopScrollRect 控制器 —— 对象池 + 数据绑定 + 1000 槽位虚拟滚动
/// </summary>
public class InventoryLoopController : MonoBehaviour, LoopScrollPrefabSource, LoopScrollMultiDataSource
{
    private LoopScrollRectMulti _loopScroll;
    private GameObject _slotPrefab;
    private readonly Stack<Transform> _pool = new();

    private Action<InventoryItem> _onHoverEnter;
    private Action _onHoverExit;

    // 刷新前构建的槽位→物品映射，O(1) 查找
    private Dictionary<int, InventoryItem> _slotMap = new();

    public void Init(LoopScrollRectMulti scroll, GameObject prefab,
        Action<InventoryItem> onHoverEnter, Action onHoverExit)
    {
        _loopScroll = scroll;
        _slotPrefab = prefab;
        _onHoverEnter = onHoverEnter;
        _onHoverExit = onHoverExit;
        _loopScroll.prefabSource = this;
        _loopScroll.dataSource = this;
    }

    #region LoopScrollPrefabSource

    public GameObject GetObject(int index)
    {
        Transform tr = null;
        if (_pool.Count > 0)
        {
            tr = _pool.Pop();
            if (tr != null) tr.gameObject.SetActive(true);
        }
        if (tr == null)
            tr = Instantiate(_slotPrefab).transform;

        return tr.gameObject;
    }

    public void ReturnObject(Transform trans)
    {
        if (trans == null) return;
        trans.gameObject.SetActive(false);
        trans.SetParent(transform, false);
        _pool.Push(trans);
    }

    #endregion

    #region LoopScrollMultiDataSource

    public void ProvideData(Transform cellTransform, int idx)
    {
        if (cellTransform == null) return;
        var slot = cellTransform.GetComponent<InventorySlot>();
        if (slot == null) return;

        // 必须先设置槽位索引，空格子也要正确的 SlotIndex
        slot.SetSlotIndex(idx);

        if (_slotMap.TryGetValue(idx, out var item))
            slot.ScrollCellIndexWithCallbacks(idx, item, _onHoverEnter, _onHoverExit);
        else
            slot.ClearSlot();
    }

    #endregion

    public void RefreshList()
    {
        _slotMap.Clear();
        foreach (var item in InventoryManager.Instance.GetInventoryItems())
            _slotMap[item.slotIndex] = item;

        int total = InventoryManager.Instance.MaxInventorySlots;
        bool totalCountChanged = _loopScroll.totalCount != total;
        _loopScroll.totalCount = total;

        if (totalCountChanged)
        {
            // 首次初始化 totalCount 变化，强制重构布局后 Refill
            Canvas.ForceUpdateCanvases();
            _loopScroll.RefillCells(0, 0.0f);
        }
        else
        {
            // 仅刷新数据，保持滚动位置
            _loopScroll.RefreshCells();
        }
    }
}
