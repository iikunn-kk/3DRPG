# Unity 计时器工具集

一套简洁高效的 Unity 计时器解决方案，基于协程实现，支持延迟执行、重复执行、倒计时、帧延迟等功能。

---

## 目录

- [快速开始](#快速开始)
- [核心组件](#核心组件)
- [API 参考](#api-参考)
- [使用示例](#使用示例)

---

## 快速开始

### TimerManager（管理器）

TimerManager 是一个单例 MonoBehaviour，管理所有计时器。

```csharp
// 获取实例（自动创建）
TimerManager.Instance.Delay(3f, () => Debug.Log("3秒后执行"));
```

> **注意**：不需要手动挂载到场景，TimerManager 会自动创建 GameObject 并设为 `DontDestroyOnLoad`。

---

## 核心组件

| 文件 | 说明 |
|------|------|
| `TimerManager.cs` | 核心管理器（单例模式） |
| `TimerHandle.cs` | 计时器句柄（控制计时器） |
| `TimerBasic.cs` | 基础延迟示例 |
| `TimerCoroutine.cs` | 重复执行示例 |
| `CountdownTimer.cs` | 倒计时示例 |

---

## API 参考

### TimerHandle（计时器句柄）

每个创建的计时器返回一个 `TimerHandle`，用于控制计时器：

```csharp
public class TimerHandle
{
    string Id;              // 唯一标识符
    bool IsRunning;         // 是否运行中
    float RemainingTime;    // 剩余时间
    float TotalTime;        // 总时间
    float Progress;         // 进度 (0-1)

    void Stop();            // 停止计时器
    void Pause();           // 暂停
    void Resume();          // 恢复
}
```

### TimerManager 方法

| 方法 | 说明 |
|------|------|
| `Delay(seconds, callback)` | 延迟执行一次 |
| `Repeat(interval, callback)` | 无限重复执行 |
| `Repeat(interval, callback, count)` | 重复执行指定次数 |
| `Countdown(total, onTick, onComplete)` | 倒计时（带进度回调） |
| `FrameDelay(frames, callback)` | 帧延迟 |
| `StopTimer(id)` | 停止指定计时器 |
| `StopAll()` | 停止所有计时器 |
| `GetTimer(id)` | 获取计时器句柄 |

---

## 使用示例

### 1. 延迟执行

```csharp
// 3秒后执行
TimerManager.Instance.Delay(3f, () => {
    Debug.Log("执行了！");
});

// 自定义ID，便于后续控制
TimerManager.Instance.Delay(5f, OnComplete, "myTimer");
```

### 2. 重复执行

```csharp
// 每2秒执行一次，无限循环
TimerManager.Instance.Repeat(2f, () => {
    Debug.Log("重复执行");
});

// 执行指定次数
var handle = TimerManager.Instance.Repeat(1f, () => {
    Debug.Log("每秒执行");
}, 10);  // 共执行10次

// 停止
handle?.Stop();
```

### 3. 倒计时

```csharp
var handle = TimerManager.Instance.Countdown(10f,
    (progress) => {
        // 进度回调 (0-1)
        progressBar.value = progress;
        timeText.text = $"{(1-progress) * 10:F1}s";
    },
    () => {
        // 完成回调
        Debug.Log("倒计时结束！");
    }
);

// 暂停/恢复
handle?.Pause();
handle?.Resume();
```

### 4. 帧延迟

```csharp
// 等待5帧后执行
TimerManager.Instance.FrameDelay(5, () => {
    Debug.Log("5帧后执行");
});
```

### 5. 控制计时器

```csharp
// 创建计时器
var handle = TimerManager.Instance.Delay(10f, OnComplete);

// 暂停
handle.Pause();

// 恢复
handle.Resume();

// 停止
handle.Stop();

// 或通过ID停止
TimerManager.Instance.StopTimer("myTimer");
```

### 6. 停止所有计时器

```csharp
// 切换场景时清除所有计时器
TimerManager.Instance.StopAll();
```

---

## 设计特点

| 特点 | 说明 |
|------|------|
| 🎯 **统一管理** | 所有计时器通过 Dictionary 存储，支持 ID 查询 |
| 🔄 **支持暂停/恢复** | 任意计时器可随时暂停和恢复 |
| 📊 **进度追踪** | Progress 属性实时计算进度百分比 |
| 🆔 **唯一标识** | 每个计时器有唯一 ID，支持手动控制 |
| ⏱️ **帧率独立** | 使用 `Time.deltaTime`，不受帧率影响 |
| 🎮 **场景切换安全** | 自动设为 DontDestroyOnLoad，切换场景不丢失 |
| ✨ **轻量简洁** | 仅依赖 Unity 协程，无外部依赖 |

---

## 文件结构

```
Assets/Scripts/Tools/
├── TimerManager.cs    # 核心管理器 (254行)
├── TimerBasic.cs      # 基础示例 (39行)
├── TimerCoroutine.cs  # 协程示例 (39行)
└── CountdownTimer.cs  # 倒计时示例 (57行)
```

---

## 依赖

- Unity 2019.4 或更高版本
- 无外部依赖

---

## License

MIT License
