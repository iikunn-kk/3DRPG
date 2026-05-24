using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 抽卡单元格组件
/// 负责显示单个抽卡卡片的UI界面
/// 功能包括：物品图片显示、星级展示等
/// 作为预制体被LotteryPanel动态实例化使用
/// </summary>
public class LotteryCell : MonoBehaviour
{
    // ==================== UI组件引用 ====================

    /// <summary>
    /// 物品图片的Transform组件
    /// 位于卡片中央位置，用于显示物品的图标或图片
    /// </summary>
    private Transform UIImage;

    /// <summary>
    /// 星级显示区域的Transform组件
    /// 位于卡片底部，包含多个星级图标子物体
    /// 用于显示物品的稀有度（星级）
    /// </summary>
    private Transform UIStars;

    // /// <summary>
    /// /// "新"标记的Transform组件（已注释）
    /// /// 位于卡片顶部，用于标识新获得的物品
    /// /// </summary>
    // private Transform UINew;

    // ==================== 数据引用 ====================

    /// <summary>
    /// 背包物品数据（InventoryItem）
    /// 包含该卡片的物品实例数据
    /// </summary>
    private InventoryItem inventoryItem;

    /// <summary>
    /// 物品数据配置
    /// 包含物品的静态配置信息（名称、图片、星级等）
    /// </summary>
    private ItemData itemData;

    /// <summary>
    /// 父级抽卡面板引用
    /// 用于与抽卡面板进行交互（如关闭按钮、刷新面板等）
    /// </summary>
    private LotteryPanel uiParent;

    /// <summary>
    /// Awake方法 - 在对象创建时调用
    /// 用于初始化UI组件引用
    /// </summary>
    private void Awake()
    {
        // 调用UI初始化方法，绑定UI组件引用
        InitUI();
    }

    /// <summary>
    /// 初始化UI组件引用
    /// 通过路径查找子物体，建立与UI元素的连接
    /// 
    /// UI层级结构：
    /// - Center/Image：物品图片位置
    /// - Bottom/Stars：星级显示区域
    /// </summary>
    void InitUI()
    {
        // 查找物品图片：位于当前物体的Center/Image子路径
        UIImage = transform.Find("Center/Image");

        // 查找星级区域：位于当前物体的Bottom/Stars子路径
        UIStars = transform.Find("Bottom/Stars");

        // UINew = transform.Find("Top/New");
        // UINew.gameObject.SetActive(false);
    }

    /// <summary>
    /// 初始化抽卡单元格
    /// 接收物品数据并更新UI显示
    /// </summary>
    /// <param name="item">物品实例数据（InventoryItem）</param>
    /// <param name="panel">父级抽卡面板引用，用于回调和交互</param>
    public void Init(InventoryItem item, LotteryPanel panel)
    {
        // ==================== 数据初始化 ====================

        // 保存物品数据引用
        this.inventoryItem = item;

        // 通过物品ID从GameManager获取物品配置数据
        this.itemData = GameDataConfig.Instance.ItemDataSo.GetItemDataById(item.itemId);

        // 保存父级面板引用
        this.uiParent = panel;

        // ==================== 刷新UI信息 ====================
        // 刷新物品星级背景图显示
        RefreshStartsBackGround();
        // 刷新物品图片显示
        RefreshImage();
        // 刷新星级显示
        RefreshStars();
    }

    /// <summary>
    /// 刷新物品图片显示
    /// 使用ItemData中的Sprite显示物品图片
    /// </summary>
    private void RefreshImage()
    {
        if (itemData != null && itemData.itemSprite != null)
        {
            UIImage.GetComponent<Image>().sprite = itemData.DrawCardWeponSprite;
        }
    }

    /// <summary>
    /// 刷新星级显示
    /// 根据物品的品质转换星级并控制显示
    /// 
    /// 品质与星级转换：
    /// - 普通 → 2星
    /// - 稀有 → 3星
    /// - 史诗 → 4星
    /// - 传说 → 5星
    /// </summary>
    public void RefreshStars()
    {
        // 将品质转换为星级
        int starCount = ConvertQualityToStar(this.inventoryItem.quantity);

        // 遍历所有星级图标子物体
        for (int i = 0; i < UIStars.childCount; i++)
        {
            // 获取第i个星级图标
            Transform star = UIStars.GetChild(i);

            // 判断该星级是否应该显示
            // i从0开始，所以当 i < starCount 时显示
            if (starCount > i)
            {
                star.gameObject.SetActive(true);
            }
            else
            {
                star.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 将品质转换为星级
    /// </summary>
    private int ConvertQualityToStar(ItemQuality quality)
    {
        return quality switch
        {
            ItemQuality.传说 => 5,
            ItemQuality.史诗 => 4,
            ItemQuality.稀有 => 3,
            ItemQuality.普通 => 2,
            _ => 1
        };
    }

    /// <summary>
    /// 刷新物品星级背景图显示
    /// 根据品质加载对应的背景图
    /// </summary>
    public void RefreshStartsBackGround()
    {
        if (inventoryItem == null || itemData == null) return;

        int starCount = ConvertQualityToStar(this.inventoryItem.quantity);
        Sprite bg = Resources.Load<Sprite>("Image/StartBackGround/Big" + starCount);
        if (bg != null)
        {
            this.gameObject.GetComponent<Image>().sprite = bg;
        }
    }
}
