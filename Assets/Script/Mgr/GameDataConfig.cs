using UnityEngine;

/// <summary>
/// 游戏数据配置管理器 - 集中管理所有 ScriptableObject 数据引用
/// 职责：
/// 1. 持有需 Inspector 赋值的 ScriptableObject 引用（替代 GameManager 中的 SO 字段）
/// 2. 提供统一的静态数据访问入口
/// 
/// 使用方式：
/// - 场景中新建 GameObject，挂载此脚本，在 Inspector 中拖入对应的 SO 资源
/// - 代码通过 GameDataConfig.Instance.xxx 访问
/// </summary>
public class GameDataConfig : Singleton<GameDataConfig>
{
    protected override void OnSingletonAwake()
    {
        Application.targetFrameRate = 60;
    }

    /// <summary>
    /// 物品/背包数据配置
    /// </summary>
    [SerializeField] private ItemDataSO itemDataSo;
    public ItemDataSO ItemDataSo => itemDataSo;

    /// <summary>
    /// 角色状态/职业基础数据配置
    /// </summary>
    [SerializeField] private PlayerCharacterStateDataSO playerCharacterStateDataSo;
    public PlayerCharacterStateDataSO PlayerCharacterStateDataSo => playerCharacterStateDataSo;

    /// <summary>
    /// 属性缩放数据配置
    /// </summary>
    [SerializeField] private PropertyScalingDataSO propertyScalingDataSo;
    public PropertyScalingDataSO PropertyScalingData => propertyScalingDataSo;

    /// <summary>
    /// 角色选择数据配置（旧系统兼容）
    /// </summary>
    [SerializeField] private CharacterSelectDataSO characterSelectDataSo;
    public CharacterSelectDataSO CharacterSelectDataSo => characterSelectDataSo;
}
