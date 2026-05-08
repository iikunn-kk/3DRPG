using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包面板模式枚举
/// 定义背包界面的三种工作模式
/// </summary>
public enum PackageMode
{
    /// <summary>
    /// 普通模式 - 默认模式，可以查看物品和详情
    /// </summary>
    normal,
    
    /// <summary>
    /// 删除模式 - 用于选择并删除多个物品
    /// </summary>
    delete,
    
    /// <summary>
    /// 排序模式 - 用于对物品进行排序显示
    /// </summary>
    sort,
}


// public class PackagePanel : BasePanel
/// <summary>
/// 背包面板类 - 管理背包界面的核心逻辑
/// 继承自UIPopPanelBase，实现背包的显示、选择、删除等功能
/// 主要职责：
/// 1. 背包物品的显示和刷新
/// 2. 物品详情查看
/// 3. 物品删除功能
/// 4. 背包分类筛选（武器、食物）
/// </summary>
public class PackagePanel : UIPopPanelBase
{
    // ==================== UI组件引用 ====================
    
    /// <summary>
    /// 顶部菜单栏的Transform组件
    /// 包含武器、食物等分类按钮
    /// </summary>
    private Transform UIMenu;
    
    /// <summary>
    /// 武器分类按钮的Transform组件
    /// 点击后筛选显示所有武器
    /// </summary>
    private Transform UIMenuWeapon;
    
    /// <summary>
    /// 食物分类按钮的Transform组件
    /// 点击后筛选显示所有食物
    /// </summary>
    private Transform UIMenuFood;
    
    /// <summary>
    /// 左侧标签名称的Transform组件
    /// 显示当前面板名称或分类名称
    /// </summary>
    private Transform UITabName;
    
    /// <summary>
    /// 关闭按钮的Transform组件
    /// 点击后关闭背包面板
    /// </summary>
    private Transform UICloseBtn;
    
    /// <summary>
    /// 中央区域的Transform组件
    /// 包含滚动视图和详情面板
    /// </summary>
    private Transform UICenter;
    
    /// <summary>
    /// 滚动视图的Transform组件
    /// 用于显示物品列表，支持滚动浏览
    /// </summary>
    private Transform UIScrollView;
    
    /// <summary>
    /// 详情面板的Transform组件
    /// 显示选中物品的详细信息
    /// </summary>
    private Transform UIDetailPanel;
    
    /// <summary>
    /// 左侧按钮的Transform组件
    /// 用于翻页或切换到上一个物品
    /// </summary>
    private Transform UILeftBtn;
    
    /// <summary>
    /// 右侧按钮的Transform组件
    /// 用于翻页或切换到下一个物品
    /// </summary>
    private Transform UIRightBtn;
    
    /// <summary>
    /// 删除面板的Transform组件
    /// 在删除模式下显示，包含删除操作按钮
    /// </summary>
    private Transform UIDeletePanel;
    
    /// <summary>
    /// 删除面板的返回按钮
    /// 退出删除模式，返回普通模式
    /// </summary>
    private Transform UIDeleteBackBtn;
    
    /// <summary>
    /// 删除面板的提示文本
    /// 显示当前选中了多少个物品
    /// </summary>
    private Transform UIDeleteInfoText;
    
    /// <summary>
    /// 删除面板的确认删除按钮
    /// 确认执行删除操作
    /// </summary>
    private Transform UIDeleteConfirmBtn;
    
    /// <summary>
    /// 底部菜单栏的Transform组件
    /// 包含删除按钮、详情按钮等
    /// </summary>
    private Transform UIBottomMenus;
    
    /// <summary>
    /// 删除按钮的Transform组件
    /// 点击后进入删除模式
    /// </summary>
    private Transform UIDeleteBtn;
    
    /// <summary>
    /// 详情按钮的Transform组件
    /// 点击后查看选中物品的详情
    /// </summary>
    private Transform UIDetailBtn;

    // ==================== 预制体引用 ====================
    
    /// <summary>
    /// 背包物品单元格的预制体引用
    /// 用于动态实例化生成物品显示单元格
    /// 需要在Inspector中绑定 PackageUIItem 预制体
    /// </summary>
    public GameObject PackageUIItemPrefab;

    // ==================== 状态管理 ====================
    
    /// <summary>
    /// 当前背包面板的工作模式
    /// 通过PackageMode枚举定义：普通模式、删除模式、排序模式
    /// 用于控制界面交互逻辑和显示状态
    /// </summary>
    public PackageMode curMode = PackageMode.normal;
    
    /// <summary>
    /// 删除模式下选中的物品UID列表
    /// 存储用户选择要删除的所有物品的唯一标识符
    /// </summary>
    public List<string> deleteChooseUid;

    // ==================== 选中物品管理 ====================
    
    /// <summary>
    /// 当前选中的物品UID私有字段
    /// 存储当前用户选择查看详情的物品
    /// </summary>
    private string _chooseUid;
    
    /// <summary>
    /// 选中物品UID的公共属性
    /// 提供对私有字段的访问，并在赋值时自动刷新详情面板
    /// </summary>
    public string chooseUID
    {
        get
        {
            // 返回当前选中的物品UID
            return _chooseUid;
        }
        set
        {
            // 设置新的选中UID
            _chooseUid = value;
            // 赋值后自动刷新详情面板显示
            RefreshDetail();
        }
    }

    // ==================== 删除相关方法 ====================

    /// <summary>
    /// 添加或移除删除选中项
    /// 在删除模式下，点击物品时调用，用于切换物品的选中状态
    /// 如果物品已在列表中则移除，否则添加
    /// </summary>
    /// <param name="uid">要切换选中状态的物品UID</param>
    public void AddChooseDeleteUid(string uid)
    {
        // 延迟初始化：确保列表不为null
        this.deleteChooseUid ??= new List<string>();
        
        // 检查该UID是否已在选中列表中
        if (!this.deleteChooseUid.Contains(uid))
        {
            // 不存在：添加到选中列表
            this.deleteChooseUid.Add(uid);
        }
        else
        {
            // 已存在：从选中列表移除（实现切换效果）
            this.deleteChooseUid.Remove(uid);
        }
        
        // 更新删除面板的显示状态
        RefreshDeletePanel();
    }

    /// <summary>
    /// 刷新删除面板的选中状态
    /// 遍历所有物品单元格，更新它们的选中/未选中显示状态
    /// </summary>
    private void RefreshDeletePanel()
    {
        // 获取ScrollView的内容RectTransform
        RectTransform scrollContent = UIScrollView.GetComponent<ScrollRect>().content;
        
        // 遍历所有物品单元格
        foreach (Transform cell in scrollContent)
        {
            // 获取单元格上的PackageCell组件
            PackageCell packageCell = cell.GetComponent<PackageCell>();
            
            // 调用单元格的刷新方法，更新删除选中状态
            packageCell.RefreshDeleteState();
        }
    }

    // ==================== 生命周期方法 ====================

    /// <summary>
    /// Awake方法 - 在对象创建时调用
    /// 执行基类初始化和UI初始化
    /// </summary>
    override protected void Awake()
    {
        // 调用基类的Awake方法，确保父类的初始化逻辑正常执行
        base.Awake();
        
        // 初始化UI组件引用
        InitUI();
    }

    /// <summary>
    /// Start方法 - 在第一帧更新前调用
    /// 面板打开时刷新显示内容
    /// </summary>
    private void Start()
    {
        // 刷新整个背包UI显示
        RefreshUI();
    }

    // ==================== 初始化方法 ====================

    /// <summary>
    /// 初始化UI组件引用
    /// 调用子初始化方法完成UI绑定和事件注册
    /// </summary>
    private void InitUI()
    {
        // 初始化UI元素名称引用
        InitUIName();
        
        // 初始化点击事件监听
        InitClick();
    }

    /// <summary>
    /// 刷新UI显示内容
    /// 提供公共刷新入口，供外部调用
    /// </summary>
    private void RefreshUI()
    {
        // 刷新滚动视图中的物品列表
        RefreshScroll();
    }

    /// <summary>
    /// 刷新物品详情面板
    /// 根据当前选中的物品UID加载并显示详情信息
    /// </summary>
    private void RefreshDetail()
    {
        // 根据UID查找对应的物品本地数据
        PackageLocalItem localItem = GameManager.Instance.GetPackageLocalItemByUId(chooseUID);
        
        // 获取详情面板组件并刷新显示
        UIDetailPanel.GetComponent<PackageDetail>().Refresh(localItem, this);
    }

    /// <summary>
    /// 刷新滚动视图中的物品列表
    /// 清空原有物品，重新从GameManager加载并实例化显示
    /// </summary>
    private void RefreshScroll()
    {
        // 获取滚动视图的内容区域
        RectTransform scrollContent = UIScrollView.GetComponent<ScrollRect>().content;
        
        // 清空原有物品：销毁所有子物体
        for (int i = 0; i < scrollContent.childCount; i++)
        {
            Destroy(scrollContent.GetChild(i).gameObject);
        }
        
        // 从GameManager获取排序后的背包物品列表
        foreach (PackageLocalItem localData in GameManager.Instance.GetSortPackageLocalData())
        {
            // 实例化物品预制体
            Transform PackageUIItem = Instantiate(PackageUIItemPrefab.transform, scrollContent) as Transform;
            
            // 获取PackageCell组件
            PackageCell packageCell = PackageUIItem.GetComponent<PackageCell>();
            
            // 刷新单元格显示
            packageCell.Refresh(localData, this);
        }
    }

    /// <summary>
    /// 初始化UI元素名称引用
    /// 通过路径查找各个UI组件并绑定到对应变量
    /// </summary>
    private void InitUIName()
    {
        // ==================== 顶部区域 ====================
        // 顶部中央菜单栏
        UIMenu = transform.Find("TopCenter/Menu");
        
        // 菜单下的武器按钮
        UIMenuWeapon = transform.Find("TopCenter/Menus/Weapon");
        
        // 菜单下的食物按钮
        UIMenuFood = transform.Find("TopCenter/Menus/Food");
        
        // 左侧顶部标签名称
        UITabName = transform.Find("LeftTop/TabName");
        
        // 右侧顶部关闭按钮
        UICloseBtn = transform.Find("RightTop/Close");
        
        // ==================== 中央区域 ====================
        // 中央区域容器
        UICenter = transform.Find("Center");
        
        // 滚动视图
        UIScrollView = transform.Find("Center/Scroll View");
        
        // 详情面板
        UIDetailPanel = transform.Find("Center/DetailPanel");
        
        // ==================== 左右翻页按钮 ====================
        // 左侧按钮
        UILeftBtn = transform.Find("Left/Button");
        
        // 右侧按钮
        UIRightBtn = transform.Find("Right/Button");
        
        // ==================== 删除面板 ====================
        // 删除面板容器
        UIDeletePanel = transform.Find("Bottom/DeletePanel");
        
        // 删除面板返回按钮
        UIDeleteBackBtn = transform.Find("Bottom/DeletePanel/Back");
        
        // 删除面板信息文本
        UIDeleteInfoText = transform.Find("Bottom/DeletePanel/InfoText");
        
        // 删除面板确认按钮
        UIDeleteConfirmBtn = transform.Find("Bottom/DeletePanel/ConfirmBtn");
        
        // ==================== 底部菜单 ====================
        // 底部菜单容器
        UIBottomMenus = transform.Find("Bottom/BottomMenus");
        
        // 底部删除按钮
        UIDeleteBtn = transform.Find("Bottom/BottomMenus/DeleteBtn");
        
        // 底部详情按钮
        UIDetailBtn = transform.Find("Bottom/BottomMenus/DetailBtn");

        // ==================== 初始状态设置 ====================
        // 默认隐藏删除面板
        UIDeletePanel.gameObject.SetActive(false);
        
        // 默认显示底部菜单
        UIBottomMenus.gameObject.SetActive(true);
    }

    /// <summary>
    /// 初始化点击事件监听
    /// 为各个按钮添加点击事件回调
    /// </summary>
    private void InitClick()
    {
        // ==================== 顶部菜单按钮 ====================
        // 武器分类按钮点击事件
        UIMenuWeapon.GetComponent<Button>().onClick.AddListener(OnClickWeapon);
        
        // 食物分类按钮点击事件
        UIMenuFood.GetComponent<Button>().onClick.AddListener(OnClickFood);
        
        // 关闭按钮点击事件
        UICloseBtn.GetComponent<Button>().onClick.AddListener(OnClickClose);
        
        // ==================== 翻页按钮 ====================
        // 左侧按钮点击事件
        UILeftBtn.GetComponent<Button>().onClick.AddListener(OnClickLeft);
        
        // 右侧按钮点击事件
        UIRightBtn.GetComponent<Button>().onClick.AddListener(OnClickRight);
        
        // ==================== 删除面板按钮 ====================
        // 删除面板返回按钮点击事件
        UIDeleteBackBtn.GetComponent<Button>().onClick.AddListener(OnDeleteBack);
        
        // 删除面板确认按钮点击事件
        UIDeleteConfirmBtn.GetComponent<Button>().onClick.AddListener(OnDeleteConfirm);
        
        // ==================== 底部菜单按钮 ====================
        // 删除按钮点击事件
        UIDeleteBtn.GetComponent<Button>().onClick.AddListener(OnDelete);
        
        // 详情按钮点击事件
        UIDetailBtn.GetComponent<Button>().onClick.AddListener(OnDetail);
    }

    // ==================== 顶部菜单按钮事件 ====================

    /// <summary>
    /// 点击武器分类按钮
    /// 切换到武器分类显示
    /// </summary>
    private void OnClickWeapon()
    {
        print(">>>>> OnClickWeapon");
    }

    /// <summary>
    /// 点击食物分类按钮
    /// 切换到食物分类显示
    /// </summary>
    private void OnClickFood()
    {
        print(">>>>> OnClickFood");
    }

    /// <summary>
    /// 点击关闭按钮
    /// 关闭当前背包面板，打开抽卡界面
    /// </summary>
    private void OnClickClose()
    {
        print(">>>>> OnClickClose");
        
        // 打开抽卡面板
        var drawCardPanel = UIManager.Instance.OpenPanel<DrawCardPanel>(out var isOpen);
        if (isOpen)
        {
            // 面板打开成功，可以在这里添加初始化逻辑
        }
        
        // 关闭当前背包面板
        UIManager.Instance.ClosePanel<PackagePanel>();
        
        // 隐藏面板（可能包含动画效果）
        Hide();
        
        // ClosePanel();
        // UIManager.Instance.OpenPanel(UIConst.DrawCardPanel);
    }

    // ==================== 翻页按钮事件 ====================

    /// <summary>
    /// 点击左侧按钮
    /// 用于翻页或切换到上一个物品
    /// </summary>
    private void OnClickLeft()
    {
        print(">>>>> OnClickLeft");
    }

    /// <summary>
    /// 点击右侧按钮
    /// 用于翻页或切换到下一个物品
    /// </summary>
    private void OnClickRight()
    {
        print(">>>>> OnClickRight");
    }

    // ==================== 删除相关按钮事件 ====================

    /// <summary>
    /// 退出删除模式
    /// 从删除模式返回到普通模式
    /// </summary>
    private void OnDeleteBack()
    {
        print(">>>>> onDeleteBack");
        
        // 切换回普通模式
        curMode = PackageMode.normal;
        
        // 隐藏删除面板
        UIDeletePanel.gameObject.SetActive(false);
        
        // 重置选中的删除列表
        deleteChooseUid = new List<string>();
        
        // 刷新物品单元格的选中状态显示
        RefreshDeletePanel();
    }

    /// <summary>
    /// 确认删除操作
    /// 执行删除选中的物品
    /// </summary>
    private void OnDeleteConfirm()
    {
        print(">>>>> OnDeleteConfirm");
        
        // 检查选中列表是否存在
        if (this.deleteChooseUid == null)
        {
            return;
        }
        
        // 检查是否有选中物品
        if (this.deleteChooseUid.Count == 0)
        {
            return;
        }
        
        // 调用GameManager删除物品
        GameManager.Instance.DeletePackageItems(this.deleteChooseUid);
        
        // 删除完成后刷新整个背包页面
        RefreshUI();
    }

    /// <summary>
    /// 进入删除模式
    /// 点击左下角删除按钮时调用
    /// </summary>
    private void OnDelete()
    {
        print(">>>>> OnDelete OnDelete OnDelete");
        
        // 切换到删除模式
        curMode = PackageMode.delete;
        
        // 显示删除面板
        UIDeletePanel.gameObject.SetActive(true);
    }

    // ==================== 详情按钮事件 ====================

    /// <summary>
    /// 点击详情按钮
    /// 显示选中物品的详细信息
    /// </summary>
    private void OnDetail()
    {
        print(">>>>> OnDetail");
    }
}
