using System.Collections.Generic;
using UnityEngine;
using System; // 添加 Obsolete 特性

[CreateAssetMenu(fileName = "MapData", menuName = "Data/地图配置")]
public class MapDataSO : ScriptableObject
{
    public List<MapRegionEntry> regions = new List<MapRegionEntry>();
    public MapRegionEntry GetRegion(string sceneName)
    {
        return regions.Find(r => r.sceneName == sceneName);
        // return regions.Find((string r) => { return r.sceneName == seneName; });
    }


}

[System.Serializable]
public class MapRegionEntry
{
    public string sceneName; // 场景名称 / Addressables key
    public string displayName; // 显示名
    public Sprite icon; // 图标
}


