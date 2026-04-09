using TMPro;
using UnityEngine;

public class LogInPopPanel : UIPopPanelBase
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text contentText;
    
    public void Init(string title, string content)
    {
        titleText.text = title;
        contentText.text = content;
        Show();
    }
    public void OnClose()
    {
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
        Hide();
    }
}
