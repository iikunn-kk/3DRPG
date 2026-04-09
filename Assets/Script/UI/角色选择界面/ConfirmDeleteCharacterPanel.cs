using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class ConfirmDeleteCharacterPanel : UIPopPanelBase
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text confirmText;

    private Func<Task> _onConfirmAsync;

    public void Init(CharacterData data, Func<Task> onConfirmAsync)
    {
        _onConfirmAsync = onConfirmAsync;
        if (titleText != null)
        {
            titleText.text = "删除角色确认";
        }
        if (confirmText != null && data != null)
        {
            confirmText.text = $"是否确认删除角色：{data.characterName}? 此操作无法撤销。";
        }
        Show();
    }

    public async void OnConfirmButtonClick()
    {
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
        try
        {
            if (_onConfirmAsync != null)
            {
                await _onConfirmAsync.Invoke();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"执行删除回调时发生异常: {e.Message}");
        }
        Hide(false);
    }

    public void OnCancelButtonClick()
    {
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
        Hide(false);
    }
}
