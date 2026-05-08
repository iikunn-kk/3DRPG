using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 背包本地数据管理器
/// 负责管理玩家背包数据的本地持久化存储
/// 采用单例模式设计，确保全局只有一个实例管理背包数据
/// 使用PlayerPrefs作为存储后端，实现背包数据的保存和加载
/// </summary>
public class PackageLocalData
{
    // ==================== 单例模式实现 ====================
    
    /// <summary>
    /// 单例实例私有字段
    /// 使用延迟初始化模式，首次访问时创建实例
    /// </summary>
    private static PackageLocalData _instance;

    /// <summary>
    /// 单例属性访问器
    /// 提供全局访问点，获取PackageLocalData的唯一实例
    /// 线程安全的懒汉式单例实现
    /// </summary>
    /// <value>返回PackageLocalData的单例实例</value>
    public static PackageLocalData Instance
    {
        get
        {
            // 检查实例是否已创建
            if (_instance == null)
            {
                // 实例为空时创建新实例
                _instance = new PackageLocalData();
            }
            // 返回单例实例
            return _instance;
        }
    }

    // ==================== 数据字段 ====================
    
    /// <summary>
    /// 背包物品列表
    /// 存储当前玩家背包中的所有物品
    /// 每个物品包含唯一标识符、物品ID、数量、等级等信息
    /// </summary>
    public List<PackageLocalItem> items;

    // ==================== 数据持久化方法 ====================

    /// <summary>
    /// 保存背包数据到本地存储
    /// 将当前背包物品列表序列化为JSON格式并存储到PlayerPrefs
    /// 
    /// 调用时机建议：
    /// - 背包数据发生变更后（如获得物品、丢弃物品、使用物品等）
    /// - 场景切换前
    /// - 退出游戏前
    /// </summary>
    public void SavePackage()
    {
        // 将当前对象序列化为JSON字符串
        // JsonUtility.ToJson会将items列表及其所有物品数据转换为JSON格式
        string inventoryJson = JsonUtility.ToJson(this);
        
        // 将JSON字符串存储到PlayerPrefs，键名为"PackageLocalData"
        // PlayerPrefs是Unity提供的轻量级本地存储方案，适合存储小数据量
        PlayerPrefs.SetString("PackageLocalData", inventoryJson);
        
        // 立即将数据写入磁盘
        // 注意：频繁调用Save会影响性能，建议批量操作后再保存
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 从本地存储加载背包数据
    /// 从PlayerPrefs中读取并反序列化背包数据
    /// 
    /// 加载逻辑说明：
    /// 1. 如果内存中已有数据，直接返回（避免重复加载）
    /// 2. 如果本地有存储数据，反序列化并返回
    /// 3. 如果本地无数据，创建新的空列表
    /// </summary>
    /// <returns>背包物品列表，如果无数据则返回空列表</returns>
    public List<PackageLocalItem> LoadPackage()
    {
        // 第一层检查：内存中已有数据
        if (items != null)
        {
            // 直接返回内存中的数据，避免重复加载
            return items;
        }
        
        // 第二层检查：本地存储中是否有数据
        if (PlayerPrefs.HasKey("PackageLocalData"))
        {
            // 从PlayerPrefs读取JSON字符串
            string inventoryJson = PlayerPrefs.GetString("PackageLocalData");
            
            // 将JSON反序列化为PackageLocalData对象
            PackageLocalData packageLocalData = JsonUtility.FromJson<PackageLocalData>(inventoryJson);
            
            // 将反序列化后的物品列表赋值给当前实例
            items = packageLocalData.items;
            
            // 返回物品列表
            return items;
        }
        else
        {
            // 第三层：无本地存储数据，创建新的空列表
            // 初始化空的物品列表，确保items不为null
            items = new List<PackageLocalItem>();
            
            // 返回空列表
            return items;
        }
    }
}



// ==================== 背包物品数据类 ====================

/// <summary>
/// 背包物品数据结构
/// 用于存储单个物品的本地数据信息
/// 标记为可序列化，支持JsonUtility进行JSON序列化/反序列化
/// </summary>
[System.Serializable]
public class PackageLocalItem
{
    /// <summary>
    /// 物品唯一标识符
    /// 通过System.Guid生成，保证每个物品实例在整个游戏中的唯一性
    /// 用于精确追踪和管理特定的物品实例
    /// </summary>
    public string uid;
    
    /// <summary>
    /// 物品配置ID
    /// 关联到PackageTable中的物品模板ID
    /// 通过此ID可以查询物品的基础配置（名称、图标、星级等）
    /// </summary>
    public int id;
    
    /// <summary>
    /// 物品数量
    /// 表示该物品实例的堆叠数量
    /// 可堆叠物品使用同一uid，num表示堆叠数
    /// </summary>
    public int num;
    
    /// <summary>
    /// 物品等级
    /// 表示该物品的强化等级（主要用于武器）
    /// 等级影响物品的属性和价值
    /// </summary>
    public int level;
    
    /// <summary>
    /// 是否为新获得的物品
    /// 用于UI界面显示"新"标记，提醒玩家有新物品
    /// 玩家查看物品后应设置为false
    /// </summary>
    public bool isNew;

    /// <summary>
    /// 重写的ToString方法
    /// 提供物品的简短描述字符串
    /// 主要用于调试和日志输出
    /// </summary>
    /// <returns>格式化的物品信息字符串</returns>
    public override string ToString()
    {
        // 返回格式：[id]:{物品ID} [num]:{数量}
        return string.Format("[id]:{0} [num]:{1}", id, num);
    }
}