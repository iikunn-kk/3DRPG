using UnityEngine;

/// <summary>
/// 物品品质工具类，提供根据物品品质获取对应颜色的方法
/// </summary>
public static class ItemQualityUtility
{
    /// <summary>
    /// 根据物品品质返回对应的颜色（炉石传说风格的低饱和度浅色系）
    /// </summary>
    /// <param name="quality">物品品质</param>
    /// <returns>品质对应的颜色</returns>
    public static Color GetQualityColor(ItemQuality quality)
    {
        switch (quality)
        {
            case ItemQuality.普通:
                return new Color(0.8f, 0.8f, 0.8f); // 灰色 - 普通品质
            case ItemQuality.稀有:
                return new Color(0.4f, 0.6f, 1f); // 蓝色 - 稀有品质
            case ItemQuality.史诗:
                return new Color(0.7f, 0.4f, 0.9f); // 紫色 - 史诗品质
            case ItemQuality.传说:
                return new Color(1f, 0.8f, 0.3f); // 橙色 - 传说品质
            default:
                return new Color(0.7f, 0.7f, 0.7f); // 默认使用普通品质颜色
        }
    }
}