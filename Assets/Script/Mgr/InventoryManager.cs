using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 背包管理器 - 负责管理玩家所有物品数据，并提供统一的接口进行操作。
/// </summary>
public class InventoryManager : Singleton<InventoryManager>
{
    #region 事件 Events

    /// <summary>当一件物品被装备时触发。</summary>
    public static event Action<InventoryItem, EquipmentData> OnItemEquipped;

    /// <summary>当一件物品被卸下时触发。</summary>
    public static event Action<InventoryItem, EquipmentData> OnItemUnequipped;

    /// <summary>当一个消耗品被使用时触发。</summary>
    public static event Action<ConsumablesData> OnItemConsumed;

    /// <summary>当背包数据发生任何变化时触发，通知UI刷新。</summary>
    public static event Action OnInventoryUpdated;

    #endregion

    #region 字段与属性 Fields & Properties

    [Header("Settings")]
    [SerializeField] private int maxInventorySlots = 72;
    public int MaxInventorySlots => maxInventorySlots;
    [SerializeField] private int quickSlotCount = 10;
    public int QuickSlotCount => quickSlotCount;


    private PlayerInventoryData _playerInventory;
    public List<InventoryItem> AllItems => _playerInventory?.allItems;
    private string _characterId;

    // 新增: 标记背包是否已完成一次加载（成功或失败兜底后也算完成）
    private bool _isLoaded;
    public bool IsLoaded => _isLoaded;

    // 数据库并发保存相关
    private bool _isSaving;
    private bool _savePending;
    private PlayerInventoryData _latestInventoryDataToSave;


    #endregion

    #region Unity生命周期 & 初始化

    /// <summary>
    /// 初始化背包管理器。
    /// </summary>
    public void Initialize(string characterId)
    {
        this._characterId = characterId;
        _ = LoadInventoryAsync();
    }

    #endregion

    #region 数据加载与保存 (Data Persistence)

    private async Task LoadInventoryAsync()
    {
        if (string.IsNullOrEmpty(_characterId))
        {
            Debug.LogWarning("角色ID为空，无法加载背包数据");
            _isLoaded = true; // 防止等待逻辑卡死
            OnInventoryUpdated?.Invoke();
            return;
        }

        try
        {
            var inventoryData = await MongoDBManager.Instance.GetPlayerInventoryDataAsync(_characterId);
            if (inventoryData != null)
            {
                // 如果在加载期间本地已经临时生成了物品（比如用户提前点了随机生成按钮），需要合并
                if (_playerInventory != null && _playerInventory.allItems != null && _playerInventory.allItems.Count > 0 && inventoryData.allItems != null)
                {
                    // 只合并那些数据库中还没有的物品（根据 instanceId 判重），避免重复
                    int added = 0;
                    foreach (var tempItem in _playerInventory.allItems)
                    {
                        if (tempItem == null) continue;
                        bool existsInDb = inventoryData.allItems.Any(x => x.instanceId == tempItem.instanceId);
                        if (existsInDb) continue;
                        //var cloned = tempItem.DeepClone();
                        var cloned = new InventoryItem()
                        {
                            instanceId = Guid.NewGuid().ToString(),
                            itemId = tempItem.itemId,
                            location = tempItem.location,
                            slotIndex = tempItem.slotIndex,
                            count = tempItem.count,
                            quantity = tempItem.quantity,
                            generatedProperties = tempItem.generatedProperties != null ? tempItem.generatedProperties.Select(p => p.DeepClone()).ToList() : new List<EquipmentProperty>()
                        };
                        // 保证 instanceId 不冲突
                        //cloned.instanceId = Guid.NewGuid().ToString();
                        inventoryData.allItems.Add(cloned);
                        added++;
                    }
                    if (added > 0)
                    {
                        Debug.Log($"合并了临时背包中的 {added} 个新物品到数据库数据中。");
                        // 立即保存一次，避免丢失
                        _latestInventoryDataToSave = inventoryData;
                        await ProcessSave();
                    }
                }
                _playerInventory = inventoryData;
                Debug.Log($"成功加载玩家 {_characterId} 的背包数据");
            }
            else
            {
                Debug.Log($"为玩家 {_characterId} 创建新的背包数据。");
                var newInventory = await MongoDBManager.Instance.CreatePlayerInventoryDataAsync(_characterId);
                if (newInventory != null)
                {
                    // 如果本地已经有临时物品（AddItem 早于创建完成），也要合并进去
                    if (_playerInventory != null && _playerInventory.allItems != null && _playerInventory.allItems.Count > 0)
                    {
                        newInventory.allItems ??= new List<InventoryItem>();
                        int added = 0;
                        foreach (var tempItem in _playerInventory.allItems)
                        {
                            if (tempItem == null) continue;
                            bool existsInNew = newInventory.allItems.Any(x => x.instanceId == tempItem.instanceId);
                            if (existsInNew) continue;
                            //var cloned = tempItem.DeepClone();
                            var cloned = new InventoryItem()
                            {
                                instanceId = Guid.NewGuid().ToString(),
                                itemId = tempItem.itemId,
                                location = tempItem.location,
                                slotIndex = tempItem.slotIndex,
                                count = tempItem.count,
                                quantity = tempItem.quantity,
                                generatedProperties = tempItem.generatedProperties != null ? tempItem.generatedProperties.Select(p => p.DeepClone()).ToList() : new List<EquipmentProperty>()
                            };
                            //cloned.instanceId = Guid.NewGuid().ToString();
                            newInventory.allItems.Add(cloned);
                            added++;
                        }
                        if (added > 0)
                        {
                            Debug.Log($"首次创建库存时合并了临时的 {added} 个物品。");
                            _latestInventoryDataToSave = newInventory;
                            await ProcessSave();
                        }
                    }
                    _playerInventory = newInventory; // <-- 关键修复：首次创建时赋值（已合并临时物品）
                }
                else
                {
                    _playerInventory = new PlayerInventoryData(_characterId); // 兜底
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"加载背包数据失败: {ex.Message}");
            if (_playerInventory == null)
            {
                _playerInventory = new PlayerInventoryData(_characterId);
            }
        }
        // 标记加载流程结束（无论成功失败，都允许后续角色尝试初始化已装备属性）
        _isLoaded = true;
        OnInventoryUpdated?.Invoke();
    }

    /// <summary>
    /// 异步保存当前背包状态到数据库。
    /// </summary>
    public async void SaveInventoryAsync()
    {
        if (_playerInventory == null || string.IsNullOrEmpty(_playerInventory.characterId))
        {
            Debug.LogWarning("背包数据无效，无法保存");
            return;
        }

        _latestInventoryDataToSave = _playerInventory;

        if (!_isSaving)
        {
            await ProcessSave();
        }
        else
        {
            _savePending = true;
        }
    }

    private async Task ProcessSave()
    {
        _isSaving = true;
        try
        {
            if (_latestInventoryDataToSave != null)
            {
                bool success = await MongoDBManager.Instance.SavePlayerInventoryDataAsync(_latestInventoryDataToSave);
                if (!success) Debug.LogError("保存背包数据失败");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"保存背包数据时发生异常: {ex.Message}");
        }
        finally
        {
            _isSaving = false;
            if (_savePending)
            {
                _savePending = false;
                await ProcessSave();
            }
        }
    }

    #endregion

    #region 物品核心操作 (增/删/用)

    // 新增: 判断是否拥有完成任务所需的物品数量
    public bool HasItemsForCosts(List<TaskConsumeCost> costs)
    {
        if (costs == null || costs.Count == 0) return true;
        if (AllItems == null) return false;
        foreach (var cost in costs)
        {
            if (cost == null || cost.amount <= 0) continue;
            int total = 0;
            foreach (var it in AllItems)
            {
                if (it == null) continue;
                if (it.itemId == cost.itemId)
                {
                    // 使用 count (堆叠数量), 若为装备或非堆叠物品 count 可能为 1 或 0, 兜底按 1 计
                    int c = it.count > 0 ? it.count : 1;
                    total += c;
                    if (total >= cost.amount) break;
                }
            }
            if (total < cost.amount) return false;
        }
        return true;
    }

    // 新增: 扣除完成任务所需的物品, 仅在全部满足时一次性扣除; 若不足返回 false 不做修改
    public bool ConsumeItemsForCosts(List<TaskConsumeCost> costs)
    {
        if (costs == null || costs.Count == 0) return true;
        if (!HasItemsForCosts(costs)) return false;
        bool changed = false;
        foreach (var cost in costs)
        {
            if (cost == null || cost.amount <= 0) continue;
            int remain = cost.amount;
            // 优先扣除堆叠多的, 简单策略: 按当前列表顺序
            for (int i = AllItems.Count - 1; i >= 0 && remain > 0; i--)
            {
                var it = AllItems[i];
                if (it == null || it.itemId != cost.itemId) continue;
                int available = it.count > 0 ? it.count : 1;
                if (available <= remain)
                {
                    remain -= available;
                    AllItems.RemoveAt(i);
                }
                else
                {
                    it.count = available - remain;
                    remain = 0;
                }
                changed = true;
            }
        }
        if (changed)
        {
            SaveInventoryAsync();
            OnInventoryUpdated?.Invoke();
        }
        return true;
    }

    /// <summary>
    /// 向背包添加物品。
    /// </summary>
    /// <returns>是否添加成功</returns>
    public bool AddItem(int itemId, int count = 1)
    {
        if (count <= 0) return false;

        // 防御：如果背包尚未加载完成，直接初始化一个本地背包，避免首次点击产生空引用
        if (_playerInventory == null)
        {
            Debug.LogWarning("背包数据尚未加载完成，临时创建本地背包数据以接收物品。");
            // 使用空字符串占位，这样 SaveInventoryAsync 会因为 characterId 为空而跳过保存，避免写入错误的记录
            _playerInventory = new PlayerInventoryData(_characterId ?? string.Empty);
        }
        // 再防御：AllItems 仍然可能是 null（极端情况），确保不为 null
        if (_playerInventory.allItems == null)
        {
            _playerInventory.allItems = new List<InventoryItem>();
        }

        var itemData = GameManager.Instance.ItemDataSo.GetItemDataById(itemId);
        if (itemData == null)
        {
            Debug.LogWarning($"未找到ID为 {itemId} 的物品数据");
            UIManager.Instance?.ShowToast("未找到物品数据");
            return false;
        }

        bool isStackable = itemData.itemType == ItemType.消耗品 || itemData.itemType == ItemType.材料;

        if (isStackable)
        {
            var existingItem = AllItems.FirstOrDefault(i => i.itemId == itemId && i.location == ItemLocation.Inventory);
            if (existingItem != null)
            {
                existingItem.count += count;
            }
            else
            {
                if (IsInventoryFull())
                {
                    Debug.Log("背包已满，无法添加新物品堆叠。");
                    UIManager.Instance?.ShowToast("背包已满，无法获得物品");
                    return false;
                }

                // [修正] 新创建的物品必须分配一个有效的格子索引。
                int emptySlot = FindFirstEmptyInventorySlot();
                if (emptySlot == -1) return false; // 再次确认，虽然前面检查过

                var newItem = new InventoryItem(itemId, count)
                {
                    location = ItemLocation.Inventory,
                    slotIndex = emptySlot
                };
                AllItems.Add(newItem);
            }
        }
        else // 不可堆叠物品 (装备)
        {
            for (int i = 0; i < count; i++)
            {
                if (IsInventoryFull())
                {
                    Debug.Log("背包已满，无法添加新装备。");
                    UIManager.Instance?.ShowToast("背包已满，无法获得装备");
                    return false;
                }

                var equipmentData = itemData as EquipmentData;
                if (equipmentData != null)
                {
                    // [修正] 新创建的装备也必须分配一个有效的格子索引。
                    int emptySlot = FindFirstEmptyInventorySlot();
                    if (emptySlot == -1) return false;

                    var newItem = new InventoryItem(itemId)
                    {
                        location = ItemLocation.Inventory,
                        slotIndex = emptySlot
                    };

                    equipmentData.GenerateBaseProperties(GameManager.Instance.PropertyScalingData);
                    // 深拷贝属性，避免后续再次调用 GenerateBaseProperties 导致已有物品属性被修改
                    newItem.generatedProperties = equipmentData.GetAllProperties()
                        .Select(p => p.DeepClone())
                        .ToList();
                    newItem.quantity = equipmentData.quantity;
                    AllItems.Add(newItem);
                }
            }
        }

        SaveInventoryAsync();
        OnInventoryUpdated?.Invoke();

        // 显示获得物品的 Toast 提示
        string itemName = itemData.itemName;
        if (string.IsNullOrEmpty(itemName))
        {
            itemName = "物品";
        }

        string message = count > 1 ? $"获得 {itemName} x{count}" : $"获得 {itemName}";
        UIManager.Instance.ShowToast(message, itemData.itemSprite);
        TaskEvents.TriggerItemCollected(itemId, count);
        return true;
    }


    /// <summary>
    /// 直接添加已生成的InventoryItem (用于抽卡等场景)
    /// </summary>
    public bool AddItemWithoutToast(int itemId, int count = 1, InventoryItem preGeneratedItem = null)
    {
        if (count <= 0) return false;

        // 防御性检查
        if (_playerInventory == null)
        {
            Debug.LogWarning("背包数据尚未加载完成，临时创建本地背包数据以接收物品。");
            _playerInventory = new PlayerInventoryData(_characterId ?? string.Empty);
        }

        if (_playerInventory.allItems == null)
        {
            _playerInventory.allItems = new List<InventoryItem>();
        }

        var itemData = GameManager.Instance.ItemDataSo.GetItemDataById(itemId);
        if (itemData == null)
        {
            Debug.LogWarning($"未找到ID为 {itemId} 的物品数据");
            return false;
        }

        // 如果有预生成的物品(通常是抽卡生成的装备)
        if (preGeneratedItem != null)
        {
            // 分配格子索引
            int emptySlot = FindFirstEmptyInventorySlot();
            if (emptySlot == -1)
            {
                UIManager.Instance?.ShowToast("背包已满，无法获得装备");
                return false;
            }

            preGeneratedItem.slotIndex = emptySlot;
            preGeneratedItem.location = ItemLocation.Inventory;

            AllItems.Add(preGeneratedItem);

            SaveInventoryAsync();
            OnInventoryUpdated?.Invoke();

            // 触发物品收集事件
            TaskEvents.TriggerItemCollected(itemId, count);

            return true;
        }

        return AddItem(itemId, count);
    }




    /// <summary>
    /// 彻底移除一个物品实例 (例如丢弃)。
    /// </summary>
    public bool RemoveItem(string instanceId)
    {
        var item = GetItemByInstanceId(instanceId);
        if (item != null)
        {
            // 获取物品信息以便显示提示
            var itemData = GameManager.Instance.ItemDataSo.GetItemDataById(item.itemId);
            string itemName = itemData != null && !string.IsNullOrEmpty(itemData.itemName) ? itemData.itemName : "物品";
            Sprite icon = itemData != null ? itemData.itemSprite : null;

            AllItems.Remove(item);
            SaveInventoryAsync();
            OnInventoryUpdated?.Invoke();

            UIManager.Instance?.ShowToast($"移除了 {itemName}", icon);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 减少物品数量，如果数量为0则移除 (例如制作、消耗)。
    /// </summary>
    public bool ReduceItemCount(string instanceId, int countToReduce)
    {
        var item = GetItemByInstanceId(instanceId);
        if (item == null || item.count < countToReduce) return false;

        item.count -= countToReduce;
        if (item.count <= 0)
        {
            AllItems.Remove(item);
        }

        SaveInventoryAsync();
        OnInventoryUpdated?.Invoke();
        return true;
    }

    /// <summary>
    /// 使用一个物品（当前仅支持消耗品）。
    /// </summary>
    public void UseItem(string instanceId)
    {
        var item = GetItemByInstanceId(instanceId);
        if (item == null) return;

        var itemData = GameManager.Instance.ItemDataSo.GetItemDataById(item.itemId);
        if (itemData == null) return;

        // 根据物品类型执行不同操作
        switch (itemData.itemType)
        {
            case ItemType.消耗品:
                var consumable = itemData as ConsumablesData;
                // 触发事件，让 CharacterState 等系统去应用效果
                OnItemConsumed?.Invoke(consumable);
                // 应用效果后，从库存中减少数量
                ReduceItemCount(instanceId, 1);

                if (consumable != null)
                {
                    // 对于回血/攻击力类型交给事件路由在真正生效后显示专用 toast（带专用 sprite），避免重复提示
                    if (consumable.consumablesType == ConsumablesType.回血 || consumable.consumablesType == ConsumablesType.加攻击力)
                    {
                        return;
                    }
                }
                // 其他类型仍显示通用使用提示
                string cname = !string.IsNullOrEmpty(itemData.itemName) ? itemData.itemName : "物品";
                UIManager.Instance?.ShowToast($"使用了 {cname}", itemData.itemSprite);
                break;
            default:
                UIManager.Instance?.ShowToast("该物品无法直接使用");
                break;
        }
    }

    #endregion

    #region 物品移动与装备 (Move & Equip)

    /// <summary>
    /// 移动物品到新的位置 (背包/装备栏/快捷栏)，如果目标位置有物品则交换。
    /// 这是所有拖拽、装备、卸下操作的核心。
    /// </summary>
    public void MoveItem(string instanceId, ItemLocation newLocation, int newSlotIndex)
    {
        var itemToMove = GetItemByInstanceId(instanceId);
        if (itemToMove == null) return;

        var originalLocation = itemToMove.location;
        var originalSlotIndex = itemToMove.slotIndex;

        var itemAtTarget = AllItems.FirstOrDefault(i => i.location == newLocation && i.slotIndex == newSlotIndex);

        if (itemAtTarget != null)
        {
            // 如果目标位置有物品，则交换它们的位置
            itemAtTarget.location = originalLocation;
            itemAtTarget.slotIndex = originalSlotIndex;
        }

        // 更新要移动的物品的位置信息
        itemToMove.location = newLocation;
        itemToMove.slotIndex = newSlotIndex;

        // 检查是否发生了装备或卸下行为，并触发相应事件
        var equipmentData = GameManager.Instance.ItemDataSo.GetEquipmentDataById(itemToMove.itemId);
        if (equipmentData != null)
        {
            if (newLocation == ItemLocation.Equipped && originalLocation != ItemLocation.Equipped)
            {
                OnItemEquipped?.Invoke(itemToMove, equipmentData);
                UIManager.Instance?.ShowToast($"装备了 {equipmentData.itemName}", equipmentData.itemSprite);
            }
            else if (originalLocation == ItemLocation.Equipped && newLocation != ItemLocation.Equipped)
            {
                OnItemUnequipped?.Invoke(itemToMove, equipmentData);
                UIManager.Instance?.ShowToast($"卸下了 {equipmentData.itemName}", equipmentData.itemSprite);
            }
        }

        SaveInventoryAsync();
        OnInventoryUpdated?.Invoke();
    }



    /// <summary>
    /// 装备一件物品的便捷方法。
    /// </summary>
    public void EquipItem(string instanceId, int equipmentSlot)
    {
        MoveItem(instanceId, ItemLocation.Equipped, equipmentSlot);
        AudioManager.Instance.PlayUISound(UISoundType.装备新装备);
    }

    /// <summary>
    /// 卸下一件装备的便捷方法。
    /// </summary>
    public void UnequipItem(string instanceId, int targetInventorySlot)
    {
        MoveItem(instanceId, ItemLocation.Inventory, targetInventorySlot);
        AudioManager.Instance.PlayUISound(UISoundType.卸下装备);
    }

    #endregion

    #region 数据查询 (Queries)

    public IEnumerable<InventoryItem> GetItemsByLocation(ItemLocation location)
    {
        return AllItems?.Where(item => item.location == location) ?? Enumerable.Empty<InventoryItem>();
    }

    public IEnumerable<InventoryItem> GetInventoryItems() => GetItemsByLocation(ItemLocation.Inventory);
    public IEnumerable<InventoryItem> GetEquippedItems() => GetItemsByLocation(ItemLocation.Equipped);
    public IEnumerable<InventoryItem> GetQuickSlotItems() => GetItemsByLocation(ItemLocation.QuickSlot);

    public InventoryItem GetItemByInstanceId(string instanceId)
    {
        return AllItems?.FirstOrDefault(item => item.instanceId == instanceId);
    }

    #endregion

    #region 背包状态 (Status)

    /// <summary>
    /// 检查背包是否已满（只计算主背包内的格子）。
    /// </summary>
    public bool IsInventoryFull()
    {
        return GetInventoryItems().Count() >= maxInventorySlots;
    }

    /// <summary>
    /// 查找并返回第一个可用的背包空格子索引。
    /// </summary>
    /// <returns>如果找到，返回格子索引；如果背包已满，返回 -1。</returns>
    public int FindFirstEmptyInventorySlot()
    {
        var occupiedSlots = GetInventoryItems()
            .Select(item => item.slotIndex)
            .ToHashSet();

        for (int i = 0; i < maxInventorySlots; i++)
        {
            if (!occupiedSlots.Contains(i))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// 清空整个玩家的所有物品（危险操作！）。
    /// </summary>
    public void ClearAllItems()
    {
        if (AllItems != null)
        {
            AllItems.Clear();
            SaveInventoryAsync();
            OnInventoryUpdated?.Invoke(); // [修正] 清空后需要通知UI刷新
        }
    }

    #endregion

    #region [删除] UI & 拖拽
    // [优化] 这部分逻辑已从InventoryManager中移除。
    // DragAndDropPanel现在是独立的单例，不再由InventoryManager创建。
    // 这使得InventoryManager的职责更纯粹：只管理数据，不负责UI。
    // public DragAndDropPanel GetOrCreateDragAndDropPanel(Canvas canvas) { ... }
    // public void DestroyDragAndDropPanel() { ... }
    #endregion

    #region 物品捡起 (Pickup)

    /// <summary>
    /// 批量捡起掉落物品
    /// </summary>
    /// <param name="droppedItemList">每个Vector2Int的x为itemID，y为数量</param>
    /// <returns>是否全部成功捡起</returns>
    public bool PickupItems(List<Vector2Int> droppedItemList)
    {
        bool allPicked = true;
        var itemDataDict = GameManager.Instance.ItemDataSo;
        foreach (var item in droppedItemList)
        {
            int itemId = item.x;
            int count = item.y;
            // 校验物品ID是否合法
            if (itemDataDict == null || itemDataDict.GetItemDataById(itemId) == null)
            {
                Debug.LogWarning($"无效物品ID: {itemId}");
                UIManager.Instance?.ShowToast("无效物品，无法捡起");
                allPicked = false;
                continue;
            }
            bool success = AddItem(itemId, count);
            if (!success)
            {
                var failedData = itemDataDict.GetItemDataById(itemId);
                string fn = failedData != null && !string.IsNullOrEmpty(failedData.itemName) ? failedData.itemName : "物品";
                Debug.LogWarning($"捡起物品失败: itemId={itemId}, count={count}");
                UIManager.Instance?.ShowToast($"无法捡起 {fn}");
                allPicked = false;
            }
        }
        if (allPicked)
        {
            OnInventoryUpdated?.Invoke();
        }
        return allPicked;
    }

    #endregion
}