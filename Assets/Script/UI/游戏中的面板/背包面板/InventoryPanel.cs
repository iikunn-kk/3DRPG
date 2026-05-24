using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class InventoryPanel : UIPopPanelBase, IUIPlayerControlLock
{
    #region 序列化字段

    [Header("3D展示摄影棚管理器")]
    [SerializeField] private Inventory3DStudioManager inventory3DStudioManager;

    [Header("角色装备")]
    [SerializeField] private EquipmentSlotUI headSlotUI;
    [SerializeField] private EquipmentSlotUI bodySlotUI;
    // [SerializeField] private EquipmentSlotUI handSlotUI;
    [SerializeField] private EquipmentSlotUI weaponSlotUI;
    [SerializeField] private EquipmentSlotUI footSlotUI;
    [SerializeField] private EquipmentSlotUI ringSlotUI;


    [Header("背包格子的预制体")]
    [SerializeField] private GameObject inventorySlotPrefab;

    [Header("背包格子的父物体")]
    [SerializeField] private Transform inventorySlotParent;

    [Header("详情面板")]
    [SerializeField] private SlotDetailsPanel slotDetailsPanel;
    [SerializeField] private EquipSlotDetailsPanel equipSlotDetailsPanel; // 新增：装备专用详情面板
    [SerializeField] private TMP_Text goldText;

    [SerializeField] private int randomSlotCount = 5;

    #endregion

    #region 私有字段

    // [修改] 背包格子和装备格子的引用
    private readonly List<InventorySlot> _inventorySlotsUI = new();
    private readonly Dictionary<EquipmentType, EquipmentSlotUI> _equipmentSlotsUI = new();

    #endregion

    #region 生命周期方法

    protected override void Awake()
    {
        base.Awake();
        // [新增] 初始化格子UI，这个过程只在Awake中执行一次
        InitializeSlotPools();
        // 注册受保护区域，防止拖拽在格子间隙被判定为丢弃
        TryRegisterProtectedAreas();
    }

    private void OnEnable()
    {
        // [新增] 订阅事件
        InventoryManager.OnInventoryUpdated += RefreshAllUI;
        // 第一次打开时，初始化3D模型并立即刷新一次UI
        Setup3DStudio();
        RefreshAllUI();
        OnUIPanelShow();
        Show();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        // [新增] 取消订阅事件
        InventoryManager.OnInventoryUpdated -= RefreshAllUI;
        OnUIPanelHide();
    }

    #endregion

    #region UI刷新方法

    /// <summary>
    /// [新增] 统一的UI刷新入口
    /// </summary>
    private void RefreshAllUI()
    {
        ClearAllSlots();
        RefreshEquippedItemsUI();
        RefreshInventoryItemsUI();
        RefenceGoldText();
    }

    /// <summary>
    /// [新增] 初始化/预创建所有UI格子
    /// </summary>
    private void InitializeSlotPools()
    {
        // 填充装备槽字典，方便后续访问
        _equipmentSlotsUI[EquipmentType.头盔] = headSlotUI;
        _equipmentSlotsUI[EquipmentType.上衣] = bodySlotUI;
        // _equipmentSlotsUI[EquipmentType.手套] = handSlotUI;
        _equipmentSlotsUI[EquipmentType.武器] = weaponSlotUI;
        _equipmentSlotsUI[EquipmentType.鞋子] = footSlotUI;
        _equipmentSlotsUI[EquipmentType.戒指] = ringSlotUI;


        // 预创建背包格子
        int maxSlots = InventoryManager.Instance.MaxInventorySlots;
        for (int i = 0; i < maxSlots; i++)
        {
            var slotGo = Instantiate(inventorySlotPrefab, inventorySlotParent);
            var slotUI = slotGo.GetComponent<InventorySlot>();
            // 可以在这里初始化拖拽逻辑
            _inventorySlotsUI.Add(slotUI);
        }
    }

    private void TryRegisterProtectedAreas()
    {
        if (DragAndDropPanel.Instance == null) return;
        // 背包格子父节点
        if (inventorySlotParent != null)
        {
            var rt = inventorySlotParent as RectTransform;
            if (rt != null)
            {
                DragAndDropPanel.Instance.RegisterProtectedArea(rt);
            }
        }
        // 装备槽区域：逐个注册其 RectTransform（若存在）
        if (headSlotUI != null) DragAndDropPanel.Instance.RegisterProtectedArea(headSlotUI.transform as RectTransform);
        if (bodySlotUI != null) DragAndDropPanel.Instance.RegisterProtectedArea(bodySlotUI.transform as RectTransform);
        // if (handSlotUI != null) DragAndDropPanel.Instance.RegisterProtectedArea(handSlotUI.transform as RectTransform);
        if (weaponSlotUI != null) DragAndDropPanel.Instance.RegisterProtectedArea(weaponSlotUI.transform as RectTransform);
        if (footSlotUI != null) DragAndDropPanel.Instance.RegisterProtectedArea(footSlotUI.transform as RectTransform);
        if (ringSlotUI != null) DragAndDropPanel.Instance.RegisterProtectedArea(ringSlotUI.transform as RectTransform);

    }

    /// <summary>
    /// [新增] 初始化3D展示
    /// </summary>
    private void Setup3DStudio()
    {
        var playerCharacter = CharacterRuntimeManager.Instance.CurrentPlayerCharacter();
        if (playerCharacter != null)
        {
            inventory3DStudioManager.InitStudio(playerCharacter.Profession);
        }
    }

    /// <summary>
    /// [新增] 刷新已装备物品的UI显示
    /// </summary>
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

    /// <summary>
    /// [新增] 刷新背包物品的UI显示
    /// </summary>
    private void RefreshInventoryItemsUI()
    {
        var inventoryItems = InventoryManager.Instance.GetInventoryItems();
        foreach (var item in inventoryItems)
        {
            // 确保 slotIndex 在有效范围内
            if (item.slotIndex >= 0 && item.slotIndex < _inventorySlotsUI.Count)
            {
                _inventorySlotsUI[item.slotIndex].Init(item, ShowDetails, HideDetails);
            }
            else
            {
                // 如果物品的格子索引无效（例如-1），说明数据有问题，可以log出来
                Debug.LogWarning($"物品 {item.itemId} 的背包索引无效: {item.slotIndex}");
                // 也可以在这里添加逻辑，为它找一个空格子并更新后端数据，但这通常不是UI的职责
            }
        }
    }

    /// <summary>
    /// [新增] 在刷新前，清空所有格子的显示内容
    /// </summary>
    private void ClearAllSlots()
    {
        foreach (var slot in _equipmentSlotsUI.Values)
        {
            slot.ClearSlot();
        }
        foreach (var slot in _inventorySlotsUI)
        {
            slot.ClearSlot();
        }
    }

    #endregion

    #region 详情面板方法

    /// <summary>
    /// [修改] 统一的显示详情方法，根据物品类型显示不同面板
    /// </summary>
    private void ShowDetails(InventoryItem item)
    {
        if (item == null) return;
        var data = GameDataConfig.Instance.ItemDataSo.GetItemDataById(item.itemId);
        bool isEquipment = data is EquipmentData;

        if (isEquipment)
        {
            if (slotDetailsPanel != null) slotDetailsPanel.HideDetails();
            if (equipSlotDetailsPanel != null)
            {
                equipSlotDetailsPanel.ShowDetails(item);
            }
            else if (slotDetailsPanel != null)
            {
                // 兜底：未配置装备面板仍然使用普通面板
                slotDetailsPanel.ShowDetails(item);
            }
        }
        else
        {
            if (equipSlotDetailsPanel != null) equipSlotDetailsPanel.HideDetails();
            if (slotDetailsPanel != null)
            {
                slotDetailsPanel.ShowDetails(item);
            }
        }
    }

    private void HideDetails()
    {
        if (slotDetailsPanel != null)
        {
            slotDetailsPanel.HideDetails();
        }
        if (equipSlotDetailsPanel != null)
        {
            equipSlotDetailsPanel.HideDetails();
        }
    }

    #endregion

    #region 金币显示方法

    public void RefenceGoldText(int text)
    {
        goldText.text = text.ToString();
    }

    private void RefenceGoldText()
    {
        goldText.text = PlayerCurrencyManager.Instance.Money.ToString();
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
        var movement = CharacterRuntimeManager.Instance.CurrentPlayerCharacter()?.Movement;
        if (movement != null)
        {
            movement.LockPlayerControl();
        }
    }

    public void OnUIPanelHide()
    {
        var movement = CharacterRuntimeManager.Instance.CurrentPlayerCharacter()?.Movement;
        if (movement != null)
        {
            movement.UnlockPlayerControl();
        }
    }

    #endregion

    #region 测试方法 (用于调试)

    /// <summary>
    /// 随机生成若干物品添加到背包中（用于调试）
    /// </summary>
    public void GenerateRandomItems()
    {
        GameDataConfig.Instance.ItemDataSo.GenerateRandomItems(randomSlotCount);
    }

    /// <summary>
    /// 随机生成若干装备添加到背包中（用于调试）
    /// </summary>
    public void GenerateRandomEquipment()
    {
        GameDataConfig.Instance.ItemDataSo.GenerateRandomEquipment(randomSlotCount);
    }

    public void AddMoney()
    {
        PlayerCurrencyManager.Instance.AddMoney(100000);
    }
    #endregion
}