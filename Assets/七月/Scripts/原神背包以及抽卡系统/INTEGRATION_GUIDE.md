# 原神抽卡系统集成到InventoryPanel指南

## 一、迁移概述

### 目标

将原神抽卡系统抽出的武器装备直接整合到InventoryPanel背包系统中，移除原神背包系统(PackageLocalData)，保留抽卡展示功能。

### 系统对比

| 功能     | 原神系统                       | InventoryPanel系统            |
| -------- | ------------------------------ | ----------------------------- |
| 数据存储 | PackageLocalData (PlayerPrefs) | PlayerInventoryData (MongoDB) |
| 物品模型 | PackageLocalItem               | InventoryItem                 |
| 背包UI   | PackagePanel/LotteryPanel      | InventoryPanel                |
| 属性系统 | 静态属性                       | 随机属性生成                  |
| 持久化   | PlayerPrefs                    | MongoDB                       |

### 迁移策略

保留抽卡展示 → 修改数据生成 → 整合到InventoryManager → 清理原背包系统

---

## 二、前期准备工作

### 2.1 备份当前代码

```bash
# 在项目根目录执行备份
cp -r Assets/七月/Scripts/原神背包以及抽卡系统 Assets/七月/Scripts/原神背包以及抽卡系统_backup
```

### 2.1 分析需要保留的功能

**保留文件:**

- `LotteryPanel.cs` - 抽卡界面展示
- `DrawCardPanel.cs` - 抽卡动画/逻辑
- `LotteryCell.cs` - 抽卡卡片UI
- `DrawCardPanel.cs` - 十连抽界面

**废弃文件:**

- `PackageLocalData.cs` - 原数据模型
- `PackagePanel.cs` - 原背包UI
- `PackageCell.cs` - 原背包格子
- `PackageDetail.cs` - 原详情面板

### 2.2 理解数据结构映射

#### 原神系统 -> InventoryPanel系统

```csharp
// PackageLocalItem (原神系统)
{
  uid: string,           → instanceId: string
  itemId: int,           → itemId: int
  itemName: string,      → 从ItemDataSO获取
  itemIcon: string,      → 从ItemDataSO获取
  starLevel: int,        → quantity: ItemQuality (映射转换)
  level: int,            → 可选扩展字段
  weaponType: int,       → equipmentType: EquipmentType
  attackPower: int,      → generatedProperties中的攻击属性
  defensePower: int,     → generatedProperties中的防御属性
  isLocked: bool,        → 可选扩展字段
}

// InventoryItem (InventoryPanel)
{
  instanceId: string,
  itemId: int,
  location: ItemLocation,
  slotIndex: int,
  count: int,           // 装备固定为1
  quantity: ItemQuality,
  generatedProperties: List<EquipmentProperty>
}
```

#### 星级与品质映射

```
原神星级 → Inventory品质
1星 → ItemQuality.普通
2星 → ItemQuality.普通
3星 → ItemQuality.稀有
4星 → ItemQuality.史诗
5星 → ItemQuality.传说
```

---

## 三、分步骤迁移指南

### 步骤1: 扩展ItemDataSO支持抽卡装备

**目标**: 在ItemDataSO中添加从PackageTable获取装备数据的逻辑

**操作**:

1. 打开 `Assets/Script/Data/道具数据/ItemDataSO.cs`
2. 添加以下方法:

```csharp
/// <summary>
/// 根据抽卡配置获取对应的装备数据
/// </summary>
public EquipmentData GetGachaEquipmentByPackageItem(PackageTableItem packageItem)
{
    if (packageItem == null) return null;

    // 根据装备ID查找对应的EquipmentData
    var equipment = allEquipmentData.FirstOrDefault(e => e.itemId == packageItem.itemId);
    return equipment;
}

/// <summary>
/// 从PackageTable随机抽取指定数量的装备
/// </summary>
public List<PackageTableItem> RandomGachaPull(int count, int weaponType = 1)
{
    var packageTable = GameManager.Instance.GetPackageTable();
    if (packageTable == null) return new List<PackageTableItem>();

    var result = new List<PackageTableItem>();
    var typeItems = packageTable.GetItemsByType(weaponType);

    for (int i = 0; i < count; i++)
    {
        if (typeItems.Count > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, typeItems.Count);
            result.Add(typeItems[randomIndex]);
        }
    }
    return result;
}
```

**注意事项**:

- 保持原有的EquipmentData结构不变
- PackageTable作为抽卡配置表继续使用
- 星级转换逻辑在后续步骤处理

---

### 步骤2: 修改抽卡逻辑生成InventoryItem

**目标**: 修改抽卡方法,生成符合InventoryPanel系统的InventoryItem对象

**操作**:

1. 打开 `Assets/Script/Mgr/GameManager.cs`
2. 找到原抽卡相关方法 (约110-150行)
3. 修改或替换为以下逻辑:

```csharp
/// <summary>
/// 单次抽卡 - 返回InventoryItem而非PackageLocalItem
/// </summary>
public InventoryItem GachaPullSingle(int weaponType = 1)
{
    // 1. 获取PackageTable配置
    var packageTable = GetPackageTable();
    if (packageTable == null) return null;

    // 2. 随机抽取配置项
    var typeItems = GetPackageTableByType(weaponType);
    if (typeItems.Count == 0) return null;

    int randomIndex = UnityEngine.Random.Range(0, typeItems.Count);
    var selectedItem = typeItems[randomIndex];

    // 3. 获取对应的EquipmentData
    var equipmentData = ItemDataSo.GetGachaEquipmentByPackageItem(selectedItem);
    if (equipmentData == null) return null;

    // 4. 将PackageTable的星级映射为ItemQuality
    ItemQuality quality = ConvertStarToQuality(selectedItem.starLevel);

    // 5. 临时修改品质以便生成属性
    var originalQuality = equipmentData.quantity;
    equipmentData.quantity = quality;

    // 6. 生成装备属性
    equipmentData.GenerateBaseProperties(PropertyScalingData);

    // 7. 创建InventoryItem实例
    var newItem = new InventoryItem(selectedItem.itemId)
    {
        location = ItemLocation.Inventory,
        slotIndex = -1, // 由InventoryManager分配
        quantity = quality,
        count = 1
    };

    // 8. 深拷贝生成的属性
    newItem.generatedProperties = equipmentData.GetAllProperties()
        .Select(p => p.DeepClone())
        .ToList();

    // 9. 恢复原始品质
    equipmentData.quantity = originalQuality;

    // 10. 直接添加到InventoryManager
    bool success = InventoryManager.Instance.AddItemWithoutToast(selectedItem.itemId, 1, newItem);

    if (success)
    {
        return newItem;
    }
    return null;
}

/// <summary>
/// 十连抽 - 返回InventoryItem列表
/// </summary>
public List<InventoryItem> GachaPullTen(int weaponType = 1, bool sort = false)
{
    var result = new List<InventoryItem>();

    for (int i = 0; i < 10; i++)
    {
        var item = GachaPullSingle(weaponType);
        if (item != null)
        {
            result.Add(item);
        }
    }

    // 如果需要排序
    if (sort && result.Count > 0)
    {
        result = result.OrderByDescending(x => x.quantity) // 按品质排序
                     .ThenBy(x => x.itemId)
                     .ToList();
    }

    return result;
}

/// <summary>
/// 将原神星级转换为ItemQuality
/// </summary>
private ItemQuality ConvertStarToQuality(int starLevel)
{
    return starLevel switch
    {
        1 => ItemQuality.普通,
        2 => ItemQuality.普通,
        3 => ItemQuality.稀有,
        4 => ItemQuality.史诗,
        5 => ItemQuality.传说,
        _ => ItemQuality.普通
    };
}
```

**注意事项**:

- 保留原有的星级概念用于UI展示
- 生成属性时使用映射后的品质
- 新增 `AddItemWithoutToast` 方法避免重复显示Toast

---

### 步骤3: 扩展InventoryManager支持直接添加

**目标**: 为InventoryManager添加直接添加已生成InventoryItem的方法

**操作**:

1. 打开 `Assets/Script/Mgr/InventoryManager.cs`
2. 在AddItem方法后添加新方法:

```csharp
/// <summary>
/// 直接添加已生成的InventoryItem (用于抽卡等场景)
/// </summary>
/// <param name="itemId">物品ID</param>
/// <param name="count">数量</param>
/// <param name="preGeneratedItem">预生成的InventoryItem对象</param>
/// <returns>是否添加成功</returns>
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

    // 普通物品添加逻辑 (原有代码)
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
                UIManager.Instance?.ShowToast("背包已满，无法获得物品");
                return false;
            }

            int emptySlot = FindFirstEmptyInventorySlot();
            if (emptySlot == -1) return false;

            var newItem = new InventoryItem(itemId, count)
            {
                location = ItemLocation.Inventory,
                slotIndex = emptySlot
            };
            AllItems.Add(newItem);
        }
    }

    SaveInventoryAsync();
    OnInventoryUpdated?.Invoke();
    TaskEvents.TriggerItemCollected(itemId, count);

    return true;
}
```

**注意事项**:

- 此方法专门用于已生成的装备
- 避免重复显示Toast,由抽卡界面统一展示
- 保持与原有AddItem方法逻辑一致

---

### 步骤4: 修改抽卡UI面板调用新逻辑

**目标**: 修改LotteryPanel和DrawCardPanel,调用新的抽卡方法

#### 4.1 修改LotteryPanel.cs

**操作**:

1. 打开 `Assets/七月/Scripts/原神背包以及抽卡系统/LotteryPanel.cs`
2. 找到抽卡相关方法,修改为:

```csharp
/// <summary>
/// 单次抽卡
/// </summary>
public void OnSingleDrawButtonClick()
{
    AudioManager.Instance.PlayUISound(UISoundType.按下按钮);

    // 调用新的抽卡方法
    var drawnItem = GameManager.Instance.GachaPullSingle();

    if (drawnItem != null)
    {
        // 显示抽卡结果
        ShowDrawResult(new List<InventoryItem> { drawnItem });

        // 显示Toast (可选,因为AddItemWithoutToast不显示)
        var itemData = GameManager.Instance.ItemDataSo.GetItemDataById(drawnItem.itemId);
        if (itemData != null)
        {
            string qualityText = GetQualityText(drawnItem.quantity);
            UIManager.Instance.ShowToast($"抽中了 {qualityText} {itemData.itemName}", itemData.itemSprite);
        }
    }
}

/// <summary>
/// 十连抽
/// </summary>
public void OnTenDrawButtonClick()
{
    AudioManager.Instance.PlayUISound(UISoundType.按下按钮);

    // 调用新的抽卡方法
    var drawnItems = GameManager.Instance.GachaPullTen(sort: true);

    if (drawnItems != null && drawnItems.Count > 0)
    {
        // 显示抽卡结果
        ShowDrawResult(drawnItems);

        // 统计稀有物品并显示Toast
        int rareOrAbove = drawnItems.Count(x => x.quantity >= ItemQuality.稀有);
        if (rareOrAbove > 0)
        {
            UIManager.Instance.ShowToast($"十连抽获得 {rareOrAbove} 个稀有及以上品质装备!");
        }
    }
}

/// <summary>
/// 显示抽卡结果 (复用现有的展示逻辑)
/// </summary>
private void ShowDrawResult(List<InventoryItem> items)
{
    // 使用现有的LotteryCell展示逻辑
    // 这部分代码可能需要微调以适配InventoryItem
    // 如果原有展示逻辑依赖PackageLocalItem,需要修改

    for (int i = 0; i < items.Count && i < resultCells.Count; i++)
    {
        var item = items[i];
        var itemData = GameManager.Instance.ItemDataSo.GetItemDataById(item.itemId);

        if (itemData != null && resultCells[i] != null)
        {
            // 修改LotteryCell的Init方法以接收InventoryItem
            resultCells[i].Init(item, itemData);
        }
    }
}

/// <summary>
/// 获取品质文本 (用于显示)
/// </summary>
private string GetQualityText(ItemQuality quality)
{
    return quality switch
    {
        ItemQuality.传说 => "传说",
        ItemQuality.史诗 => "史诗",
        ItemQuality.稀有 => "稀有",
        ItemQuality.普通 => "普通",
        _ => "未知"
    };
}
```

**注意事项**:

- 保持UI展示逻辑不变,只修改数据来源
- 需要修改LotteryCell以支持InventoryItem
- 保留动画和音效

#### 4.2 修改LotteryCell.cs

**操作**:

1. 打开 `Assets/七月/Scripts/原神背包以及抽卡系统/LotteryCell.cs`
2. 修改Init方法:

```csharp
/// <summary>
/// 初始化抽卡格子 (接收InventoryItem)
/// </summary>
public void Init(InventoryItem item, ItemData itemData)
{
    if (item == null || itemData == null)
    {
        ClearCell();
        return;
    }

    this.itemId = item.itemId;
    this.itemData = itemData;

    // 显示图标
    if (itemData.itemSprite != null)
    {
        iconImage.sprite = itemData.itemSprite;
        iconImage.enabled = true;
    }

    // 显示品质边框/颜色
    if (qualityBorder != null)
    {
        Color qualityColor = ItemQualityUtility.GetQualityColor(item.quantity);
        qualityBorder.color = qualityColor;
    }

    // 如果需要显示星级 (原神UI特性)
    if (starContainer != null)
    {
        ShowStars(ConvertQualityToStar(item.quantity));
    }

    // 播放入场动画
    PlayEntryAnimation();
}

/// <summary>
/// 将ItemQuality转换为星级 (用于UI展示)
/// </summary>
private int ConvertQualityToStar(ItemQuality quality)
{
    return quality switch
    {
        ItemQuality.传说 => 5,
        ItemQuality.史诗 => 4,
        ItemQuality.稀有 => 3,
        ItemQuality.普通 => 2,
        _ => 1
    };
}

/// <summary>
/// 显示星级 (假设有starContainer包含多个星星Image)
/// </summary>
private void ShowStars(int starCount)
{
    if (starContainer == null) return;

    for (int i = 0; i < starContainer.childCount; i++)
    {
        var star = starContainer.GetChild(i).GetComponent<Image>();
        if (star != null)
        {
            star.enabled = i < starCount;
        }
    }
}
```

**注意事项**:

- 保留原有的视觉风格(星级显示)
- 使用统一的品质颜色系统
- 可能需要添加ItemQualityUtility引用

---

### 步骤5: 处理详情面板兼容

**目标**: 确保InventoryPanel的详情面板能正确显示抽卡装备

**操作**:

1. 打开 `Assets/Script/UI/游戏中的面板/背包面板/EquipSlotDetailsPanel.cs`
2. 确保ShowDetails方法能正确显示装备属性

**通常这个面板已经支持InventoryItem,无需修改。**

但如果需要额外显示抽卡相关信息(如星级),可以扩展:

```csharp
public void ShowDetails(InventoryItem item)
{
    if (item == null) return;

    var equipmentData = GameManager.Instance.ItemDataSo.GetEquipmentDataById(item.itemId);
    if (equipmentData == null) return;

    // 基础信息
    itemNameText.text = equipmentData.itemName;
    itemIcon.sprite = equipmentData.itemSprite;

    // 显示星级 (新增)
    ShowStars(ConvertQualityToStar(item.quantity));

    // 显示属性 (已有逻辑)
    DisplayProperties(item.generatedProperties);

    // ...其他显示逻辑
}
```

---

### 步骤6: 清理原神背包系统

**目标**: 移除不再使用的PackageLocalData相关代码

#### 6.1 删除文件 (建议先移到备份文件夹)

```
Assets/七月/Scripts/原神背包以及抽卡系统/PackageLocalData.cs
Assets/七月/Scripts/原神背包以及抽卡系统/PackagePanel.cs
Assets/七月/Scripts/原神背包以及抽卡系统/PackageCell.cs
Assets/七月/Scripts/原神背包以及抽卡系统/PackageDetail.cs
```

#### 6.2 清理GameManager中的原背包方法

**操作**:

1. 打开 `Assets/Script/Mgr/GameManager.cs`
2. 找到并删除以下方法 (约66-150行):
   - `DeletePackageItems()`
   - `DeletePackageItem()`
   - `GetPackageTable()` (如果被抽卡系统使用则保留)
   - `GetPackageTableByType()` (如果被抽卡系统使用则保留)
   - `GetLotteryRandom1()`
   - `GetLotteryRandom10()`
   - `GetSortPackageLocalData()`
   - `GetPackageLocalItemByUId()`

#### 6.3 清理PlayerPrefs中的旧数据

**操作**:
在GameManager的Start或初始化方法中添加:

```csharp
// 清理原神背包系统的旧数据 (可选)
if (PlayerPrefs.HasKey("PackageLocalData"))
{
    PlayerPrefs.DeleteKey("PackageLocalData");
    PlayerPrefs.Save();
    Debug.Log("已清理原神背包系统的旧数据");
}
```

---

### 步骤7: 测试验证

#### 7.1 单元测试清单

- [ ] 单次抽卡能正确生成InventoryItem
- [ ] 十连抽能正确生成10个InventoryItem
- [ ] 抽卡装备的星级正确映射为品质
- [ ] 抽卡装备的属性正确生成
- [ ] 抽卡装备能正确添加到背包
- [ ] 抽卡装备在InventoryPanel中正确显示
- [ ] 抽卡装备可以装备、卸下
- [ ] 抽卡装备可以丢弃
- [ ] 抽卡装备详情正确显示

#### 7.2 集成测试流程

```
1. 启动游戏并登录角色
   ↓
2. 打开抽卡界面
   ↓
3. 点击单次抽卡
   ↓
4. 检查:
   - 动画播放正常
   - UI显示正常(图标、星级)
   - Toast提示正确
   ↓
5. 打开InventoryPanel
   ↓
6. 检查:
   - 新装备出现在背包中
   - 装备位置正确
   - 图标显示正确
   - 品质颜色正确
   ↓
7. 点击装备查看详情
   ↓
8. 检查:
   - 属性显示正确
   - 品质显示正确
   ↓
9. 右键装备装备
   ↓
10. 检查:
    - 装备移动到装备栏
    - 角色属性提升
    ↓
11. 十连抽测试 (重复步骤3-10)
```

#### 7.3 边界测试

- [ ] 背包已满时抽卡
- [ ] 网络断开时抽卡
- [ ] 快速连续抽卡
- [ ] 抽卡后立即退出游戏
- [ ] 抽卡后切换角色

---

## 四、数据迁移 (如有需要)

### 如果已有原神背包数据需要迁移

**场景**: 玩家已经在使用原神背包系统,需要将已有数据迁移到InventoryPanel

**操作**:

在GameManager中添加迁移方法:

```csharp
/// <summary>
/// 迁移原神背包数据到InventoryPanel系统
/// </summary>
public async void MigratePackageLocalDataToInventory()
{
    // 1. 检查是否有旧数据
    if (!PlayerPrefs.HasKey("PackageLocalData"))
    {
        Debug.Log("没有原神背包数据需要迁移");
        return;
    }

    // 2. 读取旧数据
    string inventoryJson = PlayerPrefs.GetString("PackageLocalData");
    var oldPackageData = JsonUtility.FromJson<PackageLocalData>(inventoryJson);

    if (oldPackageData?.items == null || oldPackageData.items.Count == 0)
    {
        Debug.Log("原神背包数据为空");
        return;
    }

    // 3. 等待InventoryManager加载完成
    await WaitForInventoryLoaded();

    // 4. 逐个迁移物品
    int migratedCount = 0;
    foreach (var oldItem in oldPackageData.items)
    {
        // 跳过无效物品
        if (oldItem == null) continue;

        // 检查物品是否存在
        var itemData = ItemDataSo.GetItemDataById(oldItem.itemId);
        if (itemData == null) continue;

        // 创建新的InventoryItem
        var newItem = new InventoryItem(oldItem.itemId)
        {
            location = ItemLocation.Inventory,
            slotIndex = -1,
            quantity = ConvertStarToQuality(oldItem.starLevel),
            count = 1
        };

        // 生成属性
        var equipmentData = itemData as EquipmentData;
        if (equipmentData != null && equipmentData.isRandomlyAttributes)
        {
            // 根据旧数据生成属性
            var originalQuality = equipmentData.quantity;
            equipmentData.quantity = newItem.quantity;
            equipmentData.GenerateBaseProperties(PropertyScalingData);
            newItem.generatedProperties = equipmentData.GetAllProperties()
                .Select(p => p.DeepClone())
                .ToList();
            equipmentData.quantity = originalQuality;
        }

        // 添加到背包
        bool success = InventoryManager.Instance.AddItemWithoutToast(
            oldItem.itemId,
            1,
            newItem
        );

        if (success) migratedCount++;
    }

    Debug.Log($"迁移完成: {migratedCount}/{oldPackageData.items.Count} 个物品");

    // 5. 删除旧数据
    PlayerPrefs.DeleteKey("PackageLocalData");
    PlayerPrefs.Save();

    // 6. 刷新UI
    OnInventoryUpdated?.Invoke();
}

/// <summary>
/// 等待InventoryManager加载完成
/// </summary>
private async Task WaitForInventoryLoaded()
{
    int maxWait = 100; // 最多等待10秒
    int count = 0;

    while (!InventoryManager.Instance.IsLoaded && count < maxWait)
    {
        await Task.Delay(100);
        count++;
    }

    if (!InventoryManager.Instance.IsLoaded)
    {
        Debug.LogError("等待InventoryManager加载超时");
    }
}
```

**调用时机**:

- 在角色登录后自动执行
- 或在设置菜单提供"迁移数据"按钮

---

## 五、后续优化建议

### 5.1 UI优化

- 在抽卡结果中增加"查看详情"按钮
- 在抽卡界面显示背包剩余空间
- 增加抽卡历史记录功能

### 5.2 功能扩展

- 支持抽卡保底机制
- 支持特定UP池(限定角色/装备)
- 支持抽卡预览功能

### 5.3 性能优化

- 抽卡动画使用对象池
- 装备图标预加载
- 批量保存优化

---

## 六、常见问题

### Q1: 抽卡后装备不在背包中显示?

**A**: 检查以下几点:

1. 是否调用了OnInventoryUpdated事件
2. InventoryPanel是否正确订阅了事件
3. 检查slotIndex是否正确分配

### Q2: 抽卡装备属性不正确?

**A**: 确保以下步骤:

1. 品质映射正确 (星级→ItemQuality)
2. GenerateBaseProperties调用时机正确
3. 属性深拷贝,避免被后续生成覆盖

### Q3: MongoDB保存失败?

**A**:

1. 检查MongoDB是否正常运行
2. 检查连接字符串是否正确
3. 查看控制台错误日志

### Q4: 星级显示异常?

**A**:

1. 检查ConvertStarToQuality和ConvertQualityToStar方法
2. 确认LotteryCell的Init方法正确接收InventoryItem
3. 检查星级Image组件是否存在

---

## 七、代码清单总结

### 需要修改的文件

1. `ItemDataSO.cs` - 添加抽卡数据获取方法
2. `GameManager.cs` - 修改抽卡逻辑
3. `InventoryManager.cs` - 添加AddItemWithoutToast方法
4. `LotteryPanel.cs` - 修改抽卡调用
5. `LotteryCell.cs` - 适配InventoryItem

### 需要删除的文件

1. `PackageLocalData.cs`
2. `PackagePanel.cs`
3. `PackageCell.cs`
4. `PackageDetail.cs`

### 无需修改的文件

1. `InventoryPanel.cs` - 已支持InventoryItem
2. `EquipSlotDetailsPanel.cs` - 已支持InventoryItem
3. `MongoDBManager.cs` - 已支持保存InventoryItem

---

## 八、回滚方案

如果迁移过程中出现问题,可以按以下步骤回滚:

1. 恢复备份的文件夹
2. 删除或重命名新增的方法
3. 恢复原有代码
4. 清理MongoDB中的新数据 (可选)

**建议**:

- 每完成一个步骤就进行测试
- 使用Git进行版本控制
- 保留完整的代码备份

---

## 九、时间估算

| 步骤                        | 预计时间    | 难度   |
| --------------------------- | ----------- | ------ |
| 步骤1: 扩展ItemDataSO       | 30分钟      | 低     |
| 步骤2: 修改抽卡逻辑         | 1小时       | 中     |
| 步骤3: 扩展InventoryManager | 45分钟      | 中     |
| 步骤4: 修改UI面板           | 1.5小时     | 中     |
| 步骤5: 详情面板兼容         | 15分钟      | 低     |
| 步骤6: 清理旧系统           | 30分钟      | 低     |
| 步骤7: 测试验证             | 2小时       | 高     |
| **总计**                    | **约6小时** | **中** |

---

## 十、最后检查清单

在完成所有步骤后,请确认:

- [ ] 抽卡功能正常工作
- [ ] 抽卡装备正确添加到InventoryPanel
- [ ] 装备属性生成正确
- [ ] UI显示正常(图标、品质、星级)
- [ ] 装备可以正常装备/卸下
- [ ] 数据正确保存到MongoDB
- [ ] 没有编译错误
- [ ] 没有运行时错误
- [ ] 性能无明显下降
- [ ] 已备份原始代码
- [ ] 已更新相关文档

---

**祝迁移顺利!如有问题,请参考常见问题部分或查看相关代码注释。**
