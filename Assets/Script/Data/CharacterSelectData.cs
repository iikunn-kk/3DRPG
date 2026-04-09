using System;
using UnityEngine;

[Serializable]
public class CharacterSelectData 
{
    [Header("职业")] public CharacterProfession job;
    [Header("对应的模型")] public GameObject showObj;
    [Header("游戏中的模型")] public GameObject model;
    [Header("对应的图片")] public RenderTexture texture;
}