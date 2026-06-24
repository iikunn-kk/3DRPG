using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 控制基础设置面板的UI交互，包括显示设置和音频设置。
/// [V3] 优化：在全屏/无边框模式下，分辨率下拉菜单禁用并显示当前实际分辨率。
/// </summary>
public class BasicSettingsPanel : MonoBehaviour
{
    [Header("UI 控件引用")]
    [SerializeField] private TMP_Text bgmVolumeText;
    [SerializeField] private TMP_Text soundVolumeText;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown fullScreenDropdown;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider soundVolumeSlider;
    [SerializeField] private Toggle bgmToggle;
    [SerializeField] private Toggle soundToggle;

    // 显示设置服务（取代旧的 ResolutionManager）
    private readonly DisplaySettingsService _displaySettings = new();

    private bool _listenersAdded = false;
    private CancellationTokenSource _deferredRefreshCts;

    // 当前窗口模式的有效分辨率映射（与下拉索引对应）
    private List<Vector2Int> _currentWindowedOptions = new List<Vector2Int>();

    private void Start()
    {
        if (!_listenersAdded)
        {
            AddListeners();
            _listenersAdded = true;
        }
    }

    private void OnEnable()
    {
        InitDisplaySettings();
        InitAudioSettings();
    }

    private void AddListeners()
    {
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChange);
        fullScreenDropdown.onValueChanged.AddListener(OnFullScreenModeChange);
        bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeSliderChange);
        soundVolumeSlider.onValueChanged.AddListener(OnSoundVolumeSliderChange);
        bgmToggle.onValueChanged.AddListener(OnBgmToggleChange);
        soundToggle.onValueChanged.AddListener(OnSoundToggleChange);
    }

    #region 显示设置

    private void InitDisplaySettings()
    {
        // 显示模式映射：0=全屏(独占),1=窗口,2=无边框
        int currentModeIndex = _displaySettings.GetCurrentModeIndex();
        fullScreenDropdown.SetValueWithoutNotify(currentModeIndex);

        // 根据当前状态，重建分辨率下拉列表的UI
        RebuildResolutionDropdownForCurrentMode();
    }

    /// <summary>
    /// 核心方法：根据当前显示模式，重建分辨率下拉列表的UI状态。
    /// </summary>
    private void RebuildResolutionDropdownForCurrentMode()
    {
        resolutionDropdown.ClearOptions();
        bool isWindowed = Screen.fullScreenMode == FullScreenMode.Windowed;
        resolutionDropdown.interactable = isWindowed;

        if (isWindowed)
        {
            // 窗口模式：动态获取有效分辨率选项
            _currentWindowedOptions = _displaySettings.GetWindowedOptions();
            var optionStrings = _displaySettings.BuildOptionStrings(_currentWindowedOptions);
            resolutionDropdown.AddOptions(optionStrings);

            // 选中当前分辨率对应的选项（若找不到则默认选中最后一项）
            int idx = _displaySettings.FindResolutionIndex(_currentWindowedOptions, Screen.width, Screen.height);
            if (idx < 0) idx = Mathf.Max(0, _currentWindowedOptions.Count - 1);
            resolutionDropdown.SetValueWithoutNotify(idx);
        }
        else
        {
            // 全屏或无边框模式：清空选项，只添加并显示当前实际分辨率
            string currentResText = $"{Screen.width} x {Screen.height}";
            resolutionDropdown.AddOptions(new List<string> { currentResText });
            resolutionDropdown.SetValueWithoutNotify(0);
            _currentWindowedOptions.Clear();
        }
        resolutionDropdown.RefreshShownValue();
    }

    public void OnResolutionChange(int index)
    {
        if (Screen.fullScreenMode != FullScreenMode.Windowed) return;
        if (index < 0 || index >= _currentWindowedOptions.Count) return;

        // 通过管理器应用（统一保存与模式逻辑）
        _displaySettings.ApplyWindowedResolution(index);
        AudioManager.Instance?.PlayUISound(UISoundType.按下按钮);
    }

    public void OnFullScreenModeChange(int index)
    {
        // 与 ResolutionManager 的映射保持一致：0=全屏(独占),1=窗口,2=无边框
        _displaySettings.ApplyDisplayMode(index);
        AudioManager.Instance?.PlayUISound(UISoundType.按下按钮);

        // 延迟到下一帧刷新UI，以确保Screen.width/height已更新为最新值
        DeferredRefreshDisplayUI();
    }

    /// <summary>
    /// 启动一个协程，在下一帧刷新显示设置的UI。
    /// </summary>
    private void DeferredRefreshDisplayUI()
    {
        _deferredRefreshCts?.Cancel();
        _deferredRefreshCts = new CancellationTokenSource();
        DeferredRefreshRoutineAsync(_deferredRefreshCts.Token).Forget();
    }

    private async UniTaskVoid DeferredRefreshRoutineAsync(CancellationToken token)
    {
        // 等待一帧，确保Screen.SetResolution的更改已完全应用
        await UniTask.Yield(token);
        RebuildResolutionDropdownForCurrentMode();
    }

    #endregion

    #region 音频设置

    private void InitAudioSettings()
    {
        var audioMgr = AudioManager.Instance;
        if (audioMgr == null)
        {
            Debug.LogWarning("AudioManager尚未初始化，从存档加载音频设置UI。");
            PlayerSetting playerSetting = SettingsService.Instance.Load() ?? new PlayerSetting();
            UpdateAudioUI(playerSetting.openBgm, playerSetting.bgmVolume, playerSetting.openSound, playerSetting.soundVolume);
        }
        else
        {
            UpdateAudioUI(audioMgr.IsBGMEnabled(), audioMgr.GetBGMVolume(), audioMgr.IsSFXEnabled(), audioMgr.GetSFXVolume());
        }
    }

    private void UpdateAudioUI(bool bgmOn, float bgmVol, bool sfxOn, float sfxVol)
    {
        bgmToggle.SetIsOnWithoutNotify(bgmOn);
        soundToggle.SetIsOnWithoutNotify(sfxOn);
        bgmVolumeSlider.SetValueWithoutNotify(NormalizedToSlider(bgmVolumeSlider, bgmVol));
        soundVolumeSlider.SetValueWithoutNotify(NormalizedToSlider(soundVolumeSlider, sfxVol));
        bgmVolumeText.text = (bgmVol * 100f).ToString("F0");
        soundVolumeText.text = (sfxVol * 100f).ToString("F0");
        bgmVolumeSlider.interactable = bgmOn;
        soundVolumeSlider.interactable = sfxOn;
    }

    public void OnBgmVolumeSliderChange(float value)
    {
        float normalized = SliderToNormalized(bgmVolumeSlider, value);
        bgmVolumeText.text = (normalized * 100f).ToString("F0");
        AudioManager.Instance?.SetBGMVolume(normalized);
        AudioManager.Instance?.PlayUISound(UISoundType.按下按钮);
        if (AudioManager.Instance != null) SaveAudioSettings();
    }

    public void OnSoundVolumeSliderChange(float value)
    {
        float normalized = SliderToNormalized(soundVolumeSlider, value);
        soundVolumeText.text = (normalized * 100f).ToString("F0");
        AudioManager.Instance?.SetSFXVolume(normalized);
        AudioManager.Instance?.PlayUISound(UISoundType.按下按钮);
        if (AudioManager.Instance != null) SaveAudioSettings();
    }

    public void OnBgmToggleChange(bool isOn)
    {
        bgmVolumeSlider.interactable = isOn;
        AudioManager.Instance?.SetBGMEnabled(isOn);
        AudioManager.Instance?.PlayUISound(UISoundType.按下按钮);
        if (AudioManager.Instance != null) SaveAudioSettings();
    }

    public void OnSoundToggleChange(bool isOn)
    {
        soundVolumeSlider.interactable = isOn;
        AudioManager.Instance?.SetSFXEnabled(isOn);
        AudioManager.Instance?.PlayUISound(UISoundType.按下按钮);
        if (AudioManager.Instance != null) SaveAudioSettings();
    }

    private void SaveAudioSettings()
    {
        PlayerSetting ps = SettingsService.Instance.Load() ?? new PlayerSetting();
        ps.openBgm = AudioManager.Instance.IsBGMEnabled();
        ps.bgmVolume = AudioManager.Instance.GetBGMVolume();
        ps.openSound = AudioManager.Instance.IsSFXEnabled();
        ps.soundVolume = AudioManager.Instance.GetSFXVolume();
        SettingsService.Instance.Save(ps);
    }

    #endregion

    #region 还原默认与退出逻辑

    public void RestoreDefaultSettings()
    {
        bgmToggle.isOn = true;
        soundToggle.isOn = true;
        bgmVolumeSlider.value = bgmVolumeSlider.maxValue;
        soundVolumeSlider.value = soundVolumeSlider.maxValue;
        OnBgmToggleChange(true);
        OnSoundToggleChange(true);
        OnBgmVolumeSliderChange(bgmVolumeSlider.value);
        OnSoundVolumeSliderChange(soundVolumeSlider.value);

        fullScreenDropdown.value = 0; // 0 是 "全屏(独占)"

        AudioManager.Instance.PlayUISound(UISoundType.按下按钮);
    }

    public void ReselectCharacter()
    {
        RunLoginAction(lm => lm.ReselectCharacter());
    }

    public void ReselectServer()
    {
        RunLoginAction(lm => lm.ReselectServer());
    }

    public void Logout()
    {
        RunLoginAction(lm =>
        {
            lm.Logout();
            lm.ShowLoginPanel();
        });
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void RunLoginAction(System.Action<PlayerLogInManager> action)
    {
        if (SceneManager.GetActiveScene().name == "LoginScene" || SceneLoadManager.Instance.CurrentSceneName == "LoginScene")
        {
            var lm = FindFirstObjectByType<PlayerLogInManager>();
            if (lm != null) action?.Invoke(lm);
            else Debug.LogWarning("[BasicSettingsPanel] 未找到 PlayerLogInManager (已在登录场景)");
            return;
        }

        if (SceneLoadManager.Instance.IsLoading) return;
        SceneLoadManager.Instance.LoadLoginScene(true, () =>
        {
            var lm = FindFirstObjectByType<PlayerLogInManager>();
            if (lm != null) action?.Invoke(lm);
            else Debug.LogWarning("[BasicSettingsPanel] 未找到 PlayerLogInManager (加载完成后)");
        });
    }

    #endregion

    #region 工具方法

    private float SliderToNormalized(Slider slider, float value)
    {
        if (slider == null || slider.minValue == slider.maxValue) return 0;
        return (value - slider.minValue) / (slider.maxValue - slider.minValue);
    }

    private float NormalizedToSlider(Slider slider, float normalizedValue)
    {
        if (slider == null) return 0;
        return slider.minValue + (slider.maxValue - slider.minValue) * Mathf.Clamp01(normalizedValue);
    }

    #endregion
}

