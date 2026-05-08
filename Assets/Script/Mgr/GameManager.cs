using UnityEngine;
using System.Threading.Tasks;
// using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System;
using System.Linq; // 新增: 允许在没有 SceneLoadManager 记录时回退获取当前激活场景

/// <summary>
/// 游戏管理器 - 全局核心管理类
/// 采用单例模式设计，负责管理游戏中的全局状态和系统协调
/// 主要职责：
/// 1. 管理当前角色数据和状态
/// 2. 管理背包系统和抽卡逻辑
/// 3. 管理存档保存和加载
/// 4. 协调各子系统之间的通信
/// </summary>
public class GameManager : Singleton<GameManager>
{
    // ==================== 角色数据管理 ====================

    /// <summary>
    /// 当前选中的角色数据
    /// 在角色选择界面设置，包含角色的基础属性、等级、位置等信息
    /// private set限制外部只能通过SetCurrentCharacterData方法修改
    /// </summary>
    public CharacterData CurrentCharacter { get; private set; }

    /// <summary>
    /// 角色状态数据配置资源
    /// 包含角色在不同状态下的配置信息（如动画、属性等）
    /// 通过Unity编辑器 SerializeField 赋值
    /// </summary>
    public PlayerCharacterStateDataSO playerCharacterStateDataSo;

    /// <summary>
    /// 角色选择数据配置
    /// 包含所有可选角色的预制体和信息
    /// 通过Unity编辑器 SerializeField 赋值
    /// </summary>
    [SerializeField] private CharacterSelectDataSO characterSelectDataSo;

    /// <summary>
    /// 物品数据配置资源
    /// 包含所有物品的定义数据（消耗品、装备、材料等）
    /// 提供公共访问属性 ItemDataSo 供其他系统查询物品信息
    /// </summary>
    [SerializeField] private ItemDataSO itemDataSo;
    public ItemDataSO ItemDataSo => itemDataSo;

    /// <summary>
    /// 玩家登录数据
    /// 记录玩家的登录信息、会话状态等
    /// </summary>
    public PlayerLoginData PlayerLoginData { get; private set; }

    // ==================== 地图管理 ====================

    /// <summary>
    /// 当前地图管理器引用
    /// 记录当前场景的地图管理组件，负责场景初始化、出生点管理等
    /// </summary>
    public MapManager currentMapManager { get; private set; }

    // ==================== 角色实例引用 ====================

    /// <summary>
    /// 当前玩家角色实例的引用
    /// 指向场景中实际生成的玩家角色对象（CharacterState组件）
    /// 用于快速访问玩家角色进行状态查询和操作
    /// </summary>
    private CharacterState _currentCharacterState;

    /// <summary>
    /// 获取当前玩家角色实例
    /// 提供线程安全的玩家角色访问，如果缓存不存在则自动查找
    /// </summary>
    /// <returns>当前玩家角色的CharacterState组件引用，如果未找到则返回null</returns>
    public CharacterState CurrentPlayerCharacter()
    {
        // 如果缓存存在，直接返回缓存的角色引用
        if (_currentCharacterState)
        {
            return _currentCharacterState;
        }
        else
        {
            // 缓存不存在，通过Tag查找场景中的玩家对象并获取组件
            var data = GameObject.FindGameObjectWithTag("Player")?.GetComponent<CharacterState>();
            // 更新缓存
            _currentCharacterState = data;
            return _currentCharacterState;
        }
    }

    // ==================== 属性缩放配置 ====================

    /// <summary>
    /// 属性缩放数据配置资源
    /// 定义角色属性随等级提升的缩放规则
    /// 用于装备生成、属性计算等系统
    /// </summary>
    [SerializeField] private PropertyScalingDataSO propertyScalingDataSo;
    public PropertyScalingDataSO PropertyScalingData => propertyScalingDataSo;

    // ==================== 临时状态快照（跨场景保留） ====================
    /// <summary>
    /// 临时状态快照列表 - 跨场景保留，不持久化存档
    /// 用于在场景切换时保存角色的Buff状态（如增益效果、持续伤害等）
    /// </summary>
    private List<CharacterBuffs.BuffSnapshot> _savedBuffs;

    /// <summary>
    /// Buff视觉效果快照列表
    /// 保存Buff对应的视觉表现（如特效、粒子等）
    /// </summary>
    private List<CharacterBuffs.VisualSnapshot> _savedBuffVisuals;

    // ==================== 七月编写的背包系统变量 ====================

    /// <summary>
    /// 背包表格数据缓存
    /// 从Resources加载的背包配置表，包含物品类型、权重等信息
    /// 使用延迟加载模式，首次访问时加载
    /// </summary>
    private PackageTable packageTable;

    /// <summary>
    /// Start方法 - 在游戏开始时调用
    /// 执行游戏初始化设置
    /// </summary>
    private void Start()
    {
        //---------------------------------
        // UIManager.Instance.OpenPanel(UIConst.MainPanel);
        //---------------------------   

        // 设置游戏目标帧率为60FPS，确保游戏运行流畅度一致
        Application.targetFrameRate = 60;
    }

    // ==================== 七月编写的背包系统方法 ====================

    /// <summary>
    /// 删除多个背包物品
    /// 批量删除指定UID列表中的所有物品
    /// </summary>
    /// <param name="uids">要删除的物品唯一标识符列表</param>
    public void DeletePackageItems(List<string> uids)
    {
        // 遍历UID列表，逐个删除物品
        foreach (string uid in uids)
        {
            // 调用单删方法，false表示暂不保存（等全部删除后再统一保存）
            DeletePackageItem(uid, false);
        }
        // 批量删除完成后统一保存背包数据
        PackageLocalData.Instance.SavePackage();
    }

    /// <summary>
    /// 删除单个背包物品
    /// 根据UID删除指定物品，可选择是否立即保存
    /// </summary>
    /// <param name="uid">要删除的物品唯一标识符</param>
    /// <param name="needSave">是否立即保存，true则立即写入存储，false则延迟保存</param>
    public void DeletePackageItem(string uid, bool needSave = true)
    {
        // 通过UID查找背包中的物品
        PackageLocalItem packageLocalItem = GetPackageLocalItemByUId(uid);

        // 如果物品不存在，直接返回
        if (packageLocalItem == null)
            return;

        // 从背包数据列表中移除该物品
        PackageLocalData.Instance.items.Remove(packageLocalItem);

        // 如果需要立即保存
        if (needSave)
        {
            // 将更新后的背包数据写入持久化存储
            PackageLocalData.Instance.SavePackage();
        }
    }

    /// <summary>
    /// 获取背包表格数据
    /// 采用延迟加载模式，首次访问时从Resources加载
    /// </summary>
    /// <returns>背包表格数据资源对象</returns>
    public PackageTable GetPackageTable()
    {
        // 检查缓存是否为空
        if (packageTable == null)
        {
            // 从Resources目录加载背包表格数据
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
    /// <example>
    /// // 获取所有武器配置
    /// List<PackageTableItem> weapons = GetPackageTableByType(GameConst.PackageTypeWeapon);
    /// </example>
    public List<PackageTableItem> GetPackageTableByType(int type)
    {
        // 创建返回列表
        List<PackageTableItem> packageItems = new List<PackageTableItem>();

        // 遍历背包表格中的所有物品
        foreach (PackageTableItem packageItem in GetPackageTable().DataList)
        {
            // 检查物品类型是否匹配
            if (packageItem.type == type)
            {
                // 类型匹配，添加到结果列表
                packageItems.Add(packageItem);
            }
        }
        return packageItems;
    }

    // /// <summary>
    // /// 随机抽卡 - 单抽
    // /// 从武器池中随机抽取一件武器并添加到背包
    // /// </summary>
    // /// <returns>新抽取的物品本地数据对象</returns>
    // public PackageLocalItem GetLotteryRandom1()
    // {
    //     // 获取所有武器配置列表
    //     List<PackageTableItem> packageItems = GetPackageTableByType(GameConst.PackageTypeWeapon);

    //     // 生成随机索引，从武器列表中随机选择一件
    //     int index = UnityEngine.Random.Range(0, packageItems.Count);
    //     PackageTableItem packageItem = packageItems[index];

    //     // 创建背包物品本地数据对象
    //     PackageLocalItem packageLocalItem = new()
    //     {
    //         // 生成唯一标识符
    //         uid = System.Guid.NewGuid().ToString(),
    //         // 物品ID关联到表格配置
    //         id = packageItem.id,
    //         // 初始数量为1
    //         num = 1,
    //         // 初始等级为1
    //         level = 1,
    //         // 检查是否是新获得的武器（用于显示"新"标记）
    //         isNew = CheckWeaponIsNew(packageItem.id),
    //     };

    //     // 将新物品添加到背包数据列表
    //     PackageLocalData.Instance.items.Add(packageLocalItem);

    //     // 保存更新后的背包数据
    //     PackageLocalData.Instance.SavePackage();

    //     // 返回新创建的物品数据
    //     return packageLocalItem;
    // }

    /// <summary>
    /// 随机抽卡 - 单抽
    /// 从武器池中随机抽取一件武器并添加到主背包
    /// </summary>
    public InventoryItem GetLotteryRandom1()
    {
        // 获取所有武器配置列表
        List<PackageTableItem> packageItems = GetPackageTableByType(GameConst.PackageTypeWeapon);

        // 生成随机索引，从武器列表中随机选择一件
        int index = UnityEngine.Random.Range(0, packageItems.Count);
        PackageTableItem packageItem = packageItems[index];

        // 创建 InventoryItem
        var newItem = new InventoryItem(packageItem.id)
        {
            count = 1
        };

        // 将星级转换为品质
        ItemQuality quality = ConvertStarToQuality(packageItem.star);
        newItem.quantity = quality;

        // 获取对应的装备数据
        var equipmentData = ItemDataSo.GetEquipmentDataById(packageItem.id);
        if (equipmentData != null && equipmentData.isRandomlyAttributes)
        {
            // 临时修改品质以便生成属性
            var originalQuality = equipmentData.quantity;
            equipmentData.quantity = quality;
            equipmentData.GenerateBaseProperties(PropertyScalingData);

            // 深拷贝生成的属性
            newItem.generatedProperties = equipmentData.GetAllProperties()
                .Select(p => p.DeepClone())
                .ToList();

            // 恢复原始品质
            equipmentData.quantity = originalQuality;
        }


        // 直接添加到 InventoryManager (不会显示Toast)
        bool success = InventoryManager.Instance.AddItemWithoutToast(packageItem.id, 1, newItem);

        return success ? newItem : null;
    }

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



    // /// <summary>
    // /// 随机抽卡 - 十连抽
    // /// 执行十次单抽逻辑，返回包含10件物品的列表
    // /// </summary>
    // /// <param name="sort">是否对结果排序，true则按星级和ID排序</param>
    // /// <returns>包含10件随机物品的列表</returns>
    // public List<PackageLocalItem> GetLotteryRandom10(bool sort = false)
    // {
    //     // 创建存放抽卡结果的列表
    //     List<PackageLocalItem> packageLocalItems = new();

    //     // 执行十次单抽
    //     for (int i = 0; i < 10; i++)
    //     {
    //         // 调用单抽方法获取一件随机物品
    //         PackageLocalItem packageLocalItem = GetLotteryRandom1();
    //         // 添加到结果列表
    //         packageLocalItems.Add(packageLocalItem);
    //     }

    //     // 如果需要排序
    //     if (sort)
    //     {
    //         // 使用自定义比较器进行排序
    //         // 排序规则：先按星级降序，再按ID降序，最后按等级降序
    //         packageLocalItems.Sort(new PackageItemComparer());
    //     }

    //     return packageLocalItems;
    // }



    /// <summary>
    /// 随机抽卡 - 十连抽
    /// 执行十次单抽逻辑，返回包含10个物品的列表
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

        // 如果需要排序
        if (sort && items.Count > 0)
        {
            items = items.OrderByDescending(x => x.quantity)
                         .ThenBy(x => x.itemId)
                         .ToList();
        }

        return items;
    }





    /// <summary>
    /// 检查武器是否为新获得
    /// 通过ID查询背包中是否已存在该武器
    /// </summary>
    /// <param name="id">武器ID</param>
    /// <returns>true表示是新武器（背包中不存在），false表示已拥有</returns>
    public bool CheckWeaponIsNew(int id)
    {
        // 获取当前背包所有物品
        foreach (PackageLocalItem packageLocalItem in GetPackageLocalData())
        {
            // 检查物品ID是否匹配
            if (packageLocalItem.id == id)
            {
                // 背包中已存在该武器，返回false
                return false;
            }
        }
        // 遍历完成未找到相同ID，返回true表示是新武器
        return true;
    }

    /// <summary>
    /// 获取背包本地数据
    /// 从本地存储加载玩家的背包物品数据
    /// </summary>
    /// <returns>背包物品列表</returns>
    public List<PackageLocalItem> GetPackageLocalData()
    {
        return PackageLocalData.Instance.LoadPackage();
    }

    /// <summary>
    /// 根据ID获取物品表格配置
    /// 通过物品ID在表格中查找对应的配置数据
    /// </summary>
    /// <param name="id">物品ID</param>
    /// <returns>对应的物品表格配置，如果未找到返回null</returns>
    public PackageTableItem GetPackageItemById(int id)
    {
        // 获取背包表格数据列表
        List<PackageTableItem> packageDataList = GetPackageTable().DataList;

        // 遍历查找匹配的ID
        foreach (PackageTableItem item in packageDataList)
        {
            if (item.id == id)
            {
                return item;
            }
        }
        // 未找到匹配项，返回null
        return null;
    }

    /// <summary>
    /// 根据UID获取背包物品本地数据
    /// 通过唯一标识符精确查找背包中的物品实例
    /// </summary>
    /// <param name="uid">物品唯一标识符</param>
    /// <returns>对应的物品本地数据，如果未找到返回null</returns>
    public PackageLocalItem GetPackageLocalItemByUId(string uid)
    {
        // 获取背包所有物品
        List<PackageLocalItem> packageDataList = GetPackageLocalData();

        // 遍历查找匹配的UID
        foreach (PackageLocalItem item in packageDataList)
        {
            if (item.uid == uid)
            {
                return item;
            }
        }
        // 未找到匹配项，返回null
        return null;
    }

    /// <summary>
    /// 获取排序后的背包数据
    /// 加载背包数据并按自定义规则排序后返回
    /// </summary>
    /// <returns>排序后的背包物品列表</returns>
    public List<PackageLocalItem> GetSortPackageLocalData()
    {
        // 加载背包数据
        List<PackageLocalItem> localItems = PackageLocalData.Instance.LoadPackage();

        // 使用比较器排序
        localItems.Sort(new PackageItemComparer());

        return localItems;
    }

    // ==================== 角色状态管理（跨场景） ====================

    /// <summary>
    /// 保存场景切换时的角色状态
    /// 在离开场景前调用，保存角色的Buff快照
    /// </summary>
    /// <param name="player">玩家角色对象引用，如果为null则清空保存的状态</param>
    public void SaveSceneTransitionPlayerState(CharacterState player)
    {
        // 如果角色为null，清空所有保存的状态
        if (player == null) { _savedBuffs = null; _savedBuffVisuals = null; return; }

        // 获取角色身上的Buff组件
        var buffs = player.GetComponent<CharacterBuffs>();
        if (buffs != null)
        {
            // 保存Buff快照列表
            _savedBuffs = buffs.GetBuffSnapshots();
            // 保存Buff视觉效果快照
            _savedBuffVisuals = buffs.GetVisualSnapshots();
        }
        else
        {
            // 角色没有Buff组件，清空保存
            _savedBuffs = null; _savedBuffVisuals = null;
        }
    }

    /// <summary>
    /// 恢复角色的临时状态
    /// 在进入新场景后调用，恢复之前保存的Buff状态
    /// </summary>
    /// <param name="player">玩家角色对象引用</param>
    public void RestoreTransientPlayerState(CharacterState player)
    {
        // 参数检查
        if (player == null) return;

        // 获取角色身上的Buff组件
        var buffs = player.GetComponent<CharacterBuffs>();
        if (buffs != null)
        {
            // 恢复Buff快照
            if (_savedBuffs != null) buffs.ApplyBuffSnapshots(_savedBuffs);
            // 恢复视觉效果
            if (_savedBuffVisuals != null) buffs.RestoreVisualSnapshots(_savedBuffVisuals);
        }
        // 恢复完成后清空缓存，释放内存
        _savedBuffs = null; _savedBuffVisuals = null;
    }

    /// <summary>
    /// 清除玩家实例引用
    /// 用于角色切换或销毁时清理引用
    /// </summary>
    public void UnsetPlayerInstance()
    {
        _currentCharacterState = null;
    }

    /// <summary>
    /// 设置当前角色数据
    /// 在角色选择后调用，初始化角色相关的各个系统
    /// </summary>
    /// <param name="character">角色数据对象</param>
    public void SetCurrentCharacterData(CharacterData character)
    {
        // 保存角色数据引用
        CurrentCharacter = character;

        // 确保购买记录列表存在（兼容旧存档）
        EnsurePurchaseRecordLists();

        // 初始化背包系统
        InventoryManager.Instance.Initialize(character.Id);

        // 从角色数据初始化货币系统
        PlayerCurrencyManager.Instance.InitializeFromCharacterData(character);

        // 可选：异步创建并保存角色数据到MongoDB
        _ = MongoDBManager.Instance.CreateAndSaveCharacterData(character);
    }

    /// <summary>
    /// 设置当前玩家角色实例
    /// 在场景中生成玩家后调用，建立GameManager与角色实例的关联
    /// </summary>
    /// <param name="characterState">玩家角色的CharacterState组件引用</param>
    public void SetPlayerCharacter(CharacterState characterState)
    {
        _currentCharacterState = characterState;
    }

    /// <summary>
    /// 设置当前地图管理器
    /// 在场景加载完成后调用，建立GameManager与MapManager的关联
    /// </summary>
    /// <param name="mapManager">当前场景的MapManager组件引用</param>
    public void SetMapManager(MapManager mapManager)
    {
        currentMapManager = mapManager;
    }

    // ==================== 公会系统接口 ====================

    /// <summary>
    /// 创建公会
    /// 异步调用GuildManager创建新公会
    /// </summary>
    /// <param name="guildName">公会名称</param>
    /// <param name="guildDescription">公会描述</param>
    /// <returns>创建公会操作是否成功</returns>
    public async Task<bool> CreateGuild(string guildName, string guildDescription)
    {
        return await GuildManager.Instance.CreateGuild(guildName, guildDescription);
    }

    /// <summary>
    /// 加入公会
    /// 异步调用GuildManager加入指定公会
    /// </summary>
    /// <param name="guildId">要加入的公会ID</param>
    /// <returns>加入公会操作是否成功</returns>
    public async Task<bool> JoinGuild(string guildId)
    {
        return await GuildManager.Instance.JoinGuild(guildId);
    }

    /// <summary>
    /// 退出公会
    /// 异步调用GuildManager退出当前公会
    /// </summary>
    /// <returns>退出公会操作是否成功</returns>
    public async Task<bool> QuitGuild()
    {
        return await GuildManager.Instance.QuitGuild();
    }

    // ==================== 事件订阅与回调 ====================

    /// <summary>
    /// 启用时调用 - 订阅任务事件
    /// </summary>
    private void OnEnable()
    {
        // 订阅任务开始事件
        TaskEvents.OnTaskStarted += OnTaskChanged;

        // 订阅任务完成事件
        TaskEvents.OnTaskStarted += OnTaskChanged;

        // 检查并保存已初始化的任务数据
        if (TaskManager.Instance != null && TaskManager.Instance.tasks.Count > 0 && CurrentCharacter != null)
        {
            // 保存当前角色数据
            SaveCurrentCharacterData();
            // 保存任务进度到MongoDB
            TaskManager.Instance.SaveTaskProgressToMongoDB(CurrentCharacter.Id);
        }
    }

    /// <summary>
    /// 禁用时调用 - 取消订阅任务事件
    /// </summary>
    private void OnDisable()
    {
        // 取消订阅任务开始事件
        TaskEvents.OnTaskStarted -= OnTaskChanged;

        // 取消订阅任务完成事件
        TaskEvents.OnTaskCompleted -= OnTaskChanged;
    }

    /// <summary>
    /// 任务状态变更回调
    /// 当任务开始或完成时自动保存角色数据
    /// </summary>
    /// <param name="taskId">变更的任务ID</param>
    private void OnTaskChanged(int taskId)
    {
        // 保存当前角色数据
        SaveCurrentCharacterData();

        // 如果存在角色，保存任务进度到MongoDB
        if (CurrentCharacter != null)
        {
            TaskManager.Instance?.SaveTaskProgressToMongoDB(CurrentCharacter.Id);
        }
    }

    // ==================== 数据保存系统 ====================

    /// <summary>
    /// 汇总当前运行时的所有角色数据并保存（统一入口）
    /// 异步将角色数据保存到MongoDB持久化存储
    /// 注意：不再保存当前血量，强制写满血，保证下次进入始终满血
    /// </summary>
    public async void SaveCurrentCharacterData()
    {
        try
        {
            // 检查角色数据是否存在
            if (CurrentCharacter == null) return;

            // 确保购买记录列表存在
            EnsurePurchaseRecordLists();

            // 获取当前玩家角色实例
            var cs = CurrentPlayerCharacter();
            if (cs != null)
            {
                // 更新角色经验值
                CurrentCharacter.exp = cs.Exp;

                // 更新角色等级
                CurrentCharacter.level = cs.Level;

                // 更新角色位置
                CurrentCharacter.position = cs.transform.position;

                // 【重要】满血保存，下次进入游戏时角色满血
                CurrentCharacter.hp = cs.MaxHealth;
            }

            // 更新货币数据
            if (PlayerCurrencyManager.Instance != null)
            {
                // 更新金币数量
                CurrentCharacter.gold = PlayerCurrencyManager.Instance.Money;

                // 更新钻石数量
                CurrentCharacter.gem = PlayerCurrencyManager.Instance.Diamonds;
            }

            // 更新任务数据到角色数据
            TaskManager.Instance?.PopulateCharacterDataTasks(CurrentCharacter);

            // 更新技能数据到角色数据
            SkillManager.Instance?.PopulateCharacterDataSkills(CurrentCharacter);

            // ===== 场景名安全写入逻辑 =====
            // 获取安全的场景名称（避免在LoginScene/LoadingScene写入无效场景名）
            var safeSceneName = GetSafeGameplaySceneName();
            if (!string.IsNullOrEmpty(safeSceneName))
            {
                // 只有当获取到的游戏场景名有效时才更新
                CurrentCharacter.currentScene = safeSceneName;
            }

            // 执行异步持久化保存到MongoDB
            await MongoDBManager.Instance.CreateAndSaveCharacterData(CurrentCharacter);
        }
        catch (Exception e)
        {
            // 捕获异常，避免异步回调中的未处理异常导致逻辑中断
            Debug.LogError("[GameManager] SaveCurrentCharacterData 异常: " + e);
        }
    }

    /// <summary>
    /// 获取可安全写入存档的游戏场景名称
    /// 优先使用SceneLoadManager记录的场景名，回退使用当前激活场景
    /// 过滤掉LoginScene和LoadingScene等非游戏场景
    /// </summary>
    /// <returns>安全的游戏场景名称，如果无效返回null</returns>
    private string GetSafeGameplaySceneName()
    {
        string name = null;

        // 尝试从SceneLoadManager获取场景名
        var slm = SceneLoadManager.Instance;
        if (slm != null)
        {
            name = slm.CurrentSceneName;
        }

        // 如果获取失败，回退到直接获取当前激活场景名
        if (string.IsNullOrEmpty(name))
        {
            name = SceneManager.GetActiveScene().name;
        }

        // 仍然为空，返回null
        if (string.IsNullOrEmpty(name)) return null;

        // 过滤非游戏场景
        if (name == "LoginScene" || name == "LoadingScene") return null;

        return name;
    }

    /// <summary>
    /// 确保购买记录列表存在
    /// 用于兼容旧存档，如果记录列表为null则初始化空列表
    /// </summary>
    private void EnsurePurchaseRecordLists()
    {
        if (CurrentCharacter == null) return;

        // 如果世界商店购买记录为空，初始化空列表
        CurrentCharacter.worldShopPurchases ??= new List<ShopPurchaseRecord>();

        // 如果NPC商店购买记录为空，初始化空列表
        CurrentCharacter.npcShopPurchases ??= new List<NpcShopPurchaseRecord>();
    }
}



// ==================== 七月编写的背包物品排序比较器 ====================

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
        PackageTableItem x = GameManager.Instance.GetPackageItemById(a.id);
        PackageTableItem y = GameManager.Instance.GetPackageItemById(b.id);

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



// ==================== 七月编写的游戏常量定义 ====================

/// <summary>
/// 游戏常量定义类
/// 集中管理游戏中的常量值，便于维护和修改
/// </summary>
public class GameConst
{
    /// <summary>
    /// 背包物品类型常量 - 武器
    /// 用于抽卡系统和物品分类
    /// </summary>
    public const int PackageTypeWeapon = 1;

    /// <summary>
    /// 背包物品类型常量 - 食物
    /// 用于抽卡系统和物品分类
    /// </summary>
    public const int PackageTypeFood = 2;
}


