using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharacterSelectMod : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RawImage characterImage;
    [Header("职业名字")]
    [SerializeField] private TMP_Text professionText;
    [Header("名字")]
    [SerializeField] private TMP_Text characterName;
    [Header("等级文字")]
    [SerializeField] private TMP_Text levelText;
    [Header("选中高亮边框")]
    [SerializeField] private Image border;
    private Action<CharacterSelectMod, CharacterData> _onClick;
    private Action<CharacterData, CharacterSelectMod> _onDelete;
    private CharacterData _data;

    public void Initialized(CharacterData data, RenderTexture texture, Action<CharacterSelectMod, CharacterData> onClick, Action<CharacterData, CharacterSelectMod> onDelete)
    {
        professionText.text = data.profession.ToString();
        characterName.text = data.characterName;
        characterImage.texture = texture;
        levelText.text = $"Lv.{data.level}";
        _data = data;
        _onClick = onClick;
        _onDelete = onDelete;
        SetSelected(false);
    }

    public void OnClick()
    {
        _onClick?.Invoke(this, _data);
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }

    public void OnDeleteButtonClick()
    {
        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
        _onDelete?.Invoke(_data, this);
    }

    public void SetSelected(bool isSelected)
    {
        if (border != null)
        {
            border.gameObject.SetActive(isSelected);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 取消悬停时高亮，选中状态由 SetSelected 控制
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 取消悬停时高亮，选中状态由 SetSelected 控制
    }

    public CharacterData GetData()
    {
        return _data;
    }
}
