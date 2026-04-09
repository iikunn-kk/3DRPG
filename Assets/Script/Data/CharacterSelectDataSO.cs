using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterSelectData", menuName = "Data/选择角色界面的数据")]
public class CharacterSelectDataSO:ScriptableObject
{
    [Header("所有的选择角色界面的数据文件集合")]
    public List<CharacterSelectData> data;
}
