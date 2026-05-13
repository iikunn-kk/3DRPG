using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

/// <summary>
/// 存档协调器 - 统一管理角色数据保存
/// 职责：
/// 1. 汇总角色数据（等级、经验、位置、货币、任务、技能）
/// 2. 安全场景名写入
/// 3. 调用 MongoDBManager 持久化
/// </summary>
public class SaveCoordinator : Singleton<SaveCoordinator>
{
    /// <summary>
    /// 汇总当前运行时的所有角色数据并保存（统一入口）
    /// </summary>
    public async void SaveCurrentCharacterData()
    {
        try
        {
            var character = SessionManager.Instance.CurrentCharacter;
            if (character == null) return;

            var cs = CharacterRuntimeManager.Instance.CurrentPlayerCharacter();
            if (cs != null)
            {
                character.exp = cs.Exp;
                character.level = cs.Level;
                character.position = cs.transform.position;
                character.hp = cs.MaxHealth;
            }

            if (PlayerCurrencyManager.Instance != null)
            {
                character.gold = PlayerCurrencyManager.Instance.Money;
                character.gem = PlayerCurrencyManager.Instance.Diamonds;
            }

            TaskManager.Instance?.PopulateCharacterDataTasks(character);
            SkillManager.Instance?.PopulateCharacterDataSkills(character);

            var safeSceneName = GetSafeGameplaySceneName();
            if (!string.IsNullOrEmpty(safeSceneName))
            {
                character.currentScene = safeSceneName;
            }

            await MongoDBManager.Instance.CreateAndSaveCharacterData(character);
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveCoordinator] SaveCurrentCharacterData 异常: " + e);
        }
    }

    /// <summary>
    /// 获取可安全写入存档的游戏场景名称
    /// </summary>
    public string GetSafeGameplaySceneName()
    {
        string name = null;

        var slm = SceneLoadManager.Instance;
        if (slm != null)
        {
            name = slm.CurrentSceneName;
        }

        if (string.IsNullOrEmpty(name))
        {
            name = SceneManager.GetActiveScene().name;
        }

        if (string.IsNullOrEmpty(name)) return null;
        if (name == "LoginScene" || name == "LoadingScene") return null;

        return name;
    }
}
