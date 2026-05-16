using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Pool;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

public class AudioManager : Singleton<AudioManager>
{
    [Header("配置")]
    [SerializeField] private AudioConfig audioConfig;

    [Header("AudioMixer 引用")]
    [Tooltip("主混音器 (需暴露 BGMVolume 与 SFXVolume 参数)")]
    [SerializeField] private AudioMixer masterMixer;
    [SerializeField] private AudioMixerGroup bgmGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;
    [Tooltip("必须与AudioMixer中为BGM音量暴露的参数名完全一致")]
    [SerializeField] private string bgmVolumeParam = "BGMVolume";
    [Tooltip("必须与AudioMixer中为SFX音量暴露的参数名完全一致")]
    [SerializeField] private string sfxVolumeParam = "SFXVolume";

    private IObjectPool<AudioSource> _audioSourcePool;
    private AudioSource _currentBgmSource;
    private Dictionary<int, float> _clipCooldowns = new Dictionary<int, float>();
    private Dictionary<SkillSoundType, List<AudioSource>> _activeWeaponSounds = new Dictionary<SkillSoundType, List<AudioSource>>();
    private HashSet<SkillSoundType> _loopingWeaponTypes = new HashSet<SkillSoundType>();
    private List<int> _clipCooldownsKeysToRemove = new List<int>();

    [Header("对象池配置")]
    [SerializeField] private int defaultPoolCapacity = 10;
    [SerializeField] private int maxPoolSize = 30;

    private bool _isBGMEnabled = true;
    private bool _isSFXEnabled = true;
    private float _bgmVolume = 1f;   // 0~1 线性值
    private float _sfxVolume = 1f;   // 0~1 线性值

    private PlayerSetting _playerSettingCache;

    protected override void Awake()
    {
        base.Awake();
        InitAudioSystem();
    }

    private void InitAudioSystem()
    {
        if (audioConfig == null)
        {
            Debug.LogError("未设置AudioConfig，请在Inspector中分配!");
            return;
        }

        LoadSettings();

        _audioSourcePool = new ObjectPool<AudioSource>(
            createFunc: CreatePooledAudioSource,
            actionOnGet: OnTakeFromPool,
            actionOnRelease: OnReturnToPool,
            actionOnDestroy: OnDestroyPoolObject,
            collectionCheck: true,
            defaultCapacity: defaultPoolCapacity,
            maxSize: maxPoolSize
        );

        // 在系统初始化时立即应用一次加载的设置
        ApplyAllAudioSettings();
        WarmupPool();
    }

    private void Update()
    {
        if (_clipCooldowns.Count > 0)
        {
            _clipCooldownsKeysToRemove.Clear();
            var currentTime = Time.time;
            foreach (var kvp in _clipCooldowns)
            {
                if (currentTime > kvp.Value)
                {
                    _clipCooldownsKeysToRemove.Add(kvp.Key);
                }
            }
            for (int i = 0; i < _clipCooldownsKeysToRemove.Count; i++)
            {
                _clipCooldowns.Remove(_clipCooldownsKeysToRemove[i]);
            }
        }
    }

    #region 音频设置 (使用 AudioMixer)

    // 将线性音量值 (0.0 to 1.0) 转换为分贝 (-80dB to 0dB)
    private float ToDecibel(float linearVolume)
    {
        // 确保线性值在0-1范围内
        linearVolume = Mathf.Clamp01(linearVolume);
        // 当线性值极小时，直接返回-80dB，这是Unity的静音值
        if (linearVolume < 0.0001f)
        {
            return -80f;
        }
        return Mathf.Log10(linearVolume) * 20f;
    }

    private void LoadSettings()
    {
        _playerSettingCache = SaveManager.Instance.LoadPlayerSetting() ?? new PlayerSetting();
        _isBGMEnabled = _playerSettingCache.openBgm;
        _isSFXEnabled = _playerSettingCache.openSound;
        _bgmVolume = Mathf.Clamp01(_playerSettingCache.bgmVolume);
        _sfxVolume = Mathf.Clamp01(_playerSettingCache.soundVolume);
    }

    private void SaveAudioSettings()
    {
        if (_playerSettingCache == null) _playerSettingCache = new PlayerSetting();
        _playerSettingCache.openBgm = _isBGMEnabled;
        _playerSettingCache.openSound = _isSFXEnabled;
        _playerSettingCache.bgmVolume = _bgmVolume;
        _playerSettingCache.soundVolume = _sfxVolume;
        SaveManager.Instance.SavePlayerSetting(_playerSettingCache);
    }

    public void SetBGMEnabled(bool isEnabled)
    {
        if (_isBGMEnabled == isEnabled) return;
        _isBGMEnabled = isEnabled;
        ApplyBGMSetting();
        SaveAudioSettings();
    }

    public void SetSFXEnabled(bool isEnabled)
    {
        if (_isSFXEnabled == isEnabled) return;
        _isSFXEnabled = isEnabled;
        ApplySFXSetting();
        SaveAudioSettings();
    }

    public void SetBGMVolume(float normalized)
    {
        float clampedVolume = Mathf.Clamp01(normalized);
        if (Mathf.Approximately(_bgmVolume, clampedVolume)) return;
        _bgmVolume = clampedVolume;
        ApplyBGMSetting();
        SaveAudioSettings();
    }

    public void SetSFXVolume(float normalized)
    {
        float clampedVolume = Mathf.Clamp01(normalized);
        if (Mathf.Approximately(_sfxVolume, clampedVolume)) return;
        _sfxVolume = clampedVolume;
        ApplySFXSetting();
        SaveAudioSettings();
    }

    // 应用所有音频设置，通常在初始化时调用
    private void ApplyAllAudioSettings()
    {
        ApplyBGMSetting();
        ApplySFXSetting();
    }

    private void ApplyBGMSetting()
    {
        if (masterMixer != null)
        {
            // 如果开关是关的，则直接设为静音；否则根据音量值计算分贝
            float finalDb = _isBGMEnabled ? ToDecibel(_bgmVolume) : -80f;
            masterMixer.SetFloat(bgmVolumeParam, finalDb);
        }
        if (_currentBgmSource != null)
        {
            _currentBgmSource.mute = !_isBGMEnabled;
        }
    }

    private void ApplySFXSetting()
    {
        if (masterMixer != null)
        {
            float finalDb = _isSFXEnabled ? ToDecibel(_sfxVolume) : -80f;
            masterMixer.SetFloat(sfxVolumeParam, finalDb);
        }
        if (!_isSFXEnabled)
        {
            StopAllWeaponSounds(false);
        }
    }

    public bool IsBGMEnabled() => _isBGMEnabled;
    public bool IsSFXEnabled() => _isSFXEnabled;
    public float GetBGMVolume() => _bgmVolume;
    public float GetSFXVolume() => _sfxVolume;
    #endregion

    // ... (对象池管理, BGM播放, 音效播放等其他部分代码保持不变)
    #region 对象池管理
    private void WarmupPool()
    {
        var tempList = new List<AudioSource>();
        for (int i = 0; i < defaultPoolCapacity; i++) tempList.Add(_audioSourcePool.Get());
        foreach (var obj in tempList) _audioSourcePool.Release(obj);
    }
    private AudioSource CreatePooledAudioSource()
    {
        GameObject go = new GameObject("PooledAudioSource");
        go.transform.SetParent(transform);
        AudioSource audioSource = go.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        if (sfxGroup != null) audioSource.outputAudioMixerGroup = sfxGroup;
        return audioSource;
    }
    private void OnTakeFromPool(AudioSource audioSource) => audioSource.gameObject.SetActive(true);
    private void OnReturnToPool(AudioSource audioSource)
    {
        audioSource.Stop();
        audioSource.clip = null;
        audioSource.loop = false;
        audioSource.gameObject.SetActive(false);
    }
    private void OnDestroyPoolObject(AudioSource audioSource) => Destroy(audioSource.gameObject);
    #endregion

    #region BGM 播放
    public void PlayBGM(BGMType bgmType)
    {
        AudioClip clip = audioConfig.GetBGMClip(bgmType);
        if (clip == null)
        {
            Debug.LogError($"未找到BGM配置: {bgmType}");
            return;
        }
        StopBGM(false).Forget();

        if (_currentBgmSource == null)
        {
            _currentBgmSource = gameObject.AddComponent<AudioSource>();
            _currentBgmSource.playOnAwake = false;
            if (bgmGroup != null) _currentBgmSource.outputAudioMixerGroup = bgmGroup;
            _currentBgmSource.loop = true;
        }
        _currentBgmSource.clip = clip;
        _currentBgmSource.Play();
        ApplyBGMSetting();
    }

    public async UniTask StopBGM(bool fadeOut = true, float fadeDuration = 0.5f)
    {
        if (_currentBgmSource != null && _currentBgmSource.isPlaying)
        {
            if (fadeOut) await FadeOutBGMAsync(fadeDuration, this.GetCancellationTokenOnDestroy());
            else
            {
                _currentBgmSource.Stop();
                _currentBgmSource.clip = null;
            }
        }
    }

    private async UniTask FadeOutBGMAsync(float fadeDuration, CancellationToken token)
    {
        try
        {
            float startVolume = _currentBgmSource != null ? _currentBgmSource.volume : 1f;
            float timer = 0;
            while (timer < fadeDuration && _currentBgmSource != null)
            {
                timer += Time.deltaTime;
                float t = timer / fadeDuration;
                if (_currentBgmSource != null)
                    _currentBgmSource.volume = Mathf.Lerp(startVolume, 0, t);
                await UniTask.Yield(token);
            }

            if (_currentBgmSource != null)
            {
                _currentBgmSource.Stop();
                _currentBgmSource.clip = null;
                _currentBgmSource.volume = 1f;
                ApplyBGMSetting();
            }
        }
        catch (OperationCanceledException) { }
    }

    public void PauseBGM() { if (_currentBgmSource != null && _currentBgmSource.isPlaying) _currentBgmSource.Pause(); }
    public void ResumeBGM() { if (_currentBgmSource != null && !_currentBgmSource.isPlaying && _isBGMEnabled) _currentBgmSource.UnPause(); }
    public bool IsBGMPlaying() => _currentBgmSource != null && _currentBgmSource.isPlaying;
    #endregion

    #region 音效播放
    public void PlayWeaponSound(SkillSoundType soundType)
    {
        if (!_isSFXEnabled) return;
        var clip = audioConfig.GetWeaponSoundClip(soundType);
        if (clip == null) return;
        PlayPooledSound(clip, source =>
        {
            if (!_activeWeaponSounds.ContainsKey(soundType)) _activeWeaponSounds[soundType] = new List<AudioSource>();
            _activeWeaponSounds[soundType].Add(source);
        }, soundType);
    }

    public void PlayLoopingWeaponSound(SkillSoundType soundType)
    {
        if (!_isSFXEnabled) return;
        var clip = audioConfig.GetWeaponSoundClip(soundType);
        if (clip == null) return;

        if (_activeWeaponSounds.TryGetValue(soundType, out var list))
        {
            foreach (var s in list)
            {
                if (s != null && s.isPlaying && s.loop) return;
            }
        }
        AudioSource source = _audioSourcePool.Get();
        source.clip = clip;
        source.loop = true;
        if (sfxGroup != null) source.outputAudioMixerGroup = sfxGroup;
        source.mute = !_isSFXEnabled;
        source.Play();
        if (!_activeWeaponSounds.ContainsKey(soundType)) _activeWeaponSounds[soundType] = new List<AudioSource>();
        _activeWeaponSounds[soundType].Add(source);
        _loopingWeaponTypes.Add(soundType);
    }

    public void PlayMonsterSound(MonsterSoundType soundType)
    {
        if (!_isSFXEnabled) return;
        var clip = audioConfig.GetMonsterSoundClip(soundType);
        if (clip == null) return;
        PlayPooledSound(clip);
    }

    public void PlayPlayerSound(PlayerSoundType soundType)
    {
        if (!_isSFXEnabled) return;
        var clip = audioConfig.GetPlayerSoundClip(soundType);
        if (clip == null) return;
        PlayPooledSound(clip);
    }

    public void PlayUISound(UISoundType soundType)
    {
        if (!_isSFXEnabled) return;
        var clip = audioConfig.GetUISoundClip(soundType);
        if (clip == null) return;
        PlayPooledSound(clip);
    }

    private void PlayPooledSound(AudioClip clip, Action<AudioSource> onSourcePlayed = null, SkillSoundType? weaponType = null)
    {
        int clipId = clip.GetInstanceID();
        if (_clipCooldowns.ContainsKey(clipId)) return;
        _clipCooldowns[clipId] = Time.time + audioConfig.defaultCooldown;

        AudioSource source = _audioSourcePool.Get();
        source.clip = clip;
        source.loop = false;
        if (sfxGroup != null) source.outputAudioMixerGroup = sfxGroup;
        source.mute = !_isSFXEnabled;
        source.Play();

        onSourcePlayed?.Invoke(source);
        ReturnToPoolAfterPlayAsync(source, clip.length, weaponType, this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid ReturnToPoolAfterPlayAsync(AudioSource audioSource, float delay, SkillSoundType? weaponType, CancellationToken token)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);
            if (audioSource != null && audioSource.gameObject.activeInHierarchy)
            {
                _audioSourcePool.Release(audioSource);
                if (weaponType.HasValue) RemoveFromActiveWeaponSounds(weaponType.Value, audioSource);
            }
        }
        catch (OperationCanceledException) { }
    }

    private void RemoveFromActiveWeaponSounds(SkillSoundType soundType, AudioSource source)
    {
        if (_activeWeaponSounds.TryGetValue(soundType, out var sourceList))
        {
            sourceList.Remove(source);
            if (sourceList.Count == 0) _activeWeaponSounds.Remove(soundType);
        }
    }
    #endregion

    #region 停止音效
    public void StopWeaponSound(SkillSoundType soundType, bool fadeOut = false, float fadeDuration = 0.2f)
    {
        if (!_activeWeaponSounds.TryGetValue(soundType, out var sources)) return;
        List<AudioSource> sourcesToStop = new List<AudioSource>(sources);
        foreach (var source in sourcesToStop)
        {
            if (source == null) continue;
            if (source.isPlaying) source.Stop();

            if (source.loop || _loopingWeaponTypes.Contains(soundType))
            {
                if (source.gameObject.activeInHierarchy)
                {
                    _audioSourcePool.Release(source);
                }
            }
        }
        _activeWeaponSounds.Remove(soundType);
        _loopingWeaponTypes.Remove(soundType);
    }

    public void StopAllWeaponSounds(bool fadeOut = false, float fadeDuration = 0.2f)
    {
        List<SkillSoundType> types = new List<SkillSoundType>(_activeWeaponSounds.Keys);
        foreach (var type in types) StopWeaponSound(type, fadeOut, fadeDuration);
        _activeWeaponSounds.Clear();
        _loopingWeaponTypes.Clear();
    }
    #endregion
}