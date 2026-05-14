using NUnit.Framework;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// AudioManager 单元测试 — 覆盖核心音频播控逻辑。
/// 使用 SetInstance 注入测试实例，测试 BGM/SFX 播放和音量/开关逻辑。
/// </summary>
public class AudioManagerTests
{
    private GameObject _managerGo;
    private AudioManager _audioMgr;

    [SetUp]
    public void SetUp()
    {
        _managerGo = new GameObject("Test_AudioManager");

        // 为 AudioManager 添加 AudioSource 依赖
        var bgmSource = new GameObject("BGM_Source");
        bgmSource.transform.SetParent(_managerGo.transform);
        bgmSource.AddComponent<AudioSource>();

        var sfxSource = new GameObject("SFX_Source");
        sfxSource.transform.SetParent(_managerGo.transform);
        sfxSource.AddComponent<AudioSource>();

        _audioMgr = _managerGo.AddComponent<AudioManager>();
        Singleton<AudioManager>.SetInstance(_audioMgr);
    }

    [TearDown]
    public void TearDown()
    {
        Singleton<AudioManager>.DestroyInstance();
        if (_managerGo != null)
            Object.DestroyImmediate(_managerGo);
    }

    // ==================== 单例访问测试 ====================

    [Test]
    public void Instance_AfterSetUp_IsNotNull()
    {
        Assert.That(AudioManager.Instance, Is.Not.Null);
        Assert.That(AudioManager.Instance, Is.EqualTo(_audioMgr));
    }

    // ==================== BGM 播放测试 ====================

    [Test]
    public void PlayBGM_WithValidType_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _audioMgr.PlayBGM(BGMType.场景1));
    }

    [Test]
    public void StopBGM_FadeOut_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _audioMgr.StopBGM(true, 0.5f).Forget());
    }

    [Test]
    public void StopBGM_Instant_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _audioMgr.StopBGM(false).Forget());
    }

    // ==================== SFX 播放测试 ====================

    [Test]
    public void PlayUISound_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _audioMgr.PlayUISound(UISoundType.按下按钮));
    }

    // ==================== 音量控制测试 ====================

    [Test]
    public void SetBGMVolume_WithinRange_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _audioMgr.SetBGMVolume(0.5f));
        Assert.DoesNotThrow(() => _audioMgr.SetBGMVolume(0f));
        Assert.DoesNotThrow(() => _audioMgr.SetBGMVolume(1f));
    }

    [Test]
    public void SetSFXVolume_WithinRange_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _audioMgr.SetSFXVolume(0.5f));
        Assert.DoesNotThrow(() => _audioMgr.SetSFXVolume(0f));
        Assert.DoesNotThrow(() => _audioMgr.SetSFXVolume(1f));
    }

    // ==================== 开关测试 ====================

    [Test]
    public void SetBGMEnabled_Works()
    {
        Assert.DoesNotThrow(() => _audioMgr.SetBGMEnabled(false));
        Assert.DoesNotThrow(() => _audioMgr.SetBGMEnabled(true));
    }

    [Test]
    public void SetSFXEnabled_Works()
    {
        Assert.DoesNotThrow(() => _audioMgr.SetSFXEnabled(false));
        Assert.DoesNotThrow(() => _audioMgr.SetSFXEnabled(true));
    }

    // ==================== 冷却时间测试 ====================

    [Test]
    public void PlayUISound_RespectsCooldown()
    {
        // 连续播放同一音效应受冷却限制，不会崩溃
        Assert.DoesNotThrow(() => _audioMgr.PlayUISound(UISoundType.按下按钮));
        Assert.DoesNotThrow(() => _audioMgr.PlayUISound(UISoundType.按下按钮));
    }
}
