using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// public class MainPanel : BasePanel
// public class DrawCardPanel : BasePanel
public class DrawCardPanel : UIPopPanelBase
{

    private Transform UILottery;
    private Transform UIPackage;
    private Transform UIQuitBtn;

    protected override void Awake()
    {
        base.Awake();
        InitUI();
    }

    private void InitUI()
    {
        UIQuitBtn = transform.Find("Top/QuitBtn");
        UILottery = transform.Find("Bottom/LotteryBtn");
        UIPackage = transform.Find("Bottom/PackageBtn");

        UIQuitBtn.GetComponent<Button>().onClick.AddListener(OnQuitBtn);
        UILottery.GetComponent<Button>().onClick.AddListener(OnBtnLottery);
        UIPackage.GetComponent<Button>().onClick.AddListener(OnBtnPackage);

    }

    private void OnBtnLottery()
    {
        print(">>>>> OnBtnLottery");
        // UIManager.Instance.OpenPanel(UIConst.LotteryPanel);
        // BasePanel.Instance.ClosePanel();

        var lotteryPanel = UIManager.Instance.OpenPanel<LotteryPanel>(out var isOpen);
        if (isOpen)
        {

        }
        UIManager.Instance.ClosePanel<DrawCardPanel>();
        Hide();
    }

    private void OnBtnPackage()
    {
        print(">>>>> OnBtnPackage");
        var packagePanel = UIManager.Instance.OpenPanel<PackagePanel>(out var isOpen);
        if (isOpen)
        {

        }
        UIManager.Instance.ClosePanel<DrawCardPanel>();
        Hide();
        // UIManager.Instance.OpenPanel(UIConst.PackagePanel);
        // BasePanel.Instance.ClosePanel();
    }

    private void OnQuitBtn()
    {
        print(">>>>> OnQuitDrawCardPanel");
        // BasePanel.Instance.ClosePanel();
        UIManager.Instance.ClosePanel<DrawCardPanel>();
        Hide();
        // #if UNITY_EDITOR
        //         EditorApplication.isPlaying = false;
        // #else
        //         Application.Quit();
        // #endif
    }
}
