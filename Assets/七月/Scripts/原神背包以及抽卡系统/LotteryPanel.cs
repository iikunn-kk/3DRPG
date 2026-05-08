using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 抽卡面板类 - 管理抽卡界面的核心逻辑
/// 继承自UIPopPanelBase，实现抽卡功能的UI交互
/// </summary>
public class LotteryPanel : UIPopPanelBase
{
    // ==================== UI组件引用 ====================

    /// <summary>
    /// 关闭按钮的Transform组件
    /// 位于界面右上角，用于关闭当前面板
    /// </summary>
    private Transform UIClose;

    /// <summary>
    /// 抽卡展示区域的Transform组件
    /// 用于作为抽卡卡片的父物体，卡片将在此区域生成显示
    /// </summary>
    private Transform UICenter;

    /// <summary>
    /// 十连抽按钮的Transform组件
    /// 位于界面底部，用户点击后执行十连抽
    /// </summary>
    private Transform UILottery10;

    /// <summary>
    /// 单抽按钮的Transform组件
    /// 位于界面底部，用户点击后执行单抽
    /// </summary>
    private Transform UILottery1;

    /// <summary>
    /// 抽卡卡片的预制体引用
    /// 通过Resources.Load从Resources/Prefab/Panel/Lottery/LotteryItem路径加载
    /// </summary>
    private GameObject LotteryCellPrefab;

    /// <summary>
    /// 初始化方法 - 在对象创建时调用
    /// 执行基类初始化、UI初始化和预制体加载
    /// </summary>
    protected override void Awake()
    {
        // 调用基类的Awake方法，确保父类的初始化逻辑正常执行
        base.Awake();

        // 初始化UI组件引用，将Transform组件绑定到对应的UI元素
        InitUI();

        // 加载抽卡卡片的预制体资源，用于后续实例化生成卡片
        InitPrefab();
    }

    /// <summary>
    /// Start方法 - 在第一帧更新前调用
    /// 面板打开时自动执行一次十连抽，显示初始抽卡结果
    /// </summary>
    private void Start()
    {
        // 面板打开时就自动执行十连抽，展示初始抽卡结果
        OnLottery10Btn();
    }

    /// <summary>
    /// 初始化UI组件 - 将Transform组件与UI元素绑定
    /// 通过transform.Find方法根据路径查找子物体
    /// </summary>
    private void InitUI()
    {
        // 查找关闭按钮：位于TopRight节点下的Close子物体
        UIClose = transform.Find("TopRight/Close");

        // 查找抽卡展示区域：位于Center节点
        UICenter = transform.Find("Center");

        // 查找十连抽按钮：位于Bottom节点下的Lottery10子物体
        UILottery10 = transform.Find("Bottom/Lottery10");

        // 查找单抽按钮：位于Bottom节点下的Lottery1子物体
        UILottery1 = transform.Find("Bottom/Lottery1");

        // 为十连抽按钮添加点击事件监听
        UILottery10.GetComponent<Button>().onClick.AddListener(OnLottery10Btn);

        // 为单抽按钮添加点击事件监听
        UILottery1.GetComponent<Button>().onClick.AddListener(OnLottery1Btn);

        // 为关闭按钮添加点击事件监听
        UIClose.GetComponent<Button>().onClick.AddListener(OnClose);
    }

    /// <summary>
    /// 初始化预制体 - 从Resources目录加载抽卡卡片预制体
    /// 使用Resources.Load方式加载，需要预制体放置在Resources目录下
    /// </summary>
    private void InitPrefab()
    {
        // 从Resources/Prefab/Panel/Lottery/LotteryItem路径加载卡片预制体
        // as GameObject进行类型转换，确保加载成功
        LotteryCellPrefab = Resources.Load("Prefab/Panel/Lottery/LotteryItem") as GameObject;
    }

    /// <summary>
    /// 单抽按钮点击事件处理
    /// 执行单抽逻辑：清空展示区、抽取一个物品、生成卡片显示
    /// </summary>
    private void OnLottery1Btn()
    {
        // 输出调试日志，标记单抽按钮被触发
        print(">>>>>>>>>>>> OnLottery1Btn");

        // ==================== 清空原有卡片 ====================
        // 遍历展示区域的所有子物体（原有卡片），逐一销毁
        for (int i = 0; i < UICenter.childCount; i++)
        {
            // 获取第i个子物体并销毁
            Destroy(UICenter.GetChild(i).gameObject);
        }

        // ==================== 执行单抽逻辑 ====================
        // 调用GameManager的单抽方法，获取随机抽取的物品数据
        // PackageLocalItem item = GameManager.Instance.GetLotteryRandom1();
        InventoryItem item = GameManager.Instance.GetLotteryRandom1();

        // ==================== 生成卡片并显示 ====================
        // 实例化卡片预制体，并设置父物体为展示区域
        Transform LotteryCellTran = Instantiate(LotteryCellPrefab.transform, UICenter) as Transform;

        // 获取卡片的LotteryCell组件引用
        LotteryCell lotteryCell = LotteryCellTran.GetComponent<LotteryCell>();

        // 调用Refresh方法，传入物品数据和当前面板引用，更新卡片显示内容
        // lotteryCell.Refresh(item, this);
        lotteryCell.Init(item, this);
    }

    /// <summary>
    /// 十连抽按钮点击事件处理
    /// 执行十连抽逻辑：清空展示区、抽取十个物品、生成卡片依次显示
    /// </summary>
    private void OnLottery10Btn()
    {
        // 输出调试日志，标记十连抽按钮被触发
        print(">>>>>>>>>> OnLottery10Btn");

        // ==================== 执行十连抽逻辑 ====================
        // 调用GameManager的十连抽方法，传入sort:true表示结果需要排序
        // 返回一个包含10个物品的列表
        // List<PackageLocalItem> packageLocalItems = GameManager.Instance.GetLotteryRandom10(sort: true);
        List<InventoryItem> items = GameManager.Instance.GetLotteryRandom10(sort: true);


        // ==================== 清空原有卡片 ====================
        // 遍历展示区域的所有子物体（原有卡片），逐一销毁
        for (int i = 0; i < UICenter.childCount; i++)
        {
            Destroy(UICenter.GetChild(i).gameObject);
        }

        // ==================== 生成并显示所有卡片 ====================
        // 遍历十连抽结果列表，为每个物品生成一张卡片
        // foreach (PackageLocalItem item in packageLocalItems)
        foreach (InventoryItem item in items)
        {
            // 实例化卡片预制体，设置父物体为展示区域
            Transform LotteryCellTran = Instantiate(LotteryCellPrefab.transform, UICenter) as Transform;

            // 获取卡片的LotteryCell组件引用
            LotteryCell lotteryCell = LotteryCellTran.GetComponent<LotteryCell>();

            // 调用Refresh方法更新卡片显示
            // lotteryCell.Refresh(item, this);
            lotteryCell.Init(item, this);
        }
    }

    /// <summary>
    /// 关闭按钮点击事件处理
    /// 关闭当前抽卡面板，并打开抽卡主界面（DrawCardPanel）
    /// </summary>
    private void OnClose()
    {
        // 输出调试日志，标记关闭按钮被触发
        print(">>>>>>>>> OnClose");

        // ==================== 打开下一个面板 ====================
        // 使用UIManager打开DrawCardPanel（抽卡主界面）
        // out var isOpen用于获取是否成功打开
        var drawCardPanel = UIManager.Instance.OpenPanel<DrawCardPanel>(out var isOpen);

        // 检查面板是否成功打开（isOpen为true表示成功）
        if (isOpen)
        {
            // 面板打开成功，可以在这里执行额外的初始化逻辑
            // 目前代码块为空，如果有需要可以在此处添加
        }

        // ==================== 关闭当前面板 ====================
        // 通过UIManager关闭当前的抽卡展示面板
        UIManager.Instance.ClosePanel<LotteryPanel>();

        // 调用Hide方法隐藏面板（可能包含动画效果）
        Hide();
    }
}
