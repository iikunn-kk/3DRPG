using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if TMP_PRESENT
using TMPro;
#endif

/// <summary>
/// 玩家死亡弹窗面板脚本：
/// - 由 UIManager.OpenPanel<DeathPopupPanel>() 动态实例化 (Resources/PlayingUI/DeathPopupPanel 预制体)
/// - 按下确认按钮后调用玩家的 OnDeathPopupConfirmed() 并关闭自己
/// - 动态显示损失比例说明
/// </summary>
public class DeathPopupPanel : UIPopPanelBase
{
    [Header("UI引用")]
    [SerializeField] private TMP_Text messageLegacyText; // 若未使用TMP则使用普通Text
    [SerializeField] private Button resurrectionButton;
    private CharacterState _player;
    private Action _resurrectionAction;
    
    public void Init(int expLoss,Action resurrectionAction)
    {
        resurrectionButton.interactable = false;
        messageLegacyText.text = $"你已死亡\n损失 {expLoss} 经验\n点击任意位置在最近复活点复活";
        _resurrectionAction = resurrectionAction;
        Show(() =>
        {
            resurrectionButton.interactable = true;
        });
    }
    

    public void OnButtonClick()
    {
        _resurrectionAction.Invoke();
        Hide();
        UIManager.Instance.ClosePanel<DeathPopupPanel>();
    }

}

