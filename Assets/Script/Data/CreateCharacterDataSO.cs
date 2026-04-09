using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "选择职业数据", menuName = "Data/创建角色数据")]
public class CreateCharacterDataSO :ScriptableObject
{
    [Header("创建角色数据")]
    public List<CreateCharacterData> characterData;
}
[Serializable]
public class CreateCharacterData
{
    public CharacterProfession profession;
    public Sprite titleImage;
    [Header("描述")]
    public string description;
    [Header("操作难度"), Range(0, 5)]
    public int difficultyOfOperation;
    [Header("物理攻击力"), Range(0, 5)]
    public int physicalAttackValue;
    [Header("魔法攻击力"), Range(0, 5)]
    public int magicAttackValue;
    [Header("防御力"), Range(0, 5)]
    public int defenseValue;
 
}