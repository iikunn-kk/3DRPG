using TMPro;
using UnityEngine;
using System.Collections.Generic;

public class PlayerStatePanel : UIPopPanelBase
{
    #region 字段
    [Header("摄影棚Obj")]
    [SerializeField] private GameObject ui3dStudio;//UI3DStudioOfInventory
    [Header("摄影棚位置")]
    private Vector3 _ui3dStudioPos=new Vector3(2000,2000,0);
    [Header("头部装备槽")]
    [SerializeField] private EquipmentSlotUI headSlotUI;
    [Header("身体装备槽")]
    [SerializeField] private EquipmentSlotUI bodySlotUI;
    [Header("手部装备槽")]
    [SerializeField] private EquipmentSlotUI handSlotUI;
    [Header("脚部装备槽")]
    [SerializeField] private EquipmentSlotUI footSlotUI;
    [Header("戒指装备槽")]
    [SerializeField] private EquipmentSlotUI ringSlotUI;
    [Header("装备详情面板")]
    [SerializeField] private EquipSlotDetailsPanel equipSlotDetailsPanel;
    [Header("角色名称文本")]
    [SerializeField] private TMP_Text nameText;
    [Header("角色等级文本")]
    [SerializeField] private TMP_Text levelText;
    [Header("角色职业文本")]
    [SerializeField] private TMP_Text professionText;
    [Header("生命值文本")]
    [SerializeField] private TMP_Text hpText;
    [Header("经验值文本")]
    [SerializeField] private TMP_Text expText;
    [Header("攻击力文本")]
    [SerializeField] private TMP_Text attackText;
    [Header("防御力文本")]
    [SerializeField] private TMP_Text defenseText;
    [Header("生命恢复速度文本")]
    [SerializeField] private TMP_Text hpRecoverySpeedText;
    [Header("移动速度文本")]
    [SerializeField] private TMP_Text speedText;
    [Header("物理伤害文本")]
    [SerializeField] private TMP_Text physicalDamageText;
    [Header("魔法伤害文本")]
    [SerializeField] private TMP_Text magicDamageText;
    #endregion

    // 维护一个装备槽字典，便于根据装备类型快速定位槽位
    private readonly Dictionary<EquipmentType, EquipmentSlotUI> _equipmentSlotsUI = new();

    private GameObject _ui3dStudio;

    protected override void Awake()
    {
        base.Awake();
        InitializeSlotMap();
    }

    private void OnEnable()
    {
        InventoryManager.OnInventoryUpdated += RefreshAllUI;
        RefreshAllUI();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        InventoryManager.OnInventoryUpdated -= RefreshAllUI;
        HideDetails();
    }

    public void Init(CharacterState characterState)
    {
        UpdatePlayerState(characterState);
        _ui3dStudio = Instantiate(ui3dStudio, _ui3dStudioPos, Quaternion.identity);
        _ui3dStudio.GetComponent<UI3DStudioOfInventory>().Init(characterState.Profession);
        Show();
        // 确保打开时同步显示当前已装备物品
        RefreshAllUI();
    }
    
    public void UpdatePlayerState(CharacterState characterState)
    { 
        if (characterState != null)
        {
            nameText.text = characterState.CharacterName;
            levelText.text = characterState.Level.ToString();
            professionText.text = characterState.Profession.ToString();
            hpText.text = characterState.CurrentHealth + "/" + characterState.MaxHealth;
            expText.text = characterState.Exp + "/" + characterState.NeedExp;
            attackText.text = characterState.Attack.ToString();
            defenseText.text = characterState.Defence.ToString();
            hpRecoverySpeedText.text = characterState.HpRecoverySpeed.ToString("F1");
            speedText.text = characterState.Speed.ToString("F1");
            physicalDamageText.text = characterState.PhysicalDamage.ToString("F1");
            magicDamageText.text = characterState.MagicDamage.ToString("F1");
        }
    }

    // 刷新：清空并根据当前已装备物品填充到UI槽位
    private void RefreshAllUI()
    {
        ClearAllSlots();
        RefreshEquippedItemsUI();
    }

    private void InitializeSlotMap()
    {
        _equipmentSlotsUI.Clear();
        if (headSlotUI != null) _equipmentSlotsUI[EquipmentType.头盔] = headSlotUI;
        if (bodySlotUI != null) _equipmentSlotsUI[EquipmentType.上衣] = bodySlotUI;
        if (handSlotUI != null) _equipmentSlotsUI[EquipmentType.手套] = handSlotUI;
        if (footSlotUI != null) _equipmentSlotsUI[EquipmentType.鞋子] = footSlotUI;
        if (ringSlotUI != null) _equipmentSlotsUI[EquipmentType.戒指] = ringSlotUI;
    }

    private void RefreshEquippedItemsUI()
    {
        var equippedItems = InventoryManager.Instance.GetEquippedItems();
        foreach (var item in equippedItems)
        {
            var equipmentData = GameDataConfig.Instance.ItemDataSo.GetEquipmentDataById(item.itemId);
            if (equipmentData != null && _equipmentSlotsUI.TryGetValue(equipmentData.equipmentType, out var slotUI))
            {
                slotUI.Init(item, ShowDetails, HideDetails);
            }
        }
    }

    private void ClearAllSlots()
    {
        foreach (var slot in _equipmentSlotsUI.Values)
        {
            slot.ClearSlot();
        }
    }

    // 装备详情的显示/隐藏（用于装备槽悬停）
    private void ShowDetails(InventoryItem item)
    {
        if (item == null) return;
        if (equipSlotDetailsPanel != null)
        {
            equipSlotDetailsPanel.ShowDetails(item);
        }
    }

    private void HideDetails()
    {
        if (equipSlotDetailsPanel != null)
        {
            equipSlotDetailsPanel.HideDetails();
        }
    }

    public void OnCloseButtonClick()
    {
        UIManager.Instance.ClosePanel<PlayerStatePanel>();
        Hide(true,() =>
        {
            Destroy(_ui3dStudio);
        });
    }
}