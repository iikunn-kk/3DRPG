using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 旧背包/抽卡系统管理器
/// 承接从 GameManager 中剥离的旧背包（PackageLocalData）和抽卡相关方法
/// 职责：
/// 1. 旧背包物品的增删查改
/// 2. 抽卡单抽/十连
/// 3. 物品星级→品质转换
/// 4. 背包表格数据缓存
/// </summary>
public class LegacyPackageManager : Singleton<LegacyPackageManager>
{
    /// <summary>
    /// 背包表格数据缓存
    /// 从Resources加载的背包配置表，包含物品类型、权重等信息
    /// 使用延迟加载模式，首次访问时加载
    /// </summary>
    private PackageTable packageTable;

    // ==================== 背包表格数据 ====================

    /// <summary>
    /// 获取背包表格数据
    /// 采用延迟加载模式，首次访问时从Resources加载
    /// </summary>
    public PackageTable GetPackageTable()
    {
        if (packageTable == null)
        {
            packageTable = Resources.Load<PackageTable>("TableData/PackageTable");
        }
        return packageTable;
    }

    /// <summary>
    /// 根据物品类型获取背包表格数据
    /// 从背包表格中筛选指定类型的所有物品配置
    /// </summary>
    /// <param name="type">物品类型：1=武器，2=食物（对应GameConst中的定义）</param>
    /// <returns>指定类型的物品配置列表</returns>
    public List<PackageTableItem> GetPackageTableByType(int type)
    {
        List<PackageTableItem> packageItems = new List<PackageTableItem>();
        foreach (PackageTableItem packageItem in GetPackageTable().DataList)
        {
            if (packageItem.type == type)
            {
                packageItems.Add(packageItem);
            }
        }
        return packageItems;
    }

    // ==================== 背包物品检索 ====================

    /// <summary>
    /// 获取背包本地数据
    /// 从本地存储加载玩家的背包物品数据
    /// </summary>
    public List<PackageLocalItem> GetPackageLocalData()
    {
        return PackageLocalData.Instance.LoadPackage();
    }

    /// <summary>
    /// 根据ID获取物品表格配置
    /// 通过物品ID在表格中查找对应的配置数据
    /// </summary>
    public PackageTableItem GetPackageItemById(int id)
    {
        List<PackageTableItem> packageDataList = GetPackageTable().DataList;
        foreach (PackageTableItem item in packageDataList)
        {
            if (item.id == id)
            {
                return item;
            }
        }
        return null;
    }

    /// <summary>
    /// 根据UID获取背包物品本地数据
    /// 通过唯一标识符精确查找背包中的物品实例
    /// </summary>
    public PackageLocalItem GetPackageLocalItemByUId(string uid)
    {
        List<PackageLocalItem> packageDataList = GetPackageLocalData();
        foreach (PackageLocalItem item in packageDataList)
        {
            if (item.uid == uid)
            {
                return item;
            }
        }
        return null;
    }

    /// <summary>
    /// 获取排序后的背包数据
    /// 加载背包数据并按自定义规则排序后返回
    /// </summary>
    public List<PackageLocalItem> GetSortPackageLocalData()
    {
        List<PackageLocalItem> localItems = PackageLocalData.Instance.LoadPackage();
        localItems.Sort(new PackageItemComparer());
        return localItems;
    }

    // ==================== 背包物品操作 ====================

    /// <summary>
    /// 删除单个背包物品
    /// </summary>
    public void DeletePackageItem(string uid, bool needSave = true)
    {
        PackageLocalItem packageLocalItem = GetPackageLocalItemByUId(uid);
        if (packageLocalItem == null)
            return;

        PackageLocalData.Instance.items.Remove(packageLocalItem);

        if (needSave)
        {
            PackageLocalData.Instance.SavePackage();
        }
    }

    /// <summary>
    /// 删除多个背包物品
    /// </summary>
    public void DeletePackageItems(List<string> uids)
    {
        foreach (string uid in uids)
        {
            DeletePackageItem(uid, false);
        }
        PackageLocalData.Instance.SavePackage();
    }

    /// <summary>
    /// 检查武器是否为新获得
    /// </summary>
    public bool CheckWeaponIsNew(int id)
    {
        foreach (PackageLocalItem packageLocalItem in GetPackageLocalData())
        {
            if (packageLocalItem.id == id)
            {
                return false;
            }
        }
        return true;
    }

    // ==================== 抽卡系统 ====================

    /// <summary>
    /// 将星级转换为 ItemQuality
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

    /// <summary>
    /// 随机抽卡 - 单抽
    /// 从武器池中随机抽取一件武器并添加到主背包
    /// </summary>
    public InventoryItem GetLotteryRandom1()
    {
        List<PackageTableItem> packageItems = GetPackageTableByType(GameConst.PackageTypeWeapon);
        int index = Random.Range(0, packageItems.Count);
        PackageTableItem packageItem = packageItems[index];

        var newItem = new InventoryItem(packageItem.id)
        {
            count = 1
        };

        ItemQuality quality = ConvertStarToQuality(packageItem.star);
        newItem.quantity = quality;

        var equipmentData = GameManager.Instance.ItemDataSo.GetEquipmentDataById(packageItem.id);
        if (equipmentData != null && equipmentData.isRandomlyAttributes)
        {
            var originalQuality = equipmentData.quantity;
            equipmentData.quantity = quality;
            equipmentData.GenerateBaseProperties(GameManager.Instance.PropertyScalingData);

            newItem.generatedProperties = equipmentData.GetAllProperties()
                .Select(p => p.DeepClone())
                .ToList();

            equipmentData.quantity = originalQuality;
        }

        bool success = InventoryManager.Instance.AddItemWithoutToast(packageItem.id, 1, newItem);
        return success ? newItem : null;
    }

    /// <summary>
    /// 随机抽卡 - 十连抽
    /// </summary>
    public List<InventoryItem> GetLotteryRandom10(bool sort = false)
    {
        List<InventoryItem> items = new();

        for (int i = 0; i < 10; i++)
        {
            InventoryItem item = GetLotteryRandom1();
            if (item != null)
            {
                items.Add(item);
            }
        }

        if (sort && items.Count > 0)
        {
            items = items.OrderByDescending(x => x.quantity)
                         .ThenBy(x => x.itemId)
                         .ToList();
        }

        return items;
    }
}
