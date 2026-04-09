using System;
using System.Collections.Generic;
using UnityEngine;
[Serializable]
public class PlayerCharacterStateBaseData
{
   [SerializeField] public CharacterProfession profession;
   [SerializeField] public Sprite proHeadIcon;
   [Header("初始技能配置（该职业默认拥有的技能SO）")]
   [SerializeField] public List<SkillSO> defaultSkills = new List<SkillSO>();
   [SerializeField]private int baseMaxHp=50;
   [SerializeField]private int HpLevelProgress=20;
   [SerializeField]private int BaseExp=50;
   [SerializeField]private float ExperienceGrowthIndex=1.2f; 
   [SerializeField]private int BaseAttack=10;
   [SerializeField]private int AttackLevelProgress=5;
   [SerializeField]private int BaseDefence=7;
   [SerializeField]private int DefenceLevelProgress=3;
   public float Speed { get ; private set; } = 10;
   [SerializeField] private int baseRegenHp=10;
   [SerializeField] private int regenHpLevelProgress=1;
   public int GetMaxHp(int levelNumber)
   {
      return (baseMaxHp + (levelNumber * HpLevelProgress));
   }
   public int GetAttack(int levelNumber)
   {
      return (BaseAttack + (levelNumber * AttackLevelProgress));
   }
   public int GetDefence(int levelNumber)
   {
      return (BaseDefence + (levelNumber * DefenceLevelProgress));
   }
   public int GetRegenHp(int levelNumber)
   {
      return (baseRegenHp + (levelNumber * regenHpLevelProgress));
   }
   public int GetNeedExp(int levelNumber)
   {
      return (int)(BaseExp * Mathf.Pow(ExperienceGrowthIndex, levelNumber-1));
   }
}
