using UnityEngine;
using UnityEngine.UI;

public class SettingPanel : UIPopPanelBase
{
    [Header("子面板")]
    [SerializeField] private BasicSettingsPanel basicSettingsPanel;
    [SerializeField] private HotkeySettingsPanel hotkeySettingsPanel;

    [Header("页签按钮（使用按钮颜色作为高亮）")]
    [SerializeField] private Button basicTabButton;
    [SerializeField] private Button hotkeyTabButton;
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color unselectedColor = Color.gray;

    private void OnEnable()
    {
        // 默认进入基础设置页
        ShowBasicSettingsPanel();
        Show();
    }

    // 打开基础设置页
    public void ShowBasicSettingsPanel()
    {
        SetActivePanel(true);
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }

    // 打开热键设置页
    public void ShowHotkeySettingsPanel()
    {
        SetActivePanel(false);
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }

    // 关闭整个设置面板（绑定关闭按钮）
    public void OnCloseButtonClick()
    {
        UIManager.Instance.ClosePanel<SettingPanel>();
        Hide();
    }

    private void SetActivePanel(bool showBasic)
    {
        if (basicSettingsPanel != null)
            basicSettingsPanel.gameObject.SetActive(showBasic);
        if (hotkeySettingsPanel != null)
            hotkeySettingsPanel.gameObject.SetActive(!showBasic);

        // 用按钮颜色来做高亮，而不是切换高亮物体的开关
        if (basicTabButton != null) SetButtonColor(basicTabButton, showBasic);
        if (hotkeyTabButton != null) SetButtonColor(hotkeyTabButton, !showBasic);

        if (showBasic)
        {
            // 切回基础页时刷新一次（确保与当前存档一致）
            basicSettingsPanel?.gameObject.SetActive(true);
        }
        else
        {
            // 热键页内自处理加载/刷新
            hotkeySettingsPanel?.Show();
        }
    }

    // 尝试通过 Image 颜色优先（通常用于纯色高亮），若不存在 Image 则回退到修改 Button.colors.normalColor
    private void SetButtonColor(Button btn, bool selected)
    {
        if (btn == null) return;

        Color target = selected ? selectedColor : unselectedColor;

        // 优先设置 Image.color（常见且直观）
        if (btn.image != null)
        {
            btn.image.color = target;
            return;
        }

        // 回退：修改 Button 的 ColorBlock 的 normalColor
        var cb = btn.colors;
        cb.normalColor = target;
        btn.colors = cb;
    }
}