// 文件名: DragAndDropPanel.cs
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class DragAndDropPanel : Singleton<DragAndDropPanel>
{
    private const string Path = "PlayingPrefabs";
    [Header("拖拽视觉预制体")]
    [SerializeField] private GameObject dragVisualPrefab;

    // 新增: 用于判定"仍在背包/装备UI范围内"的根RectTransform（不算作拖出界外）
    [Header("判定仍在UI内的区域(未命中Slot也不丢弃)")]
    [SerializeField] private List<RectTransform> protectedUIAreas = new();

    private DragVisualPrefab _dragVisualInstance;
    private InventorySlot _sourceSlot; // 记录拖拽的源头（背包格子）
    private EquipmentSlotUI _sourceEquipmentSlot; // 记录拖拽的装备槽源头
    [SerializeField] private Canvas canvas;

    protected override void Awake()
    {
        base.Awake();
        gameObject.SetActive(false); // 默认隐藏
    }

    private void OnDisable()
    {
        // 若在拖拽中被禁用，确保恢复源格子/装备槽的视觉与清理视觉
        if (_dragVisualInstance != null)
        {
            Destroy(_dragVisualInstance.gameObject);
            _dragVisualInstance = null;
        }

        // 恢复源格子与源装备槽的可见性（若存在）
        if (_sourceSlot != null)
        {
            _sourceSlot.SetVisible(true);
            _sourceSlot = null;
        }
        if (_sourceEquipmentSlot != null)
        {
            _sourceEquipmentSlot.SetVisible(true);
            _sourceEquipmentSlot = null;
        }
    }

    /// <summary>
    /// 供外部(例如 InventoryPanel)在初始化时注册一个受保护区域(未命中格子也视为"仍在UI中")。
    /// 如果已经在 Inspector 里拖拽引用，可不调用。
    /// </summary>
    public void RegisterProtectedArea(RectTransform area)
    {
        if (area != null && !protectedUIAreas.Contains(area))
            protectedUIAreas.Add(area);
    }

    /// <summary>
    /// 由任何 Slot 的 OnBeginDrag 调用（背包格子拖拽）
    /// </summary>
    public void StartDrag(InventorySlot sourceSlot)
    {
        if (sourceSlot == null || sourceSlot.Item == null) return; // 空的格子不能拖拽

        _sourceSlot = sourceSlot;
        _sourceEquipmentSlot = null; // 确保只有一个源

        // 记录其根区域(若尚未添加)供丢弃判定使用
        TryAutoRegisterArea(sourceSlot.transform as RectTransform);

        Canvas parentCanvas = EnsureCanvasFromSource(_sourceSlot.transform);

        // 传入整个 InventoryItem 以便使用品质颜色
        CreateDragVisual(parentCanvas, _sourceSlot.Item);

        // 将源格子的视觉隐藏，使其看起来被拖拽出来
        _sourceSlot?.SetVisible(false);

        gameObject.SetActive(true);
    }

    /// <summary>
    /// 由 EquipmentSlotUI 的 OnBeginDrag 调用（装备槽拖拽）
    /// </summary>
    public void StartDrag(EquipmentSlotUI sourceEquipment)
    {
        if (sourceEquipment == null || sourceEquipment.Item == null) return;

        _sourceEquipmentSlot = sourceEquipment;
        _sourceSlot = null; // 确保只有一个源

        TryAutoRegisterArea(sourceEquipment.transform as RectTransform);

        Canvas parentCanvas = EnsureCanvasFromSource(_sourceEquipmentSlot.transform);

        CreateDragVisual(parentCanvas, _sourceEquipmentSlot.Item);

        _sourceEquipmentSlot?.SetVisible(false);

        gameObject.SetActive(true);
    }

    // 通用: 根据源transform寻找Canvas（优先使用已序列化的canvas）
    private Canvas EnsureCanvasFromSource(Transform source)
    {
        Canvas parentCanvas = canvas;
        if (parentCanvas == null && source != null)
        {
            parentCanvas = source.GetComponentInParent<Canvas>();
            canvas = parentCanvas; // 缓存
        }
        return parentCanvas;
    }

    // 修改: 接收 InventoryItem 以便设置品质颜色
    private void CreateDragVisual(Canvas parentCanvas, InventoryItem item)
    {
        if (dragVisualPrefab != null && parentCanvas != null && item != null)
        {
            var dragVisualGo = Instantiate(dragVisualPrefab, parentCanvas.transform, false);
            dragVisualGo.transform.SetAsLastSibling();
            var cg = dragVisualGo.GetComponent<CanvasGroup>();
            if (cg == null) cg = dragVisualGo.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;

            _dragVisualInstance = dragVisualGo.GetComponent<DragVisualPrefab>();
            if (_dragVisualInstance != null)
            {
                _dragVisualInstance.Initialize(parentCanvas);
                var itemData = GameDataConfig.Instance.ItemDataSo.GetItemDataById(item.itemId);
                if (itemData != null)
                {
                    _dragVisualInstance.SetSprite(itemData.itemSprite);
                }
                // 设置背景颜色为品质色
                _dragVisualInstance.SetBackgroundColor(ItemQualityUtility.GetQualityColor(item.quantity));
            }
        }
    }

    // 自动尝试向上寻找一个RectTransform作为受保护区域(例如背包格子父节点 / 背包主面板) - 防御性：避免无限向上
    private void TryAutoRegisterArea(RectTransform child)
    {
        if (child == null) return;
        var current = child.parent;
        int depth = 0;
        while (current != null && depth < 10) // 限制层级防止死循环
        {
            var rt = current as RectTransform;
            if (rt != null && rt.gameObject.name.Contains("Inventory")) // 简单启发式：名称含 Inventory
            {
                RegisterProtectedArea(rt);
                break;
            }
            current = current.parent;
            depth++;
        }
    }

    // 已移除：技能槽（SkillQuickMod）拖拽功能

    /// <summary>
    /// 由任何格子的 OnEndDrag 调用（背包/装备拖拽结束）
    /// </summary>
    public void EndDrag(PointerEventData eventData)
    {
        bool movedSuccessfully = false; // 标记是否完成了合法移动
        bool droppedOutside = false;    // 标记是否真正丢弃/卸下

        // 记录原始位置用于判断是否实际发生移动
        ItemLocation originalLocation = ItemLocation.Inventory;
        int originalSlotIndex = -1;
        if (_sourceSlot != null && _sourceSlot.Item != null)
        {
            originalLocation = _sourceSlot.Item.location;
            originalSlotIndex = _sourceSlot.Item.slotIndex;
        }
        else if (_sourceEquipmentSlot != null && _sourceEquipmentSlot.Item != null)
        {
            originalLocation = _sourceEquipmentSlot.Item.location;
            originalSlotIndex = _sourceEquipmentSlot.Item.slotIndex;
        }

        // 1. 清理拖拽视觉元素
        if (_dragVisualInstance != null)
        {
            Destroy(_dragVisualInstance.gameObject);
            _dragVisualInstance = null;
        }

        // 2. 检查鼠标下方是否存在合法的放置目标
        IDropTarget target = null;
        if (eventData != null && eventData.pointerEnter != null)
        {
            target = eventData.pointerEnter.GetComponentInParent<IDropTarget>();
        }

        // 3. 如果找到了合法目标并且源存在，则发起移动请求
        if (target != null)
        {
            // 如果目标与原位置完全相同，视为未移动（取消拖拽）
            if (target.Location == originalLocation && target.SlotIndex == originalSlotIndex)
            {
                movedSuccessfully = false; // 明确标记
            }
            else if (_sourceSlot != null)
            {
                InventoryManager.Instance.MoveItem(
                    _sourceSlot.Item.instanceId,
                    target.Location,
                    target.SlotIndex
                );
                movedSuccessfully = true;
            }
            else if (_sourceEquipmentSlot != null)
            {
                InventoryManager.Instance.MoveItem(
                    _sourceEquipmentSlot.Item.instanceId,
                    target.Location,
                    target.SlotIndex
                );
                movedSuccessfully = true;
            }
        }
        else
        {
            // 没有命中具体Slot，判定是否仍在受保护区域
            bool insideProtected = IsPointerInsideProtectedAreas(eventData);
            if (!insideProtected)
            {
                // 真正拖出UI外：执行丢弃或卸下逻辑
                HandleDropOutside();
                droppedOutside = true;
            }
            // insideProtected 情况下视为取消拖拽
        }

        // 4. 仅在未移动成功、且未真正丢弃时，恢复源格子/装备槽的可见性
        if (!movedSuccessfully && !droppedOutside)
        {
            if (_sourceSlot != null)
            {
                _sourceSlot.SetVisible(true);
            }
            if (_sourceEquipmentSlot != null)
            {
                _sourceEquipmentSlot.SetVisible(true);
            }
        }

        // 5. 清理内部状态并隐藏面板
        _sourceSlot = null;
        _sourceEquipmentSlot = null;
        gameObject.SetActive(false);
    }

    private bool IsPointerInsideProtectedAreas(PointerEventData eventData)
    {
        if (eventData == null) return false;
        if (protectedUIAreas == null || protectedUIAreas.Count == 0) return false; // 未配置则认为不保护

        Vector2 screenPos = eventData.position;
        foreach (var area in protectedUIAreas)
        {
            if (area == null) continue;
            var parentCanvasLocal = area.GetComponentInParent<Canvas>();
            if (parentCanvasLocal == null) continue;
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                area,
                screenPos,
                parentCanvasLocal.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvasLocal.worldCamera,
                out localPoint);
            if (area.rect.Contains(localPoint))
            {
                return true; // 一旦命中任一受保护区域，则视为仍在UI里
            }
        }
        return false;
    }

    private void HandleDropOutside()
    {
        // 从背包格子拖出：丢弃
        if (_sourceSlot != null && _sourceSlot.Item != null)
        {
            var item = _sourceSlot.Item;
            var list = new System.Collections.Generic.List<Vector2Int>
            {
                new (item.itemId, item.count)
            };
            InventoryManager.Instance.RemoveItem(item.instanceId);

            var player = CharacterService.Instance.CurrentPlayerCharacter();
            Vector3 spawnPos = Vector3.zero;
            if (player != null && player.transform != null)
            {
                spawnPos = player.transform.position + (player.transform.forward * 1.2f);
            }

            GameObject prefab = AddressableCache.Load<GameObject>("DroppedItems");
            if (prefab != null)
            {
                var go = Object.Instantiate(prefab, spawnPos, Quaternion.identity);
                var dropped = go.GetComponent<DroppedItems>();
                if (dropped != null)
                {
                    dropped.Init(list);
                }
            }
            else
            {
                var go = new GameObject("DroppedItems");
                go.transform.position = spawnPos;
                var dropped = go.AddComponent<DroppedItems>();
                dropped.Init(list);
            }
        }

        // 从装备槽拖出：尝试卸下
        if (_sourceEquipmentSlot != null && _sourceEquipmentSlot.Item != null)
        {
            var equipItem = _sourceEquipmentSlot.Item;
            int emptySlot = InventoryManager.Instance.FindFirstEmptyInventorySlot();
            if (emptySlot != -1)
            {
                InventoryManager.Instance.UnequipItem(equipItem.instanceId, emptySlot);
            }
            else
            {
                Debug.Log("背包已满，无法卸下装备！");
            }
        }
    }
}

