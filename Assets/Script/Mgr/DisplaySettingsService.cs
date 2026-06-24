using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 显示设置服务 — 绞杀 ResolutionManager 的替代品。
/// 职责单一：分辨率列表、显示模式切换、启动时应用存档设置。
/// </summary>
public class DisplaySettingsService
{
    private static readonly List<Vector2Int> AllowedResolutions = new()
    {
        new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440),
        new Vector2Int(3840, 2160),
    };

    // ===== 启动入口 =====
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void Bootstrap() => new DisplaySettingsService().ApplyInitialFromSave();

    // ===== 公共 API =====

    public void ApplyInitialFromSave()
    {
        var ps = SettingsService.Instance.Load() ?? new PlayerSetting();
        if (ps.fullScreenMode == FullScreenMode.MaximizedWindow)
            ps.fullScreenMode = FullScreenMode.FullScreenWindow;
        ApplyNormalized(ps);
    }

    public Vector2Int GetMaxResolution()
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

    public List<Vector2Int> GetWindowedOptions()
    {
        var max = GetMaxResolution();
        return AllowedResolutions.Where(r => r.x <= max.x && r.y <= max.y).ToList();
    }

    public int FindResolutionIndex(List<Vector2Int> list, int w, int h)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i].x == w && list[i].y == h) return i;
        return -1;
    }

    public void ApplyWindowedResolution(int index)
    {
        var options = GetWindowedOptions();
        if (options == null || options.Count == 0)
        {
            var max = GetMaxResolution();
            Screen.SetResolution(max.x, max.y, FullScreenMode.Windowed);
            SaveCurrentState();
            return;
        }
        if (index < 0 || index >= options.Count) return;
        var res = options[index];
        Screen.SetResolution(res.x, res.y, FullScreenMode.Windowed);
        SaveCurrentState();
    }

    public void ApplyDisplayMode(int dropdownIndex)
    {
        var max = GetMaxResolution();
        if (dropdownIndex == 0)
        {
            Screen.SetResolution(max.x, max.y, FullScreenMode.ExclusiveFullScreen);
        }
        else if (dropdownIndex == 1)
        {
            var ps = SettingsService.Instance.Load() ?? new PlayerSetting();
            var options = GetWindowedOptions();
            int i = FindResolutionIndex(options, ps.resolutionWidth, ps.resolutionHeight);
            var res = i >= 0 ? options[i]
                    : options.Count > 0 ? options.Last()
                    : max;
            Screen.SetResolution(res.x, res.y, FullScreenMode.Windowed);
        }
        else
        {
            Screen.SetResolution(max.x, max.y, FullScreenMode.FullScreenWindow);
        }
        SaveCurrentState();
    }

    public int GetCurrentModeIndex()
    {
        return Screen.fullScreenMode switch
        {
            FullScreenMode.ExclusiveFullScreen => 0,
            FullScreenMode.Windowed => 1,
            FullScreenMode.FullScreenWindow => 2,
            FullScreenMode.MaximizedWindow => 2,
            _ => 1
        };
    }

    public List<string> BuildOptionStrings(List<Vector2Int> list)
    {
        var result = new List<string>(list.Count);
        foreach (var r in list)
            result.Add($"{r.x} x {r.y}");
        return result;
    }

    // ===== 内部 =====

    private void SaveCurrentState()
    {
        var ps = SettingsService.Instance.Load() ?? new PlayerSetting();
        ps.fullScreenMode = Screen.fullScreenMode;
        ps.resolutionWidth = Screen.width;
        ps.resolutionHeight = Screen.height;
        SettingsService.Instance.Save(ps);
    }

    private void ApplyNormalized(PlayerSetting ps)
    {
        var max = GetMaxResolution();
        if (ps.fullScreenMode == FullScreenMode.Windowed)
        {
            var options = GetWindowedOptions();
            int i = FindResolutionIndex(options, ps.resolutionWidth, ps.resolutionHeight);
            var res = i >= 0 ? options[i]
                    : options.Count > 0 ? options.Last()
                    : max;
            Screen.SetResolution(res.x, res.y, FullScreenMode.Windowed);
        }
        else if (ps.fullScreenMode == FullScreenMode.ExclusiveFullScreen)
        {
            Screen.SetResolution(max.x, max.y, FullScreenMode.ExclusiveFullScreen);
        }
        else
        {
            Screen.SetResolution(max.x, max.y, FullScreenMode.FullScreenWindow);
        }
        SaveCurrentState();
    }
}
