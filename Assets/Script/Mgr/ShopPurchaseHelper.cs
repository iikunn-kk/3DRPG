using System.Linq;
using UnityEngine;

/// <summary>
/// 统一处理世界商店与 NPC 商店购买次数的读取与写入（持久化通过 CharacterData）
/// </summary>
public static class ShopPurchaseHelper
{
    private static CharacterData CurrentCharacter => GameManager.Instance?.CurrentCharacter;

    #region 初始化防御
    private static void EnsureLists()
    {
        if (CurrentCharacter == null) return;
        CurrentCharacter.worldShopPurchases ??= new System.Collections.Generic.List<ShopPurchaseRecord>();
        CurrentCharacter.npcShopPurchases ??= new System.Collections.Generic.List<NpcShopPurchaseRecord>();
    }
    #endregion

    #region 世界商店
    public static int GetWorldShopPurchasedCount(int itemId)
    {
        EnsureLists();
        if (CurrentCharacter == null) return 0;
        return CurrentCharacter.worldShopPurchases.FirstOrDefault(r => r.itemId == itemId)?.purchased ?? 0;
    }

    public static void AddWorldShopPurchasedCount(int itemId, int quantity)
    {
        if (quantity <= 0) return;
        EnsureLists();
        if (CurrentCharacter == null) return;
        var rec = CurrentCharacter.worldShopPurchases.FirstOrDefault(r => r.itemId == itemId);
        if (rec == null)
        {
            rec = new ShopPurchaseRecord { itemId = itemId, purchased = quantity };
            CurrentCharacter.worldShopPurchases.Add(rec);
        }
        else
        {
            rec.purchased += quantity;
        }
        SaveCoordinator.Instance.SaveCurrentCharacterData();
    }
    #endregion

    #region NPC 商店
    public static int GetNpcShopPurchasedCount(int npcId, int itemId)
    {
        EnsureLists();
        if (CurrentCharacter == null) return 0;
        return CurrentCharacter.npcShopPurchases.FirstOrDefault(r => r.npcId == npcId && r.itemId == itemId)?.purchased ?? 0;
    }

    public static void AddNpcShopPurchasedCount(int npcId, int itemId, int quantity)
    {
        if (quantity <= 0) return;
        EnsureLists();
        if (CurrentCharacter == null) return;
        var rec = CurrentCharacter.npcShopPurchases.FirstOrDefault(r => r.npcId == npcId && r.itemId == itemId);
        if (rec == null)
        {
            rec = new NpcShopPurchaseRecord { npcId = npcId, itemId = itemId, purchased = quantity };
            CurrentCharacter.npcShopPurchases.Add(rec);
        }
        else
        {
            rec.purchased += quantity;
        }
        SaveCoordinator.Instance.SaveCurrentCharacterData();
    }
    #endregion

    #region 剩余次数计算
    public static int GetRemaining(int configLimit, int purchased)
    {
        if (configLimit < 0) return -1; // 无限
        int remain = configLimit - purchased;
        return remain < 0 ? 0 : remain;
    }
    #endregion
}

