using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryPanel : UIPopPanelBase, IUIPlayerControlLock
{
    #region 序列化字段

    [Header("3D展示摄影棚管理器")]
    [SerializeField] private Inventory3DStudioManager inventory3DStudioManager;

    [Header("角色装备")]
    [SerializeField] private EquipmentSlotUI headSlotUI;
    [SerializeField] private EquipmentSlotUI bodySlotUI;
    [SerializeField] private EquipmentSlotUI weaponSlotUI;
    [SerializeField] private EquipmentSlotUI footSlotUI;
    [SerializeField] private EquipmentSlotUI ringSlotUI;

    [Header("LoopScrollRect 配置")]
    [SerializeField] private GameObject inventorySlotPrefab;
    [SerializeField] private LoopScrollRectMulti inventoryLoopScroll;
    [SerializeField] private InventoryLoopController inventoryLoop;

    [Header("详情面板")]
    [SerializeField] private SlotDetailsPanel slotDetailsPanel;
    [SerializeField] private EquipSlotDetailsPanel equipSlotDetailsPanel;
    [SerializeField] private TMP_Text goldText;

    [SerializeField] private int randomSlotCount = 5;

    #endregion

    #region 私有字段

    private readonly Dictionary<EquipmentType, EquipmentSlotUI> _equipmentSlotsUI = new();

    #endregion

    #region 生命周期方法

    protected override void Awake()
    {
        base.Awake();
        InitializeEquipmentSlots();
        TryRegisterProtectedAreas();
        inventoryLoop.Init(inventoryLoopScroll, inventorySlotPrefab, ShowDetails, HideDetails);
    }

    private void OnEnable()
    {
        InventoryManager.OnInventoryUpdated += RefreshAllUI;
        Setup3DStudio();
        OnUIPanelShow();
        Show();
    }

    private void Start()
    {
        // 推迟到 Start：确保 LoopScrollRect + GridLayoutGroup 布局已就绪
        RefreshAllUI();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        InventoryManager.OnInventoryUpdated -= RefreshAllUI;
        OnUIPanelHide();
    }

    #endregion

    #region UI刷新方法

    private void RefreshAllUI()
    {
        ClearAllSlots();
        RefreshEquippedItemsUI();
        RefreshInventoryItemsUI();
        RefenceGoldText();
    }

    private void InitializeEquipmentSlots()
    {
        _equipmentSlotsUI[EquipmentType.头盔] = headSlotUI;
        _equipmentSlotsUI[EquipmentType.上衣] = bodySlotUI;
        _equipmentSlotsUI[EquipmentType.武器] = weaponSlotUI;
        _equipmentSlotsUI[EquipmentType.鞋子] = footSlotUI;
        _equipmentSlotsUI[EquipmentType.戒指] = ringSlotUI;
    }

    private void TryRegisterProtectedAreas()
    {
        if (DragAndDropPanel.Instance == null) return;
        if (headSlotUI != null) DragAndDropPanel.Instance.RegisterProtectedArea(headSlotUI.transform as RectTransform);
        if (bodySlotUI != null) DragAndDropPanel.Instance.RegisterProtectedArea(bodySlotUI.transform as RectTransform);
        if (weaponSlotUI != null) DragAndDropPanel.Instance.RegisterProtectedArea(weaponSlotUI.transform as RectTransform);
        if (footSlotUI != null) DragAndDropPanel.Instance.RegisterProtectedArea(footSlotUI.transform as RectTransform);
        if (ringSlotUI != null) DragAndDropPanel.Instance.RegisterProtectedArea(ringSlotUI.transform as RectTransform);
    }

    private void Setup3DStudio()
    {
        var playerCharacter = CharacterService.Instance.CurrentPlayerCharacter();
        if (playerCharacter != null)
        {
            inventory3DStudioManager.InitStudio(playerCharacter.Profession);
        }
    }

    private void RefreshEquippedItemsUI()
    {
        var equippedItems = InventoryManager.Instance.GetEquippedItems();
        foreach (var item in equippedItems)
        {
            var equipmentData = GameDataConfig.Instance.ItemDataSo.GetEquipmentDataById(item.itemId);
            if (equipmentData != null && _equipmentSlotsUI.ContainsKey(equipmentData.equipmentType))
            {
                _equipmentSlotsUI[equipmentData.equipmentType].Init(item, ShowDetails, HideDetails);
            }
        }
    }

    private void RefreshInventoryItemsUI()
    {
        inventoryLoop.RefreshList();
    }

    private void ClearAllSlots()
    {
        foreach (var slot in _equipmentSlotsUI.Values)
        {
            slot.ClearSlot();
        }
    }

    #endregion

    #region 详情面板方法

    private void ShowDetails(InventoryItem item)
    {
        if (item == null) return;
        var data = GameDataConfig.Instance.ItemDataSo.GetItemDataById(item.itemId);
        bool isEquipment = data is EquipmentData;

        if (isEquipment)
        {
            if (slotDetailsPanel != null) slotDetailsPanel.HideDetails();
            if (equipSlotDetailsPanel != null)
                equipSlotDetailsPanel.ShowDetails(item);
            else if (slotDetailsPanel != null)
                slotDetailsPanel.ShowDetails(item);
        }
        else
        {
            if (equipSlotDetailsPanel != null) equipSlotDetailsPanel.HideDetails();
            if (slotDetailsPanel != null)
                slotDetailsPanel.ShowDetails(item);
        }
    }

    private void HideDetails()
    {
        if (slotDetailsPanel != null) slotDetailsPanel.HideDetails();
        if (equipSlotDetailsPanel != null) equipSlotDetailsPanel.HideDetails();
    }

    #endregion

    #region 金币显示方法

    public void RefenceGoldText(int text)
    {
        goldText.text = text.ToString();
    }

    private void RefenceGoldText()
    {
        goldText.text = CharacterService.Instance.Money.ToString();
    }

    #endregion

    #region 公共方法

    public void OnCloseButtonClick()
    {
        UIManager.Instance.ClosePanel<InventoryPanel>();
        Hide();
    }

    #endregion

    #region 玩家控制锁定

    public void OnUIPanelShow()
    {
        var movement = CharacterService.Instance.CurrentPlayerCharacter()?.Movement;
        if (movement != null)
        {
            movement.LockPlayerControl();
        }
    }

    public void OnUIPanelHide()
    {
        var movement = CharacterService.Instance.CurrentPlayerCharacter()?.Movement;
        if (movement != null)
        {
            movement.UnlockPlayerControl();
        }
    }

    #endregion

    #region 测试方法

    public void GenerateRandomItems()
    {
        GameDataConfig.Instance.ItemDataSo.GenerateRandomItems(randomSlotCount);
    }

    public void GenerateRandomEquipment()
    {
        GameDataConfig.Instance.ItemDataSo.GenerateRandomEquipment(randomSlotCount);
    }

    public void AddMoney()
    {
        CharacterService.Instance.AddMoney(100000);
    }
    #endregion
}
