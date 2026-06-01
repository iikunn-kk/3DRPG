using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 抽卡面板类 - 管理抽卡界面的核心逻辑
/// 继承自UIPopPanelBase，实现抽卡功能的UI交互
/// </summary>
/// <summary>
/// 抽卡模式枚举：外部调用者通过 Initialize 指定初始行为
/// </summary>
public enum LotteryMode { Single, Ten }

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
    /// 外部入口：根据抽卡模式初始化面板行为，由 DrawCardPanel 在 OpenPanel 后调用
    /// </summary>
    public void Initialize(LotteryMode mode)
    {
        switch (mode)
        {
            case LotteryMode.Single:
                OnLottery1Btn();
                break;
            case LotteryMode.Ten:
                OnLottery10Btn();
                break;
        }
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
        // LotteryCellPrefab = Resources.Load("Prefab/Panel/Lottery/LotteryItem") as GameObject;
        LotteryCellPrefab = AddressableCache.Load<GameObject>("LotteryItem");
    }

    /// <summary>
    /// 单抽按钮点击事件处理
    /// 执行单抽逻辑：清空展示区、抽取一个物品、生成卡片显示
    /// </summary>
    private void OnLottery1Btn()
    {
        print(">>>>>>>>>>>> OnLottery1Btn");

        for (int i = 0; i < UICenter.childCount; i++)
        {
            Destroy(UICenter.GetChild(i).gameObject);
        }

        InventoryItem item = LegacyPackageManager.Instance.GetLotteryRandom1();
        if (item == null)
        {
            Debug.LogWarning("单抽失败，可能背包已满");
            return;
        }

        Transform LotteryCellTran = Instantiate(LotteryCellPrefab.transform, UICenter) as Transform;
        LotteryCell lotteryCell = LotteryCellTran.GetComponent<LotteryCell>();
        lotteryCell.Init(item, this);
    }

    /// <summary>
    /// 十连抽按钮点击事件处理
    /// 执行十连抽逻辑：清空展示区、抽取十个物品、生成卡片依次显示
    /// </summary>
    private void OnLottery10Btn()
    {
        print(">>>>>>>>>> OnLottery10Btn");

        List<InventoryItem> items = LegacyPackageManager.Instance.GetLotteryRandom10(sort: true);
        if (items == null || items.Count == 0)
        {
            Debug.LogWarning("十连抽返回空结果，可能背包已满");
            return;
        }

        for (int i = 0; i < UICenter.childCount; i++)
        {
            Destroy(UICenter.GetChild(i).gameObject);
        }

        foreach (InventoryItem item in items)
        {
            Transform LotteryCellTran = Instantiate(LotteryCellPrefab.transform, UICenter) as Transform;
            LotteryCell lotteryCell = LotteryCellTran.GetComponent<LotteryCell>();
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
