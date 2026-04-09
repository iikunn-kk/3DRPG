using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "New SkillCDEventSO", menuName = "Events/SkillCDEventSO")]
public class SkillCDEventSO : BaseEventSO<SkillCdData>
{
    
}

public struct SkillCdData
{
    public string SkillID;
    public float ColdDown;

    public SkillCdData(string skillID, float coldDown)
    {
        SkillID = skillID;
        ColdDown = coldDown;
    }
}