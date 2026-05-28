using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 基于系统现实时间自动切换日夜效果。
/// 上午 8:00 ~ 下午 6:00 = 白天，其余时间 = 夜晚。
/// </summary>
public class DayNightController : MonoBehaviour
{
    [Header("时间图标")]
    [Tooltip("timeSprite[0] = 太阳(白天), timeSprite[1] = 月亮(夜晚)")]
    [SerializeField] private Sprite[] timeSprite;

    [Tooltip("显示当前日夜状态的 Image 组件")]
    [SerializeField] private Image timeImage;

    [Header("天空盒子材质")]
    [Tooltip("skyboxMaterials[0] = 白天材质, skyboxMaterials[1] = 夜晚材质")]
    [SerializeField] private Material[] skyboxMaterials;
    [SerializeField] private Light sceneLight;

    [Header("时间设置")]
    [Tooltip("白天开始时间（24小时制）")]
    [SerializeField] private int dayStartHour = 8;

    [Tooltip("白天结束时间（24小时制）")]
    [SerializeField] private int dayEndHour = 18;

    private bool _isDaytime;

    private void Start()
    {
        ApplyDayNight();
    }

    /// <summary>
    /// 根据当前系统时间判断日夜并应用对应资源
    /// </summary>
    private void ApplyDayNight()
    {
        int currentHour = System.DateTime.Now.Hour;
        _isDaytime = currentHour >= dayStartHour && currentHour < dayEndHour;

        UpdateTimeIcon();
        UpdateSkybox();
        UpdateSceneLight();
    }

    /// <summary>
    /// 更新 UI 中的日夜图标
    /// </summary>
    private void UpdateTimeIcon()
    {
        if (timeImage == null)
        {
            Debug.LogWarning("DayNightController: timeImage 未赋值");
            return;
        }

        if (timeSprite == null || timeSprite.Length < 2)
        {
            Debug.LogWarning("DayNightController: timeSprite 数组长度不足，需要至少 2 个 Sprite（[0]白天, [1]夜晚）");
            return;
        }

        timeImage.sprite = _isDaytime ? timeSprite[0] : timeSprite[1];
    }

    /// <summary>
    /// 更新场景天空盒子材质
    /// </summary>
    private void UpdateSkybox()
    {
        if (skyboxMaterials == null || skyboxMaterials.Length < 2)
        {
            Debug.LogWarning("DayNightController: skyboxMaterials 数组长度不足，需要至少 2 个 Material（[0]白天, [1]夜晚）");
            return;
        }

        Material targetMat = _isDaytime ? skyboxMaterials[0] : skyboxMaterials[1];

        if (targetMat != null)
        {
            RenderSettings.skybox = targetMat;
        }
        else
        {
            Debug.LogWarning($"DayNightController: skyboxMaterials[{(_isDaytime ? 0 : 1)}] 为空");
        }

        // 如果天空盒有动态属性（如光照强度），可通过 DynamicGI 刷新
        DynamicGI.UpdateEnvironment();
    }
    private void UpdateSceneLight()
    {
        if (sceneLight == null)
        {
            Debug.Log("当前的sceneLight未赋值，请赋值");
        }
        //白天光照强度为0.8，夜晚调整为0.1
        sceneLight.intensity = _isDaytime ? 0.8f : 0.1f;

    }

    /// <summary>
    /// 公开属性：当前是否为白天
    /// </summary>
    public bool IsDaytime => _isDaytime;
}
