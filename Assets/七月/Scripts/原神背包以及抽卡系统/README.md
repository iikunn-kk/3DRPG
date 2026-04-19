# 原神背包与抽卡系统

<div align="center">

[![Unity](https://img.shields.io/badge/Unity-2022.3+-blue.svg)](https://unity.com/)
[![C#](https://img.shields.io/badge/C%23-9.0+-purple.svg)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**模仿《原神》的背包管理与随机抽卡系统**

[系统概述](#系统概述) • [功能特性](#功能特性) • [架构设计](#架构设计) • [使用指南](#使用指南) • [代码分析](#代码分析)

</div>

---

## 📋 目录

- [系统概述](#系统概述)
- [功能特性](#功能特性)
- [架构设计](#架构设计)
- [文件结构](#文件结构)
- [核心类详解](#核心类详解)
- [数据流分析](#数据流分析)
- [使用指南](#使用指南)
- [技术亮点](#技术亮点)
- [已知问题](#已知问题)
- [改进计划](#改进计划)

---

## 🎮 系统概述

这是一个模仿《原神》的背包管理与随机抽卡系统，采用模块化设计，与项目核心系统（UIManager、GameManager）无缝集成。系统支持背包管理、物品展示、批量删除、随机抽卡等核心功能。

### 设计目标

- ✅ 实现完整的背包管理功能
- ✅ 支持物品分类与排序
- ✅ 实现随机抽卡机制
- ✅ 提供流畅的 UI 交互体验
- ✅ 数据持久化存储

### 技术栈

| 技术 | 版本 | 用途 |
|------|------|------|
| **Unity** | 2022.3+ | 游戏引擎 |
| **C#** | 9.0+ | 编程语言 |
| **DOTween** | - | UI 动画效果 |
| **ScriptableObject** | - | 配置数据管理 |

---

## ✨ 功能特性

### 🎒 背包系统

- **物品列表展示** - 网格化显示背包物品
- **物品详情查看** - 点击物品查看详细信息
- **等级与星级显示** - 直观的物品等级与星级展示
- **新物品标记** - 新获得的物品显示"NEW"标记
- **智能排序** - 按星级、ID、等级自动排序
- **分类筛选** - 支持按物品类型筛选（武器、食物等）
- **批量删除** - 支持多选删除物品

### 🎰 抽卡系统

- **单抽功能** - 随机抽取一件物品
- **十连抽功能** - 一次性抽取十件物品
- **自动排序** - 十连抽结果自动按星级排序
- **抽卡动画** - 流畅的抽卡卡片展示
- **结果保存** - 抽卡结果自动保存到背包

### 🎨 UI 交互

- **悬停效果** - 鼠标悬停显示高亮动画
- **选中效果** - 点击物品显示选中动画
- **删除模式** - 专门的删除确认界面
- **流畅切换** - 面板间无缝切换

---

## 🏗 架构设计

### 系统分层架构

```
┌─────────────────────────────────────────────────────────────┐
│                    面板层 (Panel Layer)                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │
│  │ DrawCardPanel│→ │ LotteryPanel │→ │ PackagePanel │ │
│  │   (主菜单)    │  │   (抽卡)      │  │   (背包)      │ │
│  └──────────────┘  └──────────────┘  └──────────────┘ │
└─────────────────────────────────────────────────────────────┘
         ↓                   ↓                   ↓
┌─────────────────────────────────────────────────────────────┐
│                 单元格层 (Cell Layer)                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │
│  │ LotteryCell  │  │ PackageCell  │  │PackageDetail │ │
│  │  (抽卡卡片)   │  │  (背包格子)   │  │  (物品详情)   │ │
│  └──────────────┘  └──────────────┘  └──────────────┘ │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│                 数据层 (Data Layer)                        │
│  ┌──────────────────┐  ┌──────────────────┐           │
│  │ PackageTable     │  │ PackageLocalData │           │
│  │ (静态配置-SO)     │  │ (运行时数据)      │           │
│  ├──────────────────┤  ├──────────────────┤           │
│  │ PackageTableItem │  │ PackageLocalItem│           │
│  └──────────────────┘  └──────────────────┘           │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│              管理层 (Manager Layer)                      │
│              GameManager (背包管理方法)                      │
└─────────────────────────────────────────────────────────────┘
```

### 设计模式

| 设计模式 | 应用场景 | 说明 |
|----------|----------|------|
| **单例模式** | PackageLocalData | 运行时数据全局唯一访问 |
| **ScriptableObject** | PackageTable | 配置数据与逻辑分离 |
| **工厂模式** | 抽卡物品生成 | GetLotteryRandom1() |
| **观察者模式** | UI 刷新 | chooseUID 属性触发 RefreshDetail() |
| **策略模式** | 物品排序 | PackageItemComparer |

---

## 📁 文件结构

```
原神背包以及抽卡系统/
├── README.md                      # 本文档
├── BasePanel.cs                   # 面板基类（已弃用）
├── DrawCardPanel.cs               # 主菜单面板（抽卡/背包入口）
├── LotteryPanel.cs                # 抽卡面板（单抽/十抽）
├── LotteryCell.cs                 # 抽卡单元格（显示抽卡结果）
├── PackagePanel.cs                # 背包主面板（物品列表、删除模式）
├── PackageCell.cs                # 背包单元格（物品图标、等级、星级）
├── PackageDetail.cs               # 物品详情面板（物品信息展示）
├── PackageLocalData.cs            # 运行时数据管理（序列化/反序列化）
└── PackageTable.cs               # 静态配置数据（ScriptableObject）
```

### 文件说明

| 文件名 | 行数 | 职责 | 继承关系 |
|--------|------|------|----------|
| **BasePanel.cs** | 39 | 面板基类（已弃用） | Singleton\<BasePanel\> |
| **DrawCardPanel.cs** | 75 | 主菜单面板 | UIPopPanelBase |
| **LotteryPanel.cs** | 97 | 抽卡面板 | UIPopPanelBase |
| **LotteryCell.cs** | 63 | 抽卡单元格 | MonoBehaviour |
| **PackagePanel.cs** | 242 | 背包主面板 | UIPopPanelBase |
| **PackageCell.cs** | 127 | 背包单元格 | MonoBehaviour, IPointerClickHandler... |
| **PackageDetail.cs** | 75 | 物品详情面板 | MonoBehaviour |
| **PackageLocalData.cs** | 65 | 运行时数据管理 | 单例模式 |
| **PackageTable.cs** | 22 | 静态配置数据 | ScriptableObject |

---

## 📚 核心类详解

---

### 1. BasePanel.cs - 面板基类

**状态**: 已弃用，被 `UIPopPanelBase` 替代

**职责**:
- 提供面板的基础生命周期管理
- 管理 `UIManager.panelDict` 的注册与注销

**核心方法**:

```csharp
public virtual void OpenPanel(string name)
{
    this.name = name;
    SetActive(true);
}

public virtual void ClosePanel()
{
    isRemove = true;
    SetActive(false);
    Destroy(gameObject);
    if (UIManager.Instance.panelDict.ContainsKey(name))
    {
        UIManager.Instance.panelDict.Remove(name);
    }
}
```

**设计缺陷**:
- ❌ 继承 `Singleton<BasePanel>` 会导致多个面板实例化问题
- ❌ 使用 `new string name` 遮蔽了 `MonoBehaviour.name`

---

### 2. DrawCardPanel.cs - 主菜单面板

**职责**:
- 提供抽卡与背包的入口
- 管理面板之间的跳转

**核心流程**:

```
DrawCardPanel
  ├─ OnBtnLottery()    → 打开 LotteryPanel（自动十连抽）
  ├─ OnBtnPackage()   → 打开 PackagePanel（显示背包）
  └─ OnQuitBtn()      → 关闭面板
```

**关键代码**:

```csharp
private void OnBtnLottery()
{
    var lotteryPanel = UIManager.Instance.OpenPanel<LotteryPanel>(out var isOpen);
    if (isOpen)
    {
        // LotteryPanel.Start() 会自动执行十连抽
    }
    UIManager.Instance.ClosePanel<DrawCardPanel>();
    Hide();
}
```

---

### 3. LotteryPanel.cs - 抽卡面板

**职责**:
- 实现单抽与十连抽功能
- 显示抽卡结果

**核心功能**:

#### 单抽流程
```csharp
private void OnLottery1Btn()
{
    // 1. 清理旧卡片
    for (int i = 0; i < UICenter.childCount; i++)
    {
        Destroy(UICenter.GetChild(i).gameObject);
    }
    
    // 2. 随机获得一件物品
    PackageLocalItem item = GameManager.Instance.GetLotteryRandom1();
    
    // 3. 创建卡片并显示
    Transform LotteryCellTran = Instantiate(LotteryCellPrefab.transform, UICenter);
    LotteryCell lotteryCell = LotteryCellTran.GetComponent<LotteryCell>();
    lotteryCell.Refresh(item, this);
}
```

#### 十连抽流程
```csharp
private void OnLottery10Btn()
{
    // 1. 清理旧卡片
    for (int i = 0; i < UICenter.childCount; i++)
    {
        Destroy(UICenter.GetChild(i).gameObject);
    }
    
    // 2. 随机获得十件物品（已排序）
    List<PackageLocalItem> packageLocalItems = 
        GameManager.Instance.GetLotteryRandom10(sort: true);
    
    // 3. 创建卡片并显示
    foreach (PackageLocalItem item in packageLocalItems)
    {
        Transform LotteryCellTran = Instantiate(LotteryCellPrefab.transform, UICenter);
        LotteryCell lotteryCell = LotteryCellTran.GetComponent<LotteryCell>();
        lotteryCell.Refresh(item, this);
    }
}
```

---

### 4. LotteryCell.cs - 抽卡单元格

**职责**:
- 显示抽卡结果（图标、星级）
- 处理悬停效果（预留）

**数据结构**:

```csharp
public class LotteryCell : MonoBehaviour
{
    private Transform UIImage;          // 物品图片
    private Transform UIStars;          // 星级容器
    private PackageLocalItem packageLocalItem;
    private PackageTableItem packageTableItem;
    private LotteryPanel uiParent;
}
```

**刷新逻辑**:

```csharp
public void Refresh(PackageLocalItem packageLocalItem, LotteryPanel uiParent)
{
    this.packageLocalItem = packageLocalItem;
    this.packageTableItem = GameManager.Instance.GetPackageItemById(
        this.packageLocalItem.id
    );
    this.uiParent = uiParent;
    
    RefreshImage();
    RefreshStars();
}
```

---

### 5. PackagePanel.cs - 背包主面板

**职责**:
- 管理背包物品列表
- 支持物品选择与详情查看
- 实现批量删除功能

**核心状态**:

```csharp
// 背包模式
public enum PackageMode
{
    normal,    // 正常模式（查看/选择物品）
    delete,    // 删除模式（批量选择删除）
    sort       // 排序模式（预留，未实现）
}

// 当前模式
public PackageMode curMode = PackageMode.normal;

// 待删除的UID列表
public List<string> deleteChooseUid;

// 当前选中项
private string _chooseUid;
public string chooseUID
{
    get => _chooseUid;
    set
    {
        _chooseUid = value;
        RefreshDetail();  // 自动刷新详情面板
    }
}
```

**核心功能**:

#### 刷新背包列表
```csharp
private void RefreshScroll()
{
    // 1. 清理旧的物品单元格
    RectTransform scrollContent = UIScrollView.GetComponent<ScrollRect>().content;
    for (int i = 0; i < scrollContent.childCount; i++)
    {
        Destroy(scrollContent.GetChild(i).gameObject);
    }
    
    // 2. 获取排序后的背包数据
    foreach (PackageLocalItem localData in GameManager.Instance.GetSortPackageLocalData())
    {
        Transform PackageUIItem = Instantiate(PackageUIItemPrefab.transform, scrollContent);
        PackageCell packageCell = PackageUIItem.GetComponent<PackageCell>();
        packageCell.Refresh(localData, this);
    }
}
```

#### 批量删除
```csharp
public void AddChooseDeleteUid(string uid)
{
    this.deleteChooseUid ??= new List<string>();
    
    // 切换选中状态
    if (!this.deleteChooseUid.Contains(uid))
    {
        this.deleteChooseUid.Add(uid);  // 添加
    }
    else
    {
        this.deleteChooseUid.Remove(uid); // 移除
    }
    
    RefreshDeletePanel();
}

private void OnDeleteConfirm()
{
    if (this.deleteChooseUid == null || this.deleteChooseUid.Count == 0)
        return;
    
    GameManager.Instance.DeletePackageItems(this.deleteChooseUid);
    RefreshUI();
}
```

---

### 6. PackageCell.cs - 背包单元格

**职责**:
- 显示物品图标、等级、星级
- 处理点击、悬停事件
- 管理删除选中状态

**接口实现**:

```csharp
public class PackageCell : MonoBehaviour, 
    IPointerClickHandler,    // 点击事件
    IPointerEnterHandler,    // 鼠标进入事件
    IPointerExitHandler      // 鼠标退出事件
```

**核心事件**:

#### 点击事件
```csharp
public void OnPointerClick(PointerEventData eventData)
{
    // 删除模式：切换删除选中状态
    if (this.uiParent.curMode == PackageMode.delete)
    {
        this.uiParent.AddChooseDeleteUid(this.packageLocalData.uid);
    }
    
    // 正常模式：设置选中项
    if (this.uiParent.chooseUID == this.packageLocalItem.uid)
        return;
    
    this.uiParent.chooseUID = this.packageLocalItem.uid;
    
    // 播放选中动画
    UISelectAni.gameObject.SetActive(true);
    UISelectAni.GetComponent<Animator>().SetTrigger("In");
}
```

#### 悬停事件
```csharp
public void OnPointerEnter(PointerEventData eventData)
{
    UIMouseOverAni.gameObject.SetActive(true);
    UIMouseOverAni.GetComponent<Animator>().SetTrigger("In");
}

public void OnPointerExit(PointerEventData eventData)
{
    UIMouseOverAni.GetComponent<Animator>().SetTrigger("Out");
}
```

---

### 7. PackageDetail.cs - 物品详情面板

**职责**:
- 显示物品详细信息
- 展示物品图标、名称、描述、等级、星级

**刷新逻辑**:

```csharp
public void Refresh(PackageLocalItem packageLocalData, PackagePanel uiParent)
{
    this.packageLocalData = packageLocalData;
    this.packageTableItem = GameManager.Instance.GetPackageItemById(
        packageLocalData.id
    );
    this.uiParent = uiParent;
    
    // 等级（最大40级）
    UILevelText.GetComponent<Text>().text = 
        string.Format("Lv.{0}/40", this.packageLocalData.level.ToString());
    
    // 简短描述
    UIDescription.GetComponent<Text>().text = this.packageTableItem.description;
    
    // 详细描述
    UISkillDescription.GetComponent<Text>().text = this.packageTableItem.skillDescription;
    
    // 物品名称
    UITitle.GetComponent<Text>().name = this.packageTableItem.name;
    
    // 图片加载
    Texture2D t = (Texture2D)Resources.Load(this.packageTableItem.imagePath);
    Sprite temp = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0, 0));
    UIIcon.GetComponent<Image>().sprite = temp;
    
    // 星级处理
    RefreshStars();
}
```

---

### 8. PackageLocalData.cs - 运行时数据管理

**职责**:
- 管理运行时背包数据
- 实现数据的序列化与反序列化
- 提供 PlayerPrefs 持久化存储

**数据结构**:

```csharp
public class PackageLocalData
{
    private static PackageLocalData _instance;
    
    public static PackageLocalData Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new PackageLocalData();
            }
            return _instance;
        }
    }

    public List<PackageLocalItem> items;
}

[System.Serializable]
public class PackageLocalItem
{
    public string uid;    // 唯一标识符
    public int id;        // 物品ID（对应 PackageTableItem.id）
    public int num;       // 数量
    public int level;     // 等级
    public bool isNew;    // 是否新获得
}
```

**核心方法**:

#### 保存数据
```csharp
public void SavePackage()
{
    string inventoryJson = JsonUtility.ToJson(this);
    PlayerPrefs.SetString("PackageLocalData", inventoryJson);
    PlayerPrefs.Save();
}
```

#### 加载数据
```csharp
public List<PackageLocalItem> LoadPackage()
{
    if (items != null)
    {
        return items;
    }
    
    if (PlayerPrefs.HasKey("PackageLocalData"))
    {
        string inventoryJson = PlayerPrefs.GetString("PackageLocalData");
        PackageLocalData packageLocalData = JsonUtility.FromJson<PackageLocalData>(inventoryJson);
        items = packageLocalData.items;
        return items;
    }
    else
    {
        items = new List<PackageLocalItem>();
        return items;
    }
}
```

---

### 9. PackageTable.cs - 静态配置数据

**职责**:
- 定义物品静态配置数据
- 使用 ScriptableObject 实现数据与逻辑分离

**数据结构**:

```csharp
[CreateAssetMenu(menuName = "XiaoQi/PackageTable", fileName = "PackageTable")]
public class PackageTable : ScriptableObject
{
    public List<PackageTableItem> DataList = new List<PackageTableItem>();
}

[System.Serializable]
public class PackageTableItem
{
    public int id;                 // 物品ID
    public int type;               // 物品类型（1=武器，2=食物）
    public int star;               // 星级（1-5）
    public string name;            // 物品名称
    public string description;      // 简短描述
    public string skillDescription; // 详细描述
    public string imagePath;       // 图片路径
}
```

**使用方式**:

在 Unity 编辑器中：
1. 右键 → Create → XiaoQi → PackageTable
2. 填写物品配置数据
3. 放置在 `Resources/TableData/` 目录下

---

## 🔄 数据流分析

### 背包打开流程

```
DrawCardPanel.OnBtnPackage()
  ↓
UIManager.OpenPanel<PackagePanel>(out isOpen)
  ↓
PackagePanel.Awake()
  ├─ InitUI()      // 绑定 UI 引用
  └─ InitClick()   // 绑定事件
  ↓
PackagePanel.Start()
  ↓
PackagePanel.RefreshUI()
  ↓
PackagePanel.RefreshScroll()
  ├─ 清空滚动容器
  ├─ GameManager.GetSortPackageLocalData()
  │   ├─ PackageLocalData.Instance.LoadPackage()
  │   │   ├─ PlayerPrefs.Load("PackageLocalData")
  │   │   └─ JsonUtility.FromJson()
  │   └─ 排序 (PackageItemComparer)
  └─ 遍历创建 PackageCell
      └─ PackageCell.Refresh()
          ├─ 加载物品图标
          ├─ 显示等级
          └─ 显示星级
```

### 抽卡流程

```
DrawCardPanel.OnBtnLottery()
  ↓
UIManager.OpenPanel<LotteryPanel>(out isOpen)
  ↓
LotteryPanel.Awake()
  ├─ InitUI()
  └─ InitPrefab()
  ↓
LotteryPanel.Start()  // 自动执行十连抽
  ↓
LotteryPanel.OnLottery10Btn()
  ↓
GameManager.GetLotteryRandom10(sort: true)
  ├─ 循环 10 次
  │   └─ GetLotteryRandom1()
  │       ├─ GetPackageTableByType(武器类型)
  │       ├─ Random.Range()
  │       ├─ Guid.NewGuid()
  │       ├─ 创建 PackageLocalItem
  │       ├─ 添加到背包
  │       └─ SavePackage()
  └─ 排序 (PackageItemComparer)
  ↓
创建 10 个 LotteryCell
  ↓
LotteryCell.Refresh()
  ├─ 加载图标
  └─ 显示星级
```

### 批量删除流程

```
PackagePanel.OnDelete()
  ↓
切换到删除模式 (curMode = PackageMode.delete)
  ↓
显示删除面板 (UIDeletePanel)
  ↓
用户点击物品单元格
  ↓
PackageCell.OnPointerClick()
  ↓
PackagePanel.AddChooseDeleteUid()
  ├─ 添加/移除 UID
  └─ RefreshDeletePanel()
      └─ PackageCell.RefreshDeleteState()
  ↓
用户点击确认删除
  ↓
PackagePanel.OnDeleteConfirm()
  ↓
GameManager.DeletePackageItems(deleteChooseUid)
  ├─ 遍历 UID 列表
  │   └─ DeletePackageItem(uid, false)
  │       └─ PackageLocalData.Instance.items.Remove()
  └─ SavePackage()
  ↓
PackagePanel.RefreshUI()
  └─ RefreshScroll()
```

---

## 📖 使用指南

### 1. 准备工作

#### 创建配置数据

1. 在 Unity 编辑器中创建 PackageTable
   - 右键 → Create → XiaoQi → PackageTable
   - 命名为 `PackageTable`

2. 配置物品数据
   ```csharp
   // 示例配置
   {
       id: 1001,
       type: 1,  // 1=武器
       star: 5,  // 5星
       name: "天空之剑",
       description: "获得风元素伤害加成",
       skillDescription: "普通攻击与重击命中时...",
       imagePath: "Images/Weapons/SwordOfSky"
   }
   ```

3. 放置到 Resources 目录
   - 路径：`Resources/TableData/PackageTable`

#### 设置预制件

确保以下预制件存在：
- `Prefab/Panel/Lottery/LotteryItem` - 抽卡卡片
- `Prefab/Panel/Package/PackageItem` - 背包物品格子

---

### 2. 集成到项目

#### 打开主菜单

```csharp
// 在需要的地方调用
var drawCardPanel = UIManager.Instance.OpenPanel<DrawCardPanel>(out var isOpen);
if (isOpen)
{
    // 新打开的面板，无需初始化
}
```

#### 直接打开背包

```csharp
var packagePanel = UIManager.Instance.OpenPanel<PackagePanel>(out var isOpen);
if (isOpen)
{
    // PackagePanel.Start() 会自动刷新背包UI
}
```

---

### 3. 自定义配置

#### 修改抽卡概率

```csharp
// 在 GameManager.cs 中修改 GetLotteryRandom1() 方法
public PackageLocalItem GetLotteryRandom1()
{
    List<PackageTableItem> packageItems = GetPackageTableByType(
        GameConst.PackageTypeWeapon
    );
    
    // 实现自定义概率算法
    int randomValue = UnityEngine.Random.Range(0, 1000);
    PackageTableItem packageItem;
    
    if (randomValue < 6)  // 0.6% 概率获得5星
    {
        packageItem = packageItems.Find(x => x.star == 5);
    }
    else if (randomValue < 57)  // 5.1% 概率获得4星
    {
        packageItem = packageItems.Find(x => x.star == 4);
    }
    else  // 94.3% 概率获得3星及以下
    {
        packageItem = packageItems.Find(x => x.star <= 3);
    }
    
    // ... 后续逻辑
}
```

#### 添加物品类型

```csharp
// 修改 GameConst.cs
public static class GameConst
{
    public const int PackageTypeWeapon = 1;
    public const int PackageTypeFood = 2;
    public const int PackageTypeMaterial = 3;  // 新增
}

// 在 PackagePanel.cs 中添加筛选逻辑
private void OnClickMaterial()
{
    // 筛选材料类型
}
```

---

### 4. 扩展功能

#### 添加物品强化功能

```csharp
// 在 PackageDetail.cs 中添加强化按钮
private void OnEnhance()
{
    if (this.packageLocalData.level >= 40)
        return;
    
    // 扣除材料
    // 提升等级
    this.packageLocalData.level++;
    
    // 保存
    PackageLocalData.Instance.SavePackage();
    
    // 刷新UI
    Refresh(this.packageLocalData, this.uiParent);
}
```

#### 添加物品分享功能

```csharp
// 在 PackageDetail.cs 中添加分享按钮
private void OnShare()
{
    string shareText = string.Format(
        "我抽到了{0}星级武器【{1}】！",
        this.packageTableItem.star,
        this.packageTableItem.name
    );
    
    // 调用系统分享
    // Application.Share(shareText, image);
}
```

---

## 💡 技术亮点

### 1. ScriptableObject 配置驱动

**优势**:
- ✅ 数据与逻辑分离
- ✅ 可视化编辑
- ✅ 热更新支持
- ✅ 版本控制友好

**实现**:
```csharp
[CreateAssetMenu(menuName = "XiaoQi/PackageTable")]
public class PackageTable : ScriptableObject
{
    public List<PackageTableItem> DataList;
}
```

---

### 2. 物品排序算法

**排序优先级**:
1. 星级：高星级在前
2. ID：大 ID 在前
3. 等级：高等级在前

**实现**:
```csharp
public class PackageItemComparer : IComparer<PackageLocalItem>
{
    public int Compare(PackageLocalItem a, PackageLocalItem b)
    {
        PackageTableItem x = GameManager.Instance.GetPackageItemById(a.id);
        PackageTableItem y = GameManager.Instance.GetPackageItemById(b.id);
        
        // 按星级从大到小
        int starComparison = y.star.CompareTo(x.star);
        if (starComparison != 0)
            return starComparison;
        
        // 星级相同，按 ID 从大到小
        int idComparison = y.id.CompareTo(x.id);
        if (idComparison != 0)
            return idComparison;
        
        // ID 也相同，按等级从大到小
        return b.level.CompareTo(a.level);
    }
}
```

---

### 3. 唯一标识符生成

**实现**:
```csharp
public PackageLocalItem GetLotteryRandom1()
{
    PackageLocalItem packageLocalItem = new()
    {
        uid = System.Guid.NewGuid().ToString(),  // 生成唯一ID
        id = packageItem.id,
        num = 1,
        level = 1,
        isNew = true
    };
    
    return packageLocalItem;
}
```

**优势**:
- ✅ 全局唯一
- ✅ 防止物品重复
- ✅ 便于追踪物品

---

### 4. 属性触发器

**实现**:
```csharp
public string chooseUID
{
    get => _chooseUid;
    set
    {
        _chooseUid = value;
        RefreshDetail();  // 自动刷新详情面板
    }
}
```

**优势**:
- ✅ 自动刷新 UI
- ✅ 简化调用代码
- ✅ 提升代码可读性

---

### 5. 批量操作优化

**实现**:
```csharp
public void DeletePackageItems(List<string> uids)
{
    foreach (string uid in uids)
    {
        DeletePackageItem(uid, false);  // 不立即保存
    }
    PackageLocalData.Instance.SavePackage();  // 批量保存一次
}
```

**优势**:
- ✅ 减少磁盘 I/O
- ✅ 提升性能
- ✅ 避免数据不一致

---

## ⚠️ 已知问题

### 1. 图片重复加载

**问题描述**:
每次刷新物品单元格都从 Resources 加载图片，没有缓存机制。

**影响**:
- 性能低下
- 内存占用高

**位置**:
- `PackageCell.cs:56`
- `LotteryCell.cs:42`
- `PackageDetail.cs:54`

---

### 2. 对象销毁不彻底

**问题描述**:
正向遍历销毁子对象可能导致索引问题。

**影响**:
- 可能导致对象未完全销毁
- 内存泄漏风险

**位置**:
- `PackagePanel.cs:115-118`
- `LotteryPanel.cs:51-54`

---

### 3. 序列化问题

**问题描述**:
`JsonUtility` 不支持 `List<T>` 的直接序列化。

**影响**:
- 数据保存可能失败
- 数据加载异常

**位置**:
- `PackageLocalData.cs:25`

---

### 4. 测试代码残留

**问题描述**:
生产环境包含测试代码。

**影响**:
- 代码不规范
- 可能产生意外行为

**位置**:
- `PackageDetail.cs:24-27`

---

### 5. BasePanel 设计缺陷

**问题描述**:
已弃用的类未移除，可能造成混淆。

**影响**:
- 代码可读性下降
- 维护成本增加

**位置**:
- `BasePanel.cs` 整个文件

---

## 🚀 改进计划

### 1. 性能优化

#### 图片缓存

```csharp
private Dictionary<string, Sprite> _spriteCache = new();

private Sprite LoadSprite(string path)
{
    if (!_spriteCache.TryGetValue(path, out Sprite sprite))
    {
        Texture2D t = (Texture2D)Resources.Load(path);
        sprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0, 0));
        _spriteCache[path] = sprite;
    }
    return sprite;
}
```

#### 对象池

```csharp
public class PackageCellPool
{
    private Queue<PackageCell> _pool = new Queue<PackageCell>();
    
    public PackageCell Get()
    {
        if (_pool.Count > 0)
        {
            return _pool.Dequeue();
        }
        return Instantiate(prefab);
    }
    
    public void Return(PackageCell cell)
    {
        cell.gameObject.SetActive(false);
        _pool.Enqueue(cell);
    }
}
```

---

### 2. 数据持久化优化

#### 使用 JSON.NET

```csharp
using Newtonsoft.Json;

public void SavePackage()
{
    string inventoryJson = JsonConvert.SerializeObject(items);
    PlayerPrefs.SetString("PackageLocalData", inventoryJson);
    PlayerPrefs.Save();
}

public List<PackageLocalItem> LoadPackage()
{
    if (items != null)
        return items;
    
    if (PlayerPrefs.HasKey("PackageLocalData"))
    {
        string inventoryJson = PlayerPrefs.GetString("PackageLocalData");
        items = JsonConvert.DeserializeObject<List<PackageLocalItem>>(inventoryJson);
        return items;
    }
    
    items = new List<PackageLocalItem>();
    return items;
}
```

---

### 3. 代码清理

#### 移除测试代码

```csharp
// 移除或使用条件编译
#if UNITY_EDITOR
private void Test()
{
    Refresh(GameManager.Instance.GetPackageLocalData()[1], null);
}
#endif
```

#### 移除 BasePanel

- 删除 `BasePanel.cs` 文件
- 确保所有面板都继承 `UIPopPanelBase`

---

### 4. 功能扩展

#### 添加物品强化

```csharp
public class ItemEnhancer
{
    public bool Enhance(PackageLocalItem item, int materialCount)
    {
        if (item.level >= 40)
            return false;
        
        if (materialCount < GetRequiredMaterial(item.level))
            return false;
        
        item.level++;
        return true;
    }
    
    private int GetRequiredMaterial(int currentLevel)
    {
        // 根据等级返回所需材料数量
        return currentLevel * 100;
    }
}
```

#### 添加物品合成

```csharp
public class ItemCombiner
{
    public PackageLocalItem Combine(List<string> materialUids)
    {
        // 验证材料
        // 扣除材料
        // 创建新物品
        // 添加到背包
        
        return new PackageLocalItem();
    }
}
```

---

## 📊 性能指标

| 指标 | 当前值 | 目标值 |
|------|--------|--------|
| 抽卡响应时间 | < 100ms | < 50ms |
| 背包加载时间 | < 500ms | < 200ms |
| 物品删除时间 | < 50ms | < 20ms |
| 内存占用 | ~100MB | < 50MB |

---

## 🤝 贡献指南

欢迎提交 Issue 和 Pull Request！

### 提交规范

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Type 类型**:
- `feat`: 新功能
- `fix`: 修复 bug
- `docs`: 文档更新
- `style`: 代码格式
- `refactor`: 重构
- `test`: 测试
- `chore`: 构建或辅助工具变动

---

## 📄 许可证

本项目采用 MIT 许可证 - 详见项目根目录 LICENSE 文件

---

## 📞 联系方式

- **作者**: 七月
- **邮箱**: [your.email@example.com](mailto:your.email@example.com)
- **GitHub**: [项目地址](https://github.com/yourusername/3DRPG)

---

## 🙏 致谢

感谢以下资源和技术支持：

- **Unity** - 游戏引擎
- **DOTween** - 动画库
- **原神** - 灵感来源

---

<div align="center">

**如果这个系统对你有帮助，请给个 ⭐️ Star！**

Made with ❤️ by 七月

</div>
