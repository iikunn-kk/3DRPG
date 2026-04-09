using UnityEngine;

/// <summary>
/// Simple in-scene helper to test monster damage number popups.
/// Attach to any GameObject and assign a MonsterCombat in the inspector.
/// Press keys P (physical), M (magic), H (heal) to apply damage/heal.
/// </summary>
public class MonsterDamageTest : MonoBehaviour
{
    public MonsterCombat targetMonster;
    [Tooltip("Damage amount for P/M keys, heal amount for H key")]
    public int amount = 50;

    void Update()
    {
        if (targetMonster == null) return;

        if (Input.GetKeyDown(KeyCode.P))
        {
            targetMonster.TakeDamage(amount, AttackType.物理攻击);
            Debug.Log($"Applied {amount} physical damage to {targetMonster.name}");
        }
        if (Input.GetKeyDown(KeyCode.M))
        {
            targetMonster.TakeDamage(amount, AttackType.魔法攻击);
            Debug.Log($"Applied {amount} magic damage to {targetMonster.name}");
        }
        if (Input.GetKeyDown(KeyCode.H))
        {
            targetMonster.TakeDamage(amount, AttackType.回血技能);
            Debug.Log($"Applied {amount} heal to {targetMonster.name}");
        }
    }
}

