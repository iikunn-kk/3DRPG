using UnityEngine;
using System.Threading.Tasks;
// using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System; // 新增: 允许在没有 SceneLoadManager 记录时回退获取当前激活场景

public class GameManager : Singleton<GameManager>
{
    public CharacterData CurrentCharacter { get; private set; }
    public PlayerCharacterStateDataSO playerCharacterStateDataSo;
    [SerializeField] private CharacterSelectDataSO characterSelectDataSo;
    [SerializeField] private ItemDataSO itemDataSo;
    public ItemDataSO ItemDataSo => itemDataSo;
    public PlayerLoginData PlayerLoginData { get; private set; }

    public MapManager currentMapManager { get; private set; }


    // 添加对PlayerCharacter的引用
    private CharacterState _currentCharacterState;
    public CharacterState CurrentPlayerCharacter()
    {
        if (_currentCharacterState)
        {
            return _currentCharacterState;
        }
        else
        {
            var data = GameObject.FindGameObjectWithTag("Player")?.GetComponent<CharacterState>();
            _currentCharacterState = data;
            return _currentCharacterState;
        }
    }

    [SerializeField] private PropertyScalingDataSO propertyScalingDataSo;
    public PropertyScalingDataSO PropertyScalingData => propertyScalingDataSo;

    // ========== 临时状态快照（跨场景保留，不持久化存档） ==========

    private List<CharacterBuffs.BuffSnapshot> _savedBuffs;
    private List<CharacterBuffs.VisualSnapshot> _savedBuffVisuals;



    //七月进行编写的变量
    private PackageTable packageTable;

    private void Start()
    {

        //---------------------------------
        // UIManager.Instance.OpenPanel(UIConst.MainPanel);
        //---------------------------   

        Application.targetFrameRate = 60;
    }




    //--------------------------------------------以下为七月的代码



    public void DeletePackageItems(List<string> uids)
    {
        foreach (string uid in uids)
        {
            DeletePackageItem(uid, false);
        }
        PackageLocalData.Instance.SavePackage();
    }

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

    public PackageTable GetPackageTable()
    {
        if (packageTable == null)
        {
            packageTable = Resources.Load<PackageTable>("TableData/PackageTable");
        }
        return packageTable;
    }

    // 1: 武器， 2：食物
    // 根据类型获取配置的表格数据
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

    // 随机抽卡，获得一件武器
    public PackageLocalItem GetLotteryRandom1()
    {
        List<PackageTableItem> packageItems = GetPackageTableByType(GameConst.PackageTypeWeapon);
        int index = UnityEngine.Random.Range(0, packageItems.Count);
        PackageTableItem packageItem = packageItems[index];
        PackageLocalItem packageLocalItem = new()
        {
            uid = System.Guid.NewGuid().ToString(),
            id = packageItem.id,
            num = 1,
            level = 1,
            isNew = CheckWeaponIsNew(packageItem.id),
        };
        PackageLocalData.Instance.items.Add(packageLocalItem);
        PackageLocalData.Instance.SavePackage();
        return packageLocalItem;
    }

    // 随机抽卡，获得十件武器
    public List<PackageLocalItem> GetLotteryRandom10(bool sort = false)
    {
        // 随机抽卡
        List<PackageLocalItem> packageLocalItems = new();
        for (int i = 0; i < 10; i++)
        {
            PackageLocalItem packageLocalItem = GetLotteryRandom1();
            packageLocalItems.Add(packageLocalItem);
        }
        // 武器排序
        if (sort)
        {
            packageLocalItems.Sort(new PackageItemComparer());
        }
        return packageLocalItems;
    }

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


    public List<PackageLocalItem> GetPackageLocalData()
    {
        return PackageLocalData.Instance.LoadPackage();
    }

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


    public List<PackageLocalItem> GetSortPackageLocalData()
    {
        List<PackageLocalItem> localItems = PackageLocalData.Instance.LoadPackage();
        localItems.Sort(new PackageItemComparer());
        return localItems;
    }




    //---------------------------------------------------------以上为七月




    public void SaveSceneTransitionPlayerState(CharacterState player)
    {
        if (player == null) { _savedBuffs = null; _savedBuffVisuals = null; return; }
        var buffs = player.GetComponent<CharacterBuffs>();
        if (buffs != null)
        {
            _savedBuffs = buffs.GetBuffSnapshots();
            _savedBuffVisuals = buffs.GetVisualSnapshots();
        }
        else
        {
            _savedBuffs = null; _savedBuffVisuals = null;
        }
    }

    public void RestoreTransientPlayerState(CharacterState player)
    {
        if (player == null) return;
        var buffs = player.GetComponent<CharacterBuffs>();
        if (buffs != null)
        {
            if (_savedBuffs != null) buffs.ApplyBuffSnapshots(_savedBuffs);
            if (_savedBuffVisuals != null) buffs.RestoreVisualSnapshots(_savedBuffVisuals);
        }
        _savedBuffs = null; _savedBuffVisuals = null; // 用完即清
    }

    public void UnsetPlayerInstance()
    {
        _currentCharacterState = null;
    }

    public void SetCurrentCharacterData(CharacterData character)
    {
        CurrentCharacter = character;
        EnsurePurchaseRecordLists(); // 旧存档兼容，补全列表
        // 初始化背包 & 货币（任务系统改为在 SpawnPlayer 后初始化，避免登录场景时写入空任务覆盖存档）
        InventoryManager.Instance.Initialize(character.Id);
        PlayerCurrencyManager.Instance.InitializeFromCharacterData(character);
        // 不再在这里调用 TaskManager.LoadTasksFromCharacterData / InitializeMainMissions / PopulateCharacterDataTasks
        // 避免因还未生成角色而将存档 taskList 清空
        _ = MongoDBManager.Instance.CreateAndSaveCharacterData(character); // 可选：记录最近选择，但不触碰任务列表
    }
    public void SetPlayerCharacter(CharacterState characterState)
    {
        _currentCharacterState = characterState;

    }
    public void SetMapManager(MapManager mapManager)
    {
        currentMapManager = mapManager;
    }


    /// <summary>
    /// 创建公会
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
    /// </summary>
    /// <param name="guildId">要加入的公会ID</param>
    /// <returns>加入公会操作是否成功</returns>
    public async Task<bool> JoinGuild(string guildId)
    {
        return await GuildManager.Instance.JoinGuild(guildId);
    }

    /// <summary>
    /// 退出公会
    /// </summary>
    /// <returns>退出公会操作是否成功</returns>
    public async Task<bool> QuitGuild()
    {
        return await GuildManager.Instance.QuitGuild();
    }

    private void OnEnable()
    {
        // 订阅任务事件（任务接取/完成 -> 保存角色 & 任务进度）
        TaskEvents.OnTaskStarted += OnTaskChanged;
        TaskEvents.OnTaskCompleted += OnTaskChanged;
        // 这里不再尝试立即保存任务，因为任务可能尚未初始化（等待玩家 Spawn）。
        if (TaskManager.Instance != null && TaskManager.Instance.tasks.Count > 0 && CurrentCharacter != null)
        {
            SaveCurrentCharacterData();
            TaskManager.Instance.SaveTaskProgressToMongoDB(CurrentCharacter.Id);
        }
    }
    private void OnDisable()
    {
        TaskEvents.OnTaskStarted -= OnTaskChanged;
        TaskEvents.OnTaskCompleted -= OnTaskChanged;
    }
    private void OnTaskChanged(int taskId)
    {
        SaveCurrentCharacterData();
        if (CurrentCharacter != null)
        {
            TaskManager.Instance?.SaveTaskProgressToMongoDB(CurrentCharacter.Id);
        }
    }
    /// <summary>
    /// 汇总当前运行时的所有角色数据并保存（统一入口）。
    /// 不再保存当前血量，强制写满血，保证下次进入始终满血。
    /// </summary>
    public async void SaveCurrentCharacterData()
    {
        try
        {
            if (CurrentCharacter == null) return;
            EnsurePurchaseRecordLists(); // 确保保存前列表存在
            var cs = CurrentPlayerCharacter();
            if (cs != null)
            {
                CurrentCharacter.exp = cs.Exp;
                CurrentCharacter.level = cs.Level;
                CurrentCharacter.position = cs.transform.position;
                // 注意：currentScene 的写入放到后面统一处理
                CurrentCharacter.hp = cs.MaxHealth; // 满血保存
            }
            // 货币
            if (PlayerCurrencyManager.Instance != null)
            {
                CurrentCharacter.gold = PlayerCurrencyManager.Instance.Money;
                CurrentCharacter.gem = PlayerCurrencyManager.Instance.Diamonds;
            }
            // 任务 & 技能（此时任务已经在玩家生成后初始化）
            TaskManager.Instance?.PopulateCharacterDataTasks(CurrentCharacter);
            SkillManager.Instance?.PopulateCharacterDataSkills(CurrentCharacter);

            // ===== 场景名安全写入逻辑 =====
            // 避免在 LoginScene / LoadingScene 或 SceneLoadManager 还未记录时把有效 gameplay 场景名覆盖为 null
            var safeSceneName = GetSafeGameplaySceneName();
            if (!string.IsNullOrEmpty(safeSceneName))
            {
                // 只有当获取到的 gameplay 场景名有效时才覆盖
                CurrentCharacter.currentScene = safeSceneName;
            }

            // 执行持久化
            await MongoDBManager.Instance.CreateAndSaveCharacterData(CurrentCharacter);
        }
        catch (Exception e)
        {
            // 这里不要继续 throw；避免在异步回调中产生未捕获异常导致逻辑中断
            Debug.LogError("[GameManager] SaveCurrentCharacterData 异常: " + e);
        }
    }

    /// <summary>
    /// 获取可安全写入存档的 gameplay 场景名：
    /// - 优先使用 SceneLoadManager.Instance.CurrentSceneName
    /// - 回退使用 SceneManager.GetActiveScene().name
    /// - 过滤 LoginScene / LoadingScene / 空
    /// </summary>
    private string GetSafeGameplaySceneName()
    {
        string name = null;
        var slm = SceneLoadManager.Instance;
        if (slm != null)
        {
            name = slm.CurrentSceneName;
        }
        if (string.IsNullOrEmpty(name))
        {
            // 回退：直接取当前激活场景
            name = SceneManager.GetActiveScene().name;
        }
        if (string.IsNullOrEmpty(name)) return null;
        // 过滤非 gameplay 场景（根据你项目中的实际命名可再扩展）
        if (name == "LoginScene" || name == "LoadingScene") return null;
        return name;
    }

    private void EnsurePurchaseRecordLists()
    {
        if (CurrentCharacter == null) return;
        CurrentCharacter.worldShopPurchases ??= new List<ShopPurchaseRecord>();
        CurrentCharacter.npcShopPurchases ??= new List<NpcShopPurchaseRecord>();
    }
}



//--------------------------------------------------以下为七月

public class PackageItemComparer : IComparer<PackageLocalItem>
{
    public int Compare(PackageLocalItem a, PackageLocalItem b)
    {
        PackageTableItem x = GameManager.Instance.GetPackageItemById(a.id);
        PackageTableItem y = GameManager.Instance.GetPackageItemById(b.id);
        // 首先按star从大到小排序
        int starComparison = y.star.CompareTo(x.star);

        // 如果star相同，则按id从大到小排序
        if (starComparison == 0)
        {
            int idComparison = y.id.CompareTo(x.id);
            if (idComparison == 0)
            {
                return b.level.CompareTo(a.level);
            }
            return idComparison;
        }

        return starComparison;
    }
}


//-------------------------------------------------------以上为七月的代码






#region //七月
public class GameConst
{
    // 武器类型
    public const int PackageTypeWeapon = 1;
    // 食物类型
    public const int PackageTypeFood = 2;
}

#endregion