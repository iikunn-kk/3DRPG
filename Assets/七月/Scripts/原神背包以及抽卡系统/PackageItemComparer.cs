using System.Collections.Generic;

/// <summary>
/// 背包物品比较器
/// 实现IComparer接口，用于对背包物品进行多级排序
/// 排序规则：
/// 1. 首先按星级降序（星级高的排前面）
/// 2. 星级相同则按ID降序
/// 3. ID也相同则按等级降序
/// </summary>
public class PackageItemComparer : IComparer<PackageLocalItem>
{
    /// <summary>
    /// 比较两个背包物品
    /// </summary>
    /// <param name="a">第一个物品</param>
    /// <param name="b">第二个物品</param>
    /// <returns>比较结果：正数a在b后面，负数a在b前面，0相等</returns>
    public int Compare(PackageLocalItem a, PackageLocalItem b)
    {
        // 获取两个物品对应的表格配置数据
        PackageTableItem x = LegacyPackageManager.Instance.GetPackageItemById(a.id);
        PackageTableItem y = LegacyPackageManager.Instance.GetPackageItemById(b.id);

        // 第一级排序：按星级从大到小排序
        int starComparison = y.star.CompareTo(x.star);

        // 如果星级相同，进入第二级排序
        if (starComparison == 0)
        {
            // 按ID从大到小排序
            int idComparison = y.id.CompareTo(x.id);

            // 如果ID也相同，进入第三级排序
            if (idComparison == 0)
            {
                // 按等级从大到小排序
                return b.level.CompareTo(a.level);
            }

            return idComparison;
        }

        return starComparison;
    }
}
