using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 抽卡服务 — 从 LegacyPackageManager 提取的纯抽卡逻辑。
/// 新背包系统由 InventoryManager 负责，老背包已移除。
/// </summary>
public class LotteryService : Singleton<LotteryService>
{
    private PackageTable _packageTable;

    // ===== 配置加载 =====

    public PackageTable GetPackageTable()
    {
        if (_packageTable == null)
            _packageTable = Resources.Load<PackageTable>("TableData/PackageTable");
        return _packageTable;
    }

    public List<PackageTableItem> GetPackageTableByType(int type)
    {
        var result = new List<PackageTableItem>();
        foreach (var item in GetPackageTable().DataList)
            if (item.type == type)
                result.Add(item);
        return result;
    }

    public PackageTableItem GetPackageItemById(int id)
    {
        foreach (var item in GetPackageTable().DataList)
            if (item.id == id)
                return item;
        return null;
    }

    // ===== 抽卡 =====

    public InventoryItem GetLotteryRandom1()
    {
        var packageItems = GetPackageTableByType(GameConst.PackageTypeWeapon);
        int index = Random.Range(0, packageItems.Count);
        var packageItem = packageItems[index];

        var newItem = new InventoryItem(packageItem.id) { count = 1 };
        var quality = StarToQuality(packageItem.star);
        newItem.quantity = quality;

        var equipData = GameDataConfig.Instance.ItemDataSo.GetEquipmentDataById(packageItem.id);
        if (equipData != null && equipData.isRandomlyAttributes)
        {
            var originalQuality = equipData.quantity;
            equipData.quantity = quality;
            equipData.GenerateBaseProperties(GameDataConfig.Instance.PropertyScalingData);
            newItem.generatedProperties = equipData.GetAllProperties()
                .Select(p => p.DeepClone())
                .ToList();
            equipData.quantity = originalQuality;
        }

        bool success = InventoryManager.Instance.AddItemWithoutToast(packageItem.id, 1, newItem);
        return success ? newItem : null;
    }

    public List<InventoryItem> GetLotteryRandom10(bool sort = false)
    {
        var items = new List<InventoryItem>();
        for (int i = 0; i < 10; i++)
        {
            var item = GetLotteryRandom1();
            if (item != null)
                items.Add(item);
        }

        if (sort && items.Count > 0)
        {
            items = items.OrderByDescending(x => x.quantity)
                         .ThenBy(x => x.itemId)
                         .ToList();
        }

        return items;
    }

    // ===== 工具 =====

    private ItemQuality StarToQuality(int starLevel)
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
}
