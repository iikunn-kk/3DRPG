using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChooseProfessionMod : MonoBehaviour
{
    [Header("UI组件")]
    [SerializeField] private Image titleImage;
    [SerializeField] private TMP_Text professionName;
    [SerializeField] private Image selectionImage;
    
    private CharacterProfession _profession;
    private System.Action<CharacterProfession> _onProfessionSelected;

    /// <summary>
    /// 初始化职业选择按钮
    /// </summary>
    /// <param name="data">职业数据</param>
    /// <param name="onProfessionSelected">选择职业的回调</param>
    public void Init(CreateCharacterData data, System.Action<CharacterProfession> onProfessionSelected)
    {
        _onProfessionSelected = onProfessionSelected;
        _profession = data.profession;
        
        // 设置显示内容
        if (titleImage != null && data.titleImage != null)
            titleImage.sprite = data.titleImage;
            
        if (professionName != null)
            professionName.text = _profession.ToString();
            
        // 默认隐藏选中状态
        if (selectionImage != null)
            selectionImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// 当玩家点击职业按钮时调用
    /// </summary>
    public void OnProfessionSelected()
    {
        _onProfessionSelected?.Invoke(_profession);
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }
    
    /// <summary>
    /// 设置选中状态
    /// </summary>
    /// <param name="selected">是否选中</param>
    public void SetSelected(bool selected)
    {
        if (selectionImage != null)
            selectionImage.gameObject.SetActive(selected);
    }
    
    /// <summary>
    /// 获取职业枚举
    /// </summary>
    /// <returns>职业枚举</returns>
    public CharacterProfession GetProfession()
    {
        return _profession;
    }
}