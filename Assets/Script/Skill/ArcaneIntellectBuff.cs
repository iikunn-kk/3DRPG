using UnityEngine;

[System.Obsolete("ArcaneIntellectBuff 已弃用。请改用 ArcaneIntellectSkill + ArcaneIntellectRuntime 和 CharacterBuffs 来管理 Buff。")]
public class ArcaneIntellectBuff : MonoBehaviour
{
    private void Awake()
    {
        Debug.LogWarning("ArcaneIntellectBuff 已弃用，正在自动移除。请使用 ArcaneIntellectSkill + ArcaneIntellectRuntime。");
        Destroy(this);
    }
}
