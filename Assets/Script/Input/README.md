# 热键绑定系统使用指南

## 问题说明

修改技能快捷键在编辑器正常但打包后失效的问题已修复。

## 核心修复

1. **强制刷新机制** - 绑定修改后 Disable/Enable InputAction
2. **多实例同步** - 同步到所有 PlayerInput 和 InputActionAsset
3. **双重刷新** - 事件广播 + 直接查找快捷栏

## 快速使用（2步）

### 步骤 1: 添加引导脚本（可选但推荐）

1. 在主场景创建空 GameObject，命名 `InputBootstrap`
2. 附加 `InputBindingBootstrap.cs` 脚本

### 步骤 2: 测试

1. 运行游戏，修改热键
2. 观察快捷栏是否立即更新
3. 重启游戏验证设置保留

## 工作原理

```
修改热键
    ↓
保存到 PlayerPrefs
    ↓
同步到所有 PlayerInput 实例
    ↓
强制刷新 InputAction (Disable/Enable)
    ↓
更新快捷栏UI显示
```

## 打包测试

1. 打包游戏
2. 修改热键
3. 重启验证保留

## 故障排查

### 问题：修改后 UI 不更新
- 检查 `HotkeySettingsPanel` 的 `slotActions` 是否赋值
- 确认快捷栏对象在场景中存在且激活

### 问题：重启后设置丢失  
- 确认添加了 `InputBindingBootstrap`
- 检查 PlayerPrefs 权限

### 问题：打包后完全不工作
- 确保 InputActionAsset 已包含在构建中
- 检查 PlayerInput 组件配置

## 调试工具

使用 `InputBindingDiagnostics.cs` 进行诊断：
1. 创建 GameObject，附加脚本
2. 设置 Target Asset 和 Action Refs
3. 右键组件 → Context Menu → 选择诊断功能

## 文件说明

- `KeybindingStorage.cs` - 保存/加载绑定
- `InputBindingRuntimeSync.cs` - 同步到运行时实例
- `InputBindingBootstrap.cs` - 启动时自动加载
- `HotkeySettingsPanel.cs` - 设置面板
- `SkillQuickButtonBar.cs` - 快捷栏刷新
- `InputBindingDiagnostics.cs` - 诊断工具

## 注意事项

1. **必需**: `slotActions` 必须正确赋值
2. **必需**: 场景中需要 PlayerInput 组件
3. **推荐**: 添加 InputBindingBootstrap
4. **可选**: 使用诊断工具排查问题

---

如有问题请检查 Console 日志。
