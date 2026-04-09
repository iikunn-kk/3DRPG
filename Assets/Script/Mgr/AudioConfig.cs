using UnityEngine;
using System;
using System.Collections.Generic;

// ===== 枚举定义 (提前放置以避免解析工具顺序问题) =====
public enum BGMType
{
    登录界面,
    场景1,
    场景2
}

public enum SkillSoundType
{
    奥术射线发射,
    奥术飞弹发射,
    奥术飞弹击中,
    Buff释放,
    闪电护盾释放,
    三段踢攻击到敌人,
    三段踢释放,
}

public enum MonsterSoundType
{
    攻击,
    死亡,
}

public enum PlayerSoundType
{
    玩家死亡,
}

public enum UISoundType
{
    打开面板,
    关闭面板,
    按下按钮,
    装备新装备,
    卸下装备,
    光标划过物品栏,
    打开地图,
    获得金钱,
    获得钻石,
    确认
}

[CreateAssetMenu(fileName = "AudioConfig", menuName = "AudioConfig/AudioConfig")]
public class AudioConfig : ScriptableObject
{
    [Header("全局音频设置")]
    [Tooltip("所有音效的默认冷却时间(秒)")]
    public float defaultCooldown = 0.1f;
    
    [Header("BGM配置")]
    public List<AudioEntry<BGMType>> bgmList = new List<AudioEntry<BGMType>>();
    
    [Header("技能音效配置")]
    public List<AudioEntry<SkillSoundType>> skillSoundList = new List<AudioEntry<SkillSoundType>>();
    
    [Header("怪物音效配置")]
    public List<AudioEntry<MonsterSoundType>> monsterSoundList = new List<AudioEntry<MonsterSoundType>>();
    
    [Header("玩家音效配置")]
    public List<AudioEntry<PlayerSoundType>> playerSoundList = new List<AudioEntry<PlayerSoundType>>();
    
    [Header("UI音效配置")]
    public List<AudioEntry<UISoundType>> uiSoundList = new List<AudioEntry<UISoundType>>();

    // 直接引用 AudioClip
    [Serializable]
    public class AudioEntry<T> where T : Enum
    {
        public T soundType;
        public AudioClip audioClip; 
    }

    // 查找方法
    public AudioClip GetBGMClip(BGMType type) => bgmList.Find(x => x.soundType.Equals(type))?.audioClip;
    public AudioClip GetWeaponSoundClip(SkillSoundType type) => skillSoundList.Find(x => x.soundType.Equals(type))?.audioClip;
    public AudioClip GetMonsterSoundClip(MonsterSoundType type) => monsterSoundList.Find(x => x.soundType.Equals(type))?.audioClip;
    public AudioClip GetPlayerSoundClip(PlayerSoundType type) => playerSoundList.Find(x => x.soundType.Equals(type))?.audioClip;
    public AudioClip GetUISoundClip(UISoundType type) => uiSoundList.Find(x => x.soundType.Equals(type))?.audioClip;
}
