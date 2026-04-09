using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "AllSkillsData", menuName = "ScriptableObjects/AllSkillsData")]
public class AllSkillsSO : ScriptableObject
{
    [Header("所有技能列表")]
    public List<SkillSO> allSkills=new ();
}