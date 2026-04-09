using TMPro;
using UnityEngine;
using System;

public class ServerCategoryMod : MonoBehaviour
{
    [SerializeField] private TMP_Text serverCategoryName;
    [SerializeField] private UnityEngine.UI.Button categoryButton;
    
    private int _serverCategoryId;
    private Action<int> _onCategorySelected;
    
    public void Init(ServerCategoryData data, Action<int> onCategorySelected)
    {
        _serverCategoryId = data.categoryId;
        _onCategorySelected = onCategorySelected;
        
        if (serverCategoryName != null)
        {
            serverCategoryName.text = data.categoryName;
        }
        
        // 添加按钮点击事件
        if (categoryButton != null)
        {
            categoryButton.onClick.RemoveAllListeners();
            categoryButton.onClick.AddListener(() => OnCategorySelected());
        }
    }
    
    private void OnCategorySelected()
    {
        // 触发分类选择回调，传入分类ID
        _onCategorySelected?.Invoke(_serverCategoryId);
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }
}