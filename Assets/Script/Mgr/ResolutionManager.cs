// filepath: c:\U3DSTU\Demo\Assets\Script\Mgr\ResolutionManager.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// [已废弃] 请使用 DisplaySettingsService 替代。
/// 保留此类仅为了不破坏已有引用（目前无引用方），将在后续版本删除。
/// </summary>
[System.Obsolete("Use DisplaySettingsService instead.", false)]
public static class ResolutionManager
{
    // 允许的标准分辨率列表（宽 x 高）
    private static readonly List<Vector2Int> AllowedBaseResolutions = new List<Vector2Int>
    {
        new Vector2Int(1920, 1080),   // 1080P
        new Vector2Int(2560, 1440),   // 2K
        new Vector2Int(3840, 2160),   // 4K
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void Bootstrap() => ApplyInitialFromSave();

    /// <summary>
    /// 启动时根据存档应用显示设置（在 Unity 启动动画前执行）。
    /// </summary>
    public static void ApplyInitialFromSave()
    {
        var ps = SettingsService.Instance.Load() ?? new PlayerSetting();
        // 兼容历史：把 MaximizedWindow 当作无边框窗口处理
        if (ps.fullScreenMode == FullScreenMode.MaximizedWindow)
        {
            ps.fullScreenMode = FullScreenMode.FullScreenWindow;
        }
        NormalizeAndApply(ps);
    }

    /// <summary>
    /// 获取当前显示器最大分辨率（尽可能准确）。
    /// </summary>
    public static Vector2Int GetMaxDisplayResolution()
    {
        int w = 0, h = 0;
        if (Display.main != null)
        {
            w = Display.main.systemWidth;
            h = Display.main.systemHeight;
        }

        if (w <= 0 || h <= 0)
        {
            var max = Screen.resolutions.OrderBy(r => r.width * r.height).LastOrDefault();
            if (max.width > 0) return new Vector2Int(max.width, max.height);
            var cur = Screen.currentResolution;
            return new Vector2Int(cur.width, cur.height);
        }
        return new Vector2Int(w, h);
    }

    /// <summary>
    /// 获取窗口模式下可供选择的分辨率（仅 1080P/2K/4K，且不超过显示器最大分辨率）。
    /// </summary>
    public static List<Vector2Int> GetWindowedOptions()
    {
        var max = GetMaxDisplayResolution();
        return AllowedBaseResolutions.Where(r => r.x <= max.x && r.y <= max.y).ToList();
    }

    /// <summary>
    /// 根据列表查找指定分辨率的索引。
    /// </summary>
    public static int FindIndexForResolution(List<Vector2Int> list, int width, int height)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].x == width && list[i].y == height) return i;
        }
        return -1;
    }

    /// <summary>
    /// 应用窗口模式下的分辨率（通过索引）。
    /// </summary>
    public static void ApplyWindowedResolutionByIndex(int index)
    {
        var options = GetWindowedOptions();
        if (options == null || options.Count == 0) // 极端情况：显示器低于 1080P
        {
            // 回退到显示器最大分辨率，但仍保持窗口模式
            var max = GetMaxDisplayResolution();
            Screen.SetResolution(max.x, max.y, FullScreenMode.Windowed);
            SaveApplied();
            return;
        }

        if (index < 0 || index >= options.Count) return;
        var res = options[index];
        Screen.SetResolution(res.x, res.y, FullScreenMode.Windowed);
        SaveApplied();
    }

    /// <summary>
    /// 根据下拉框索引应用显示模式：
    /// 0=全屏模式(独占)，1=窗口模式，2=无边框窗口最大化。
    /// </summary>
    public static void ApplyDisplayModeFromDropdownIndex(int index)
    {
        if (index == 0)
        {
            // 全屏模式（独占）：强制显示器最大分辨率
            var max = GetMaxDisplayResolution();
            Screen.SetResolution(max.x, max.y, FullScreenMode.ExclusiveFullScreen);
        }
        else if (index == 1)
        {
            // 窗口模式：优先使用存档中的窗口分辨率，否则取可选列表中的最大一档
            var ps = SettingsService.Instance.Load() ?? new PlayerSetting();
            var options = GetWindowedOptions();
            Vector2Int res;
            int i = FindIndexForResolution(options, ps.resolutionWidth, ps.resolutionHeight);
            if (i < 0)
            {
                res = options.Count > 0 ? options[options.Count - 1] : GetMaxDisplayResolution();
            }
            else
            {
                res = options[i];
            }
            Screen.SetResolution(res.x, res.y, FullScreenMode.Windowed);
        }
        else
        {
            // 无边框窗口最大化：强制显示器最大分辨率
            var max = GetMaxDisplayResolution();
            Screen.SetResolution(max.x, max.y, FullScreenMode.FullScreenWindow);
        }
        SaveApplied();
    }

    /// <summary>
    /// 保存当前已应用的屏幕设置到存档。
    /// </summary>
    public static void SaveApplied()
    {
        var ps = SettingsService.Instance.Load() ?? new PlayerSetting();
        ps.fullScreenMode = Screen.fullScreenMode;
        ps.resolutionWidth = Screen.width;
        ps.resolutionHeight = Screen.height;
        SettingsService.Instance.Save(ps);
    }

    /// <summary>
    /// 将存档中的设置规范化并应用到当前屏幕。
    /// </summary>
    private static void NormalizeAndApply(PlayerSetting ps)
    {
        var max = GetMaxDisplayResolution();
        if (ps.fullScreenMode == FullScreenMode.Windowed)
        {
            var options = GetWindowedOptions();
            Vector2Int res = options.LastOrDefault();
            int i = FindIndexForResolution(options, ps.resolutionWidth, ps.resolutionHeight);
            if (i >= 0)
            {
                res = options[i];
            }
            else if (options.Count == 0)
            {
                // 极端情况：显示器低于 1080P
                res = max;
            }
            Screen.SetResolution(res.x, res.y, FullScreenMode.Windowed);
        }
        else if (ps.fullScreenMode == FullScreenMode.ExclusiveFullScreen)
        {
            Screen.SetResolution(max.x, max.y, FullScreenMode.ExclusiveFullScreen);
        }
        else
        {
            // 其他模式统一视作无边框最大化
            Screen.SetResolution(max.x, max.y, FullScreenMode.FullScreenWindow);
        }

        // 将规范化后的结果回写到存档
        SaveApplied();
    }

    /// <summary>
    /// 获取当前全屏下拉框应显示的索引。
    /// 0=全屏(独占), 1=窗口, 2=无边框
    /// </summary>
    public static int GetFullScreenDropdownIndex()
    {
        switch (Screen.fullScreenMode)
        {
            case FullScreenMode.ExclusiveFullScreen: return 0;
            case FullScreenMode.Windowed: return 1;
            case FullScreenMode.FullScreenWindow: return 2;
            case FullScreenMode.MaximizedWindow: return 2; // 兼容映射
            default: return 1;
        }
    }

    /// <summary>
    /// 构建分辨率下拉框显示字符串。
    /// </summary>
    public static List<string> BuildResolutionOptionStrings(List<Vector2Int> list)
    {
        var opts = new List<string>(list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            opts.Add($"{list[i].x} x {list[i].y}");
        }
        return opts;
    }
}
