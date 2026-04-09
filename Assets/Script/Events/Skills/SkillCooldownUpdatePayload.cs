using System;

[Serializable]
public struct SkillCooldownUpdatePayload
{
    public string SkillID;
    public float Remaining;
    public float Total;
}