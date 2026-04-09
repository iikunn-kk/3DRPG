using System;
using MongoDB.Bson.Serialization.Attributes;
using UnityEngine;

using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;

[CreateAssetMenu(fileName = "所有的物品数据", menuName = "Data/所有的物品数据")]
public class ItemDataSO : ScriptableObject
{
   [Header("所有的消耗品")]
   [SerializeField]private List<ConsumablesData> allConsumablesData;
   [Header("所有的装备")]
   [SerializeField]private List<EquipmentData> allEquipmentData;
   [Header("所有的材料")]
   [SerializeField]private List<MaterialData> allMaterialData;
   
   public ConsumablesData GetConsumablesDataById(int id)
   {
      return allConsumablesData.FirstOrDefault(x => x.itemId == id);
   }
   public EquipmentData GetEquipmentDataById(int id)
   {
      return allEquipmentData.FirstOrDefault(x => x.itemId == id);
   }
   public MaterialData GetMaterialDataById(int id)
   {
      return allMaterialData.FirstOrDefault(x => x.itemId == id);
   }
   public ItemData GetItemDataById(int id)
   {
      ItemData data = GetConsumablesDataById(id);
      if (data != null) return data;
    
      data = GetEquipmentDataById(id);
      if (data != null) return data;

      data = GetMaterialDataById(id);
      return data;
   }
   
   /// <summary>
   /// 随机生成N个物品添加到背包中（用于调试）
   /// </summary>
   /// <param name="count">要生成的物品数量</param>
   public void GenerateRandomItems(int count)
   {
       // 收集所有可堆叠的物品（消耗品和材料）
       var stackableItems = new List<ItemData>();
       
       // 添加所有消耗品
       foreach (var consumable in allConsumablesData)
       {
           stackableItems.Add(consumable);
       }
       
       // 添加所有材料
       foreach (var material in allMaterialData)
       {
           stackableItems.Add(material);
       }
       
       if (stackableItems.Count == 0)
       {
           Debug.LogWarning("没有可生成的物品数据");
           return;
       }
       
       // 随机选择并添加物品
       for (int i = 0; i < count; i++)
       {
           int randomIndex = UnityEngine.Random.Range(0, stackableItems.Count);
           ItemData randomItem = stackableItems[randomIndex];
           
           // 随机数量（1-10）
           int randomCount = UnityEngine.Random.Range(1, 11);
           
           InventoryManager.Instance.AddItem(randomItem.itemId, randomCount);
       }
       
       Debug.Log($"已生成 {count} 个随机物品");
   }
   
   /// <summary>
   /// 随机生成N个装备添加到背包中（用于调试）
   /// </summary>
   /// <param name="count">要生成的装备数量</param>
   public void GenerateRandomEquipment(int count)
   {
       if (allEquipmentData.Count == 0)
       {
           Debug.LogWarning("没有可生成的装备数据");
           return;
       }
       
       // 随机选择并添加装备
       for (int i = 0; i < count; i++)
       {
           int randomIndex = UnityEngine.Random.Range(0, allEquipmentData.Count);
           EquipmentData randomEquipment = allEquipmentData[randomIndex];
           InventoryManager.Instance.AddItem(randomEquipment.itemId, 1);
       }
       
       Debug.Log($"已生成 {count} 个随机装备");
   }
}

/// <summary>
/// 物品数据类
/// </summary>
[Serializable]
public class ItemData
{
   [BsonId]
   public int itemId;
   public string itemName;
   public ItemQuality quantity;
   [TextArea]
   public string itemDescription;
   public Sprite itemSprite;
   public ItemType itemType;
   
   public ItemData(int itemId, string itemName,string itemDescription, ItemQuality quantity)
   {
      this.itemId = itemId;
      this.itemName = itemName;
      this.itemDescription = itemDescription;
      this.quantity = quantity;
   }
    
   public int GetMySellPrice()
   {
       switch (itemType)
       {
           case ItemType.装备:
               return  100;
           case ItemType.消耗品:
               return  10;
           case ItemType.材料:
               return 5;
           default:
               throw new ArgumentOutOfRangeException();
       }
   }
   public ItemData()
   {
        
   }
}

[Serializable]
public class ConsumablesData : ItemData
{

   public ConsumablesType consumablesType;
   [Header("是否是百分比")]
   public bool isPercentage=true;
   [Header("数值")]
   public float value;
}

[Serializable]
public class EquipmentData : ItemData
{
   public EquipmentType equipmentType;
   [Header("最低装备等级")]
   public int minimumLevel = 1;

   [Header("是随机获取属性")]
   public bool isRandomlyAttributes;

   // 自动生成的属性
   [Header("基础属性")]
   public List<EquipmentProperty> baseProperties = new List<EquipmentProperty>();
   
   // 自定义属性（用于任务奖励等特殊装备）
   [Header("自定义属性")]
   public List<EquipmentProperty> customProperties = new List<EquipmentProperty>();
   
   // 获取所有属性（基础属性+自定义属性）
   public List<EquipmentProperty> GetAllProperties()
   {
      List<EquipmentProperty> allProperties = new List<EquipmentProperty>();
      allProperties.AddRange(baseProperties);
      allProperties.AddRange(customProperties);
      return allProperties;
   }
   
   /// <summary>
   /// 根据配置数据动态生成基础属性
   /// </summary>
   public void GenerateBaseProperties(PropertyScalingDataSO scalingData)
   {
       if (!isRandomlyAttributes)
       {
           // 如果不是随机属性，则使用预设的 customProperties
           foreach (var prop in customProperties)
           {
               // 确保预设属性也有一个随机值（如果在其范围内）
               prop.GenerateActualValue();
           }
           return;
       }

       baseProperties.Clear();

       // 1. 根据品质决定基础属性数量
       int propertyCount = 0;
       switch (quantity)
       {
           case ItemQuality.普通:
               propertyCount = UnityEngine.Random.Range(1, 3); // 1-2条属性
               break;
           case ItemQuality.稀有:
               propertyCount = UnityEngine.Random.Range(2, 4); // 2-3条属性
               break;
           case ItemQuality.史诗:
               propertyCount = UnityEngine.Random.Range(3, 5); // 3-4条属性
               break;
           case ItemQuality.传说:
               propertyCount = UnityEngine.Random.Range(4, 6); // 4-5条属性
               break;
       }

       // 2. 获取当前装备等级对应的属性阶梯和品质修正
       var tierProps = scalingData.GetTierProperties(this.minimumLevel);
       var qualityMod = scalingData.GetQualityModifier(this.quantity);

       if (tierProps == null || qualityMod == null)
       {
           Debug.LogError($"未找到等级 {this.minimumLevel} 或品质 {this.quantity} 的属性配置！");
           return;
       }

       // 3. 随机选择属性并生成
       List<PropertyType> availableProperties = Enum.GetValues(typeof(PropertyType)).Cast<PropertyType>().ToList();

       for (int i = 0; i < propertyCount; i++)
       {
           if (availableProperties.Count == 0) break;

           int randomIndex = UnityEngine.Random.Range(0, availableProperties.Count);
           PropertyType randomPropertyType = availableProperties[randomIndex];
           availableProperties.RemoveAt(randomIndex); 

           float baseValue = tierProps.GetBaseValue(randomPropertyType);
           if (baseValue <= 0) continue; 

           float finalMinValue = baseValue * qualityMod.minModifier;
           float finalMaxValue = baseValue * qualityMod.maxModifier;
           
           EquipmentProperty newProperty = new EquipmentProperty
           {
               propertyType = randomPropertyType,
               minValue = finalMinValue,
               maxValue = finalMaxValue
           };
           
           newProperty.GenerateActualValue();

           // 根据是否为百分比属性决定是否取整
           if (!newProperty.IsPercentage)
           {
              newProperty.actualValue = Mathf.RoundToInt(newProperty.actualValue);
           }
           
           baseProperties.Add(newProperty);
       }
   }

   // --- AddRandomProperty 方法已被删除 ---
}

[Serializable]
public class MaterialData : ItemData
{
    // 材料目前没有额外属性
}

[Serializable]
public class EquipmentProperty
{
   [Header("属性类型")]
   public PropertyType propertyType;
   
   [Header("最小值")]
   public float minValue;
   
   [Header("最大值")]
   public float maxValue;
   [Header("无需修改")]
   // 实际生成的数值
   public float actualValue;

   /// <summary>
   /// [新增] 判断该属性是否是百分比类型
   /// </summary>
   public bool IsPercentage
   {
      get
      {
         return propertyType == PropertyType.物理增伤 || 
                propertyType == PropertyType.魔法增伤;
      }
   }
   
   public void GenerateActualValue()
   {
      actualValue = UnityEngine.Random.Range(minValue, maxValue);
   }

   /// <summary>
   /// [新增] 获取用于UI显示的格式化字符串
   /// </summary>
   /// <returns>例如: "+10 攻击" 或 "+5.5% 物理增伤"</returns>
   public string GetDisplayText()
   {
      string valueString;
      if (IsPercentage)
      {
         valueString = $"+{actualValue:F1}%";
      }
      else
      {
         valueString = $"+{Mathf.RoundToInt(actualValue)}";
      }
      return $"{valueString} {propertyType}";
   }

   // 深拷贝，防止 ScriptableObject 中的 baseProperties 被后续随机生成覆盖导致已生成装备属性丢失或串改
   public EquipmentProperty DeepClone()
   {
      return new EquipmentProperty
      {
         propertyType = this.propertyType,
         minValue = this.minValue,
         maxValue = this.maxValue,
         actualValue = this.actualValue
      };
   }
}
public enum PropertyType
{
   攻击,
   防御,
   生命,
   生命回复,
   物理增伤,
   魔法增伤
}

// 用于存储消耗品的数据结构
[Serializable]
public class StoredConsumable
{
   public int itemId; // 模板ID
   public int count;  // 数量
   
   // 背包中的位置，-1表示不在背包中（可能在快捷栏）
   public int inventorySlotIndex = -1;
}

// 用于存储装备的数据结构
[Serializable]
public class StoredEquipment
{
   public int itemId; // 模板ID
   public ItemQuality quantity; // 品质，因为它决定了外观或基础设定
    
   // 核心：直接存储随机生成的结果
   public List<EquipmentProperty> generatedProperties = new();
   
   // 快捷栏位置，-1表示不在快捷栏中
   public int quickSlotIndex = -1;
   
   // 背包中的位置，-1表示不在背包中（可能在装备栏或快捷栏）
   public int inventorySlotIndex = -1;
}

// 用于存储材料的数据结构
[Serializable]
public class StoredMaterial
{
   public int itemId; // 模板ID
   public int count;  // 数量
   
   // 背包中的位置，-1表示不在背包中
   public int inventorySlotIndex = -1;
}
// 这个新的枚举将用来标识物品在哪个“容器”里
public enum ItemLocation
{
   Inventory,  // 在主背包中
   Equipped,   // 在角色装备栏上
   QuickSlot   // 在快捷栏中
}

[Serializable]
public class InventoryItem
{
   // --- 通用属性 ---
   [BsonId] // 每个物品实例都应该有一个唯一的ID，方便查找和修改
   public string instanceId { get; set; } = ObjectId.GenerateNewId().ToString();

   public int itemId; // 物品的模板ID (关联到 ItemDataSO)

   public ItemLocation location = ItemLocation.Inventory; // 物品当前所在位置

   // --- 位置索引 ---
   // 注意：一个物品同一时间只会有一个位置。
   // slotIndex 可以表示它在背包、装备栏或快捷栏中的具体格子索引。
   public int slotIndex = -1; 

   // --- 可堆叠物品属性 (消耗品, 材料) ---
   public int count = 1;

   // --- 装备特有属性 ---
   public ItemQuality quantity; // 品质需要存储，因为它影响随机属性
   public List<EquipmentProperty> generatedProperties = new(); // 存储已生成的属性

   // 无参数构造函数是序列化所必需的
   public InventoryItem() { }

   // 方便创建的构造函数
   public InventoryItem(int itemId, int count = 1)
   {
      this.itemId = itemId;
      this.count = count;
   }

   // 深拷贝方法，用于在合并临时物品时避免 instanceId 和引用冲突
   public InventoryItem DeepClone()
   {
      var clone = new InventoryItem()
      {
         instanceId = ObjectId.GenerateNewId().ToString(),
         itemId = this.itemId,
         location = this.location,
         slotIndex = this.slotIndex,
         count = this.count,
         quantity = this.quantity,
         generatedProperties = this.generatedProperties != null ? new List<EquipmentProperty>(this.generatedProperties.Select(p => p.DeepClone())) : new List<EquipmentProperty>()
      };
      return clone;
   }
}