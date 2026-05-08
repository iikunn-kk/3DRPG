using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 计时器信息，用于控制计时器
/// </summary>
public class TimerHandle
{
    public string Id { get; private set; }
    public bool IsRunning { get; private set; }
    public float RemainingTime { get; set; }
    public float TotalTime { get; set; }
    public float Progress => TotalTime > 0 ? 1 - (RemainingTime / TotalTime) : 0;

    private Coroutine _coroutine;
    private MonoBehaviour _owner;

    internal void Setup(string id, MonoBehaviour owner, Coroutine coroutine, float totalTime)
    {
        Id = id;
        _owner = owner;
        _coroutine = coroutine;
        TotalTime = totalTime;
        RemainingTime = totalTime;
        IsRunning = true;
    }

    /// <summary>
    /// 停止计时器
    /// </summary>
    public void Stop()
    {
        if (_owner != null && _coroutine != null)
        {
            _owner.StopCoroutine(_coroutine);
        }
        IsRunning = false;
        TimerManager.Instance.RemoveTimer(Id);
    }

    /// <summary>
    /// 暂停计时器
    /// </summary>
    public void Pause()
    {
        IsRunning = false;
    }

    /// <summary>
    /// 恢复计时器
    /// </summary>
    public void Resume()
    {
        IsRunning = true;
    }
}

/// <summary>
/// 计时器管理器 - 统一管理所有计时器
/// </summary>
public class TimerManager : MonoBehaviour
{
    private static TimerManager _instance;
    public static TimerManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<TimerManager>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("TimerManager");
                    _instance = obj.AddComponent<TimerManager>();
                    DontDestroyOnLoad(obj);
                }
            }
            return _instance;
        }
    }

    private Dictionary<string, TimerHandle> _timers = new Dictionary<string, TimerHandle>();
    private int _timerIndex = 0;

    /// <summary>
    /// 延迟执行（秒）
    /// </summary>
    public TimerHandle Delay(float seconds, Action onComplete)
    {
        return Delay(seconds, onComplete, "Timer_" + (_timerIndex++));
    }

    public TimerHandle Delay(float seconds, Action onComplete, string timerId)
    {
        RemoveTimer(timerId);
        var handle = new TimerHandle();
        Coroutine coroutine = StartCoroutine(DelayCoroutine(seconds, onComplete, handle));
        handle.Setup(timerId, this, coroutine, seconds);
        _timers[timerId] = handle;
        return handle;
    }

    private IEnumerator DelayCoroutine(float seconds, Action onComplete, TimerHandle handle)
    {
        handle.RemainingTime = seconds;
        while (handle.RemainingTime > 0)
        {
            yield return null;
            if (handle.IsRunning)
                handle.RemainingTime -= Time.deltaTime;
        }
        onComplete?.Invoke();
        _timers.Remove(handle.Id);
    }

    /// <summary>
    /// 重复执行（无限）
    /// </summary>
    public TimerHandle Repeat(float interval, Action onTick)
    {
        return Repeat(interval, onTick, -1, "Timer_" + (_timerIndex++));
    }

    public TimerHandle Repeat(float interval, Action onTick, int repeatCount)
    {
        return Repeat(interval, onTick, repeatCount, "Timer_" + (_timerIndex++));
    }

    public TimerHandle Repeat(float interval, Action onTick, int repeatCount, string timerId)
    {
        RemoveTimer(timerId);
        var handle = new TimerHandle();
        Coroutine coroutine = StartCoroutine(RepeatCoroutine(interval, onTick, repeatCount, handle));
        handle.Setup(timerId, this, coroutine, interval);
        _timers[timerId] = handle;
        return handle;
    }

    private IEnumerator RepeatCoroutine(float interval, Action onTick, int repeatCount, TimerHandle handle)
    {
        int count = 0;
        while (repeatCount < 0 || count < repeatCount)
        {
            handle.RemainingTime = interval;
            while (handle.RemainingTime > 0)
            {
                yield return null;
                if (handle.IsRunning)
                    handle.RemainingTime -= Time.deltaTime;
            }
            onTick?.Invoke();
            count++;
        }
        _timers.Remove(handle.Id);
    }

    /// <summary>
    /// 倒计时
    /// </summary>
    public TimerHandle Countdown(float totalTime, Action<float> onTick, Action onComplete)
    {
        return Countdown(totalTime, onTick, onComplete, "Timer_" + (_timerIndex++));
    }

    public TimerHandle Countdown(float totalTime, Action<float> onTick, Action onComplete, string timerId)
    {
        RemoveTimer(timerId);
        var handle = new TimerHandle();
        Coroutine coroutine = StartCoroutine(CountdownCoroutine(totalTime, onTick, onComplete, handle));
        handle.Setup(timerId, this, coroutine, totalTime);
        _timers[timerId] = handle;
        return handle;
    }

    private IEnumerator CountdownCoroutine(float totalTime, Action<float> onTick, Action onComplete, TimerHandle handle)
    {
        handle.RemainingTime = totalTime;
        while (handle.RemainingTime > 0)
        {
            yield return null;
            if (handle.IsRunning)
            {
                handle.RemainingTime -= Time.deltaTime;
                onTick?.Invoke(handle.Progress);
            }
        }
        onComplete?.Invoke();
        _timers.Remove(handle.Id);
    }

    /// <summary>
    /// 帧延迟（等待指定帧数）
    /// </summary>
    public TimerHandle FrameDelay(int frames, Action onComplete)
    {
        string timerId = "Timer_" + (_timerIndex++);
        var handle = new TimerHandle();
        Coroutine coroutine = StartCoroutine(FrameDelayCoroutine(frames, onComplete, handle));
        handle.Setup(timerId, this, coroutine, frames);
        _timers[timerId] = handle;
        return handle;
    }

    private IEnumerator FrameDelayCoroutine(int frames, Action onComplete, TimerHandle handle)
    {
        int count = 0;
        while (count < frames)
        {
            yield return null;
            if (handle.IsRunning)
                count++;
        }
        onComplete?.Invoke();
        _timers.Remove(handle.Id);
    }

    /// <summary>
    /// 停止指定计时器
    /// </summary>
    public void StopTimer(string timerId)
    {
        if (_timers.ContainsKey(timerId))
        {
            _timers[timerId].Stop();
        }
    }

    /// <summary>
    /// 停止所有计时器
    /// </summary>
    public void StopAll()
    {
        foreach (var timer in _timers.Values)
        {
            timer.Stop();
        }
        _timers.Clear();
    }

    /// <summary>
    /// 获取计时器
    /// </summary>
    public TimerHandle GetTimer(string timerId)
    {
        return _timers.ContainsKey(timerId) ? _timers[timerId] : null;
    }

    public void RemoveTimer(string timerId)
    {
        _timers.Remove(timerId);
    }
}
