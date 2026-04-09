using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "装备属性缩放配置", menuName = "Data/装备属性缩放配置")]
public class PropertyScalingDataSO : ScriptableObject
{
    [Header("--- 核心成长规则 ---")]
    [Tooltip("每多少级属性进行一次阶梯式成长")]
    [SerializeField] private int tierStep = 10;
    
    [Tooltip("每个阶梯相对于上一个阶梯的属性倍率")]
    [SerializeField] private float tierMultiplier = 2.0f;

    [Header("--- 初始基础值 (1级时) ---")]
    [Tooltip("定义1-10级装备的各项属性基础值")]
    [SerializeField] private List<PropertyBaseValue> initialTierProperties;
    
    [Header("--- 品质修正系数 ---")]
    [Tooltip("定义不同品质对基础属性的修正范围")]
    [SerializeField] private List<QualityModifier> qualityModifiers;

    // 根据等级动态计算对应的阶梯配置
    public LevelTierProperties GetTierProperties(int level)
    {
        if (level < 1) level = 1;

        // 1. 计算当前等级属于第几个阶梯 (从0开始)
        // 例如：1-10级是第0阶, 11-20级是第1阶, 21-30级是第2阶
        int tierIndex = (level - 1) / tierStep;

        // 2. 计算当前阶梯的总成长倍率
        // tierIndex=0, multiplier=1 (2^0)
        // tierIndex=1, multiplier=2 (2^1)
        // tierIndex=2, multiplier=4 (2^2)
        float totalMultiplier = Mathf.Pow(tierMultiplier, tierIndex);
        
        // 3. 动态创建一个 LevelTierProperties 实例并填充计算后的数据
        var calculatedProperties = new LevelTierProperties
        {
            levelTier = (tierIndex * tierStep) + 1,
            propertyBaseValues = new List<PropertyBaseValue>()
        };

        foreach (var initialProp in initialTierProperties)
        {
            calculatedProperties.propertyBaseValues.Add(new PropertyBaseValue
            {
                propertyType = initialProp.propertyType,
                baseValue = initialProp.baseValue * totalMultiplier
            });
        }
        
        return calculatedProperties;
    }
    
    // GetQualityModifier 方法保持不变
    public QualityModifier GetQualityModifier(ItemQuality quality)
    {
        return qualityModifiers.FirstOrDefault(q => q.quality == quality);
    }
}
// PropertyBaseValue 和 QualityModifier 类保持不变
[Serializable]
public class PropertyBaseValue
{
    public PropertyType propertyType;
    public float baseValue;
}

[Serializable]
public class QualityModifier
{
    public ItemQuality quality;
    [Header("最小修正系数 (例如 0.5)")]
    public float minModifier;
    [Header("最大修正系数 (例如 0.6)")]
    public float maxModifier;
}

// LevelTierProperties 类也保持不变，但我们现在会在代码中动态创建它
[Serializable]
public class LevelTierProperties
{
    [Header("等级阶梯 (例如: 10, 代表1-10级)")]
    public int levelTier;
    public List<PropertyBaseValue> propertyBaseValues;

    public float GetBaseValue(PropertyType type)
    {
        var prop = propertyBaseValues.FirstOrDefault(p => p.propertyType == type);
        return prop != null ? prop.baseValue : 0f;
    }
}

