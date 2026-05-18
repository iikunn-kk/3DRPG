using UnityEngine;
using UnityEngine.UI;

public class DrawCardPanel : UIPopPanelBase
{
    [Header("按钮引用")]
    [SerializeField] private Button _singleDrawBtn;  // 单抽按钮
    [SerializeField] private Button _tenDrawBtn;     // 十连抽按钮
    [SerializeField] private Button _quitBtn;        // 退出按钮

    protected override void Awake()
    {
        base.Awake();
        BindButtons();
    }


    /// <summary>
    /// 绑定按钮事件。
    /// 使用 Inspector 拖入的方式替代 transform.Find，更安全、避免硬编码路径。
    /// </summary>
    private void BindButtons()
    {
        if (_quitBtn) _quitBtn.onClick.AddListener(OnQuitBtn);
        if (_singleDrawBtn) _singleDrawBtn.onClick.AddListener(OnBtnSingleDraw);
        if (_tenDrawBtn) _tenDrawBtn.onClick.AddListener(OnBtnTenDraw);
    }

    // ==================== 按钮回调 ====================

    /// <summary>单抽按钮</summary>
    private void OnBtnSingleDraw()
    {
        print("[DrawCardPanel] 单抽");
        OpenLotteryPanel(LotteryMode.Single);
    }

    /// <summary>十连抽按钮</summary>
    private void OnBtnTenDraw()
    {
        print("[DrawCardPanel] 十连抽");
        OpenLotteryPanel(LotteryMode.Ten);
    }

    /// <summary>退出按钮</summary>
    private void OnQuitBtn()
    {
        print("[DrawCardPanel] 退出");
        UIManager.Instance.ClosePanel<DrawCardPanel>();
        Hide();
    }


    //旧的打开背包按钮
    // private void OnBtnPackage()
    // {
    //     print(">>>>> OnBtnPackage");
    //     var packagePanel = UIManager.Instance.OpenPanel<PackagePanel>(out var isOpen);
    //     if (isOpen)
    //     {

    //     }
    //     UIManager.Instance.ClosePanel<DrawCardPanel>();
    //     Hide();
    // }



    // ==================== 核心逻辑 ====================

    /// <summary>
    /// 打开 LotteryPanel 并传入抽卡模式。
    /// 两个按钮共享这段逻辑，只有 LotteryMode 参数不同。
    /// </summary>
    private void OpenLotteryPanel(LotteryMode mode)
    {
        var lotteryPanel = UIManager.Instance.OpenPanel<LotteryPanel>(out var isOpen);
        if (isOpen)
        {
            lotteryPanel.Initialize(mode);
        }
        UIManager.Instance.ClosePanel<DrawCardPanel>();
        Hide();
    }
}
