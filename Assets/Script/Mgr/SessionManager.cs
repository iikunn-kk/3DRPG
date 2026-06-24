using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// 会话管理器 - 管理当前游戏会话的核心数据
/// 职责：
/// 1. 当前角色数据 CurrentCharacter
/// 2. 玩家登录数据 PlayerLoginData
/// 3. SetCurrentCharacterData 初始化协调
/// </summary>
public class SessionManager : Singleton<SessionManager>
{
    /// <summary>
    /// 当前选中的角色数据
    /// 在角色选择界面设置，包含角色的基础属性、等级、位置等信息
    /// </summary>
    public CharacterData CurrentCharacter { get; private set; }

    /// <summary>
    /// 玩家登录数据
    /// 记录玩家的登录信息、会话状态等
    /// </summary>
    public PlayerLoginData PlayerLoginData { get; private set; }

    /// <summary>
    /// 设置当前角色数据
    /// 在角色选择后调用，初始化角色相关的各个系统
    /// </summary>
    /// <param name="character">角色数据对象</param>
    public void SetCurrentCharacterData(CharacterData character)
    {
        CurrentCharacter = character;

        // 确保购买记录列表存在（兼容旧存档）
        EnsurePurchaseRecordLists();

        // 初始化背包系统
        InventoryManager.Instance.Initialize(character.Id);

        // 从角色数据初始化货币系统
        CharacterService.Instance.InitializeFromCharacterData(character);

        // 可选：异步创建并保存角色数据到MongoDB
        _ = MongoDBManager.Instance.CreateAndSaveCharacterData(character);
    }

    /// <summary>
    /// 确保购买记录列表存在
    /// 用于兼容旧存档，如果记录列表为null则初始化空列表
    /// </summary>
    private void EnsurePurchaseRecordLists()
    {
        if (CurrentCharacter == null) return;

        CurrentCharacter.worldShopPurchases ??= new List<ShopPurchaseRecord>();
        CurrentCharacter.npcShopPurchases ??= new List<NpcShopPurchaseRecord>();
    }

    /// <summary>
    /// 设置玩家登录数据
    /// </summary>
    public void SetPlayerLoginData(PlayerLoginData data)
    {
        PlayerLoginData = data;
    }
}
