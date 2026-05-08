// 本文件：快捷技能栏控制器
// 说明：
// SkillQuickButtonBar 负责将玩家的技能数据映射到 UI 快捷栏槽位（SkillQuickMod），
// 建立槽位显示、加载/刷新快捷键文本、处理输入系统按键与槽位映射、以及保存/加载快捷栏布局。
// 本文件的注释以中文为主，关键公有方法均使用 XML 注释以便编辑器/IDE 能显示说明。


using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using System;
using System.Collections;

public class SkillQuickButtonBar : MonoBehaviour
{
    [Header("配置")]
    [SerializeField] private Transform skillModContainer; // 容器：用于在运行或编辑器中按层级查找 SkillQuickMod 子项
    [Tooltip("与快捷栏槽位一一对应的输入动作（新输入系统）。用于显示快捷键文本、在设置面板中进行重绑定。")]
    [SerializeField] private InputActionReference[] slotActions; // 每个槽位对应的 InputActionReference（用于显示与触发）
    [Header("映射修正")]
    [Tooltip("当为 true 时，slotActions[0] 将映射到最后一个槽位，从而修正在层级/布局导致的视觉与数组顺序反转问题。")]
    [SerializeField] private bool reverseBindingOrder = false; // 允许在 Inspector 中修正反向绑定的问题

    // 新增：对外暴露只读访问，供设置面板读取并保持映射一致
    public bool ReverseBindingOrder => reverseBindingOrder;

    private SkillController _controller; // 运行时注入的玩家 SkillController
    [SerializeField] private List<SkillQuickMod> _mods = new(); // 快捷栏槽位列表（UI 元素）

    // ✅ PlayerInput 缓存（性能优化）
    private static PlayerInput[] _cachedPlayerInputs;
    private static bool _playerInputsCacheDirty = true;

    [Header("调试")]
    [SerializeField] private bool debugHotkeyMapping = false; // 调试开关：打印绑定映射信息
    [SerializeField] private bool debugQuickbarLogs = true;    // 调试开关：打印简洁运行日志

    private GlobalCooldownController _subscribedGcd; // 当前订阅的全局冷却控制器引用

    [Header("运行时兜底")]
    [Tooltip("在运行时为 InputAction 订阅 performed 回调，避免 UnityEvent 配置缺失导致的按键不触发（不会禁用动作，仅 Enable）。")]
    [SerializeField] private bool fallbackRuntimeWire = true;

    // 兜底布线用缓存
    private readonly List<InputAction> _wiredRuntimeActions = new();
    private readonly System.Collections.Generic.Dictionary<InputAction, System.Action<InputAction.CallbackContext>> _runtimeCallbacks = new();
    private readonly System.Collections.Generic.Dictionary<int, int> _lastCastFrameByActionIndex = new();

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 在编辑器中保持 _mods 顺序与容器子对象一致，避免序列化列表与层级顺序不一致导致的显示/绑定错位
        TryPopulateModsFromContainer();
    }
#endif

    private void Awake()
    {
        // 启动时尝试根据容器构建或校正 _mods 的顺序
        TryPopulateModsFromContainer();
    }

    /// <summary>
    /// 如果指定了 skillModContainer，则按其子物体顺序收集 SkillQuickMod 并赋值给 _mods。
    /// 仅在容器中至少找到一个 SkillQuickMod 时覆盖已序列化的 _mods，避免无意覆盖手动配置。
    /// </summary>
    private void TryPopulateModsFromContainer()
    {
        if (skillModContainer == null) return;
        var list = new List<SkillQuickMod>();
        for (int i = 0; i < skillModContainer.childCount; i++)
        {
            var child = skillModContainer.GetChild(i);
            if (child == null) continue;
            var mod = child.GetComponent<SkillQuickMod>();
            if (mod != null) list.Add(mod);
        }
        if (list.Count > 0)
        {
            _mods = list;
        }
    }


    private void OnEnable()
    {
        // 进入场景/启用时同步一次已保存的重绑定
        InputBindingRuntimeSync.ApplySavedOverridesToAllPlayersExisting();
        if (debugQuickbarLogs) Debug.Log("[热键] 快捷栏启用：同步覆盖");

        // 兜底：运行时为动作订阅 performed，避免 UnityEvent 在构建后未触发的风险
        FallbackWireActions();

        // 简要打印当前 PlayerInput 控制方案（用于定位实机问题）
        try
        {
            var players = FindAllPlayerInputs();
            foreach (var pi in players)
            {
                if (pi == null) continue;
            }
        }
        catch { }
    }


    // private void Start()
    // {
    //     // 进入场景/启用时同步一次已保存的重绑定
    //     InputBindingRuntimeSync.ApplySavedOverridesToAllPlayersExisting();
    //     if (debugQuickbarLogs) Debug.Log("[热键] 快捷栏启用：同步覆盖");

    //     // 兜底：运行时为动作订阅 performed，避免 UnityEvent 在构建后未触发的风险
    //     FallbackWireActions();

    //     // 简要打印当前 PlayerInput 控制方案（用于定位实机问题）
    //     try
    //     {
    //         var players = FindAllPlayerInputs();
    //         foreach (var pi in players)
    //         {
    //             if (pi == null) continue;
    //         }
    //     }
    //     catch { }
    // }


    // // --------------------------
    // // ------ NEW CODE ----------
    // private void OnEnable()
    // {
    //     // 进入场景/启用时同步一次已保存的重绑定
    //     InputBindingRuntimeSync.ApplySavedOverridesToAllPlayersExisting();
    //     if (debugQuickbarLogs) Debug.Log("[热键] 快捷栏启用：同步覆盖");

    //     // ✅ 延迟一帧，等待所有 PlayerInput 完全初始化
    //     StartCoroutine(DeferredWireActions());
    // }

    // private IEnumerator DeferredWireActions()
    // {
    //     yield return null; // 等待下一帧，所有 PlayerInput.OnEnable() 都已执行

    //     // 兜底：运行时为动作订阅 performed，避免 UnityEvent 在构建后未触发的风险
    //     FallbackWireActions();

    //     // 简要打印当前 PlayerInput 控制方案（用于定位实机问题）
    //     try
    //     {
    //         var players = FindAllPlayerInputs();
    //         foreach (var pi in players)
    //         {
    //             if (pi == null) continue;
    //             Debug.Log($"[热键] 找到 PlayerInput: {pi.name}, 启用={pi.enabled}, 控制方案={pi.currentControlScheme}");
    //         }
    //     }
    //     catch { }
    // }
    // // --------------------------、



    // private IEnumerator DeferredWireActions()
    // {
    //     yield return null; // 等待下一帧

    //     Debug.Log("=== [热键] 开始调试 PlayerInput 状态 ===");

    //     var players = FindAllPlayerInputs();
    //     Debug.Log($"[热键] 找到 {players.Length} 个 PlayerInput");

    //     foreach (var pi in players)
    //     {
    //         Debug.Log($"[热键] PlayerInput 名称: {pi.name}");
    //         Debug.Log($"[热键]   启用状态: {pi.enabled}");
    //         Debug.Log($"[热键]   当前控制方案: {pi.currentControlScheme}");

    //         // ✅ 如果是玩家角色且控制方案为空，禁用自动设备分配避免冲突
    //         if (string.IsNullOrEmpty(pi.currentControlScheme) && pi.name.Contains("模型"))
    //         {
    //             Debug.LogWarning($"[热键] 玩家角色 {pi.name} 控制方案为空，禁用其 PlayerInput 以避免冲突");
    //             pi.enabled = false;
    //         }

    //         // 检查所有 InputAction 的状态
    //         if (pi.actions != null)
    //         {
    //             foreach (var action in pi.actions)
    //             {
    //                 Debug.Log($"[热键]     Action: {action.name}, 启用: {action.enabled}");
    //             }
    //         }
    //     }

    //     Debug.Log("=== [热键] 开始布线 ===");
    //     FallbackWireActions();

    //     // 检查布线结果
    //     Debug.Log($"[热键] 已订阅 {_wiredRuntimeActions.Count} 个 InputAction");
    //     foreach (var action in _wiredRuntimeActions)
    //     {
    //         Debug.Log($"[热键]   Action: {action.name}, 启用: {action.enabled}");
    //     }

    //     Debug.Log("=== [热键] 调试结束 ===");
    // }




    private void OnDisable()
    {
        if (debugQuickbarLogs) Debug.Log("[热键] 快捷栏禁用");
        UnsubscribeFromGcd();
        UnwireFallbackActions();
    }

    /// <summary>
    /// 在玩家实例创建后由外部调用（例如 MapManager），进行与玩家相关的初始化。
    /// - 注入 SkillController
    /// - 构建或刷新快捷栏 UI
    /// - 订阅 GCD（全局冷却）事件
    /// </summary>
    /// <param name="playerInstance">玩家 GameObject，必须包含 SkillController</param>
    public void Init(CharacterState playerInstance)
    {
        if (playerInstance == null) return;
        var controller = playerInstance.GetComponent<SkillController>();
        if (controller == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning("[SkillQuickButtonBar] Init: player 上未找到 SkillController");
#endif
            return;
        }

        // 取消旧订阅
        UnsubscribeFromGcd();

        _controller = controller;
        var snap = controller.GetAllSkillsSnapshot();
        if (snap != null && snap.Count > 0)
        {
            BuildBar(controller);
        }
        else
        {
            // 请求 SkillManager 刷新（稍后通过 OnSkillsInitialized 回调构建）
            controller.RefreshSkills();
        }

        // 订阅玩家的 GCD 事件并转发给各 mod
        SubscribeToGcd(controller.GlobalCooldown);

        // 输入触发：由 UnityEvent + 兜底布线共同保障
    }

    /// <summary>
    /// 供事件系统/ScriptableObject 回调使用：当 SkillController 快照准备好后构建快捷栏（SO 事件绑定使用）。
    /// </summary>
    /// <param name="controller">触发事件的 SkillController</param>
    public void OnSkillsInitialized(SkillController controller)
    {
        _controller = controller;
        BuildBar(controller);
    }

    /// <summary>
    /// 为每个 SkillQuickMod 构建或刷新显示（不包含普通攻击），并加载用户保存的布局与热键显示。
    /// </summary>
    private void BuildBar(SkillController controller)
    {
        print("开始创建快捷栏");

        var snapshot = controller.GetAllSkillsSnapshot();
        if (snapshot == null || snapshot.Count == 0) return;

        // 读取已保存的布局（槽位 -> 技能ID 顺序）
        var savedOrder = QuickbarLayoutStorage.LoadOrder() ?? new List<string>();

        // 仅使用非普通攻击的技能参与快捷栏
        var allSkills = new List<PlayerSkill>(snapshot.Values.Where(ps => ps.SkillSO.skillType != SkillEffectType.普通攻击));
        var used = new HashSet<string>();
        var finalSkillIDs = new List<string>(_mods.Count);

        // 优先按照已保存布局填充
        foreach (var skillId in savedOrder)
        {
            if (finalSkillIDs.Count >= _mods.Count) break;
            if (snapshot.ContainsKey(skillId) && used.Add(skillId)) finalSkillIDs.Add(skillId);
        }

        // 用剩余技能补齐（按稳定规则排序以保证一致性）
        var remain = allSkills.Where(s => !used.Contains(s.SkillSO.SkillID)).ToList();
        remain.Sort((a, b) =>
        {
            int at = a.SkillSO.skillType == SkillEffectType.普通攻击 ? 0 : 1;
            int bt = b.SkillSO.skillType == SkillEffectType.普通攻击 ? 0 : 1;
            int t = at.CompareTo(bt);
            if (t != 0) return t;
            return a.SkillSO.cooldown.CompareTo(b.SkillSO.cooldown);
        });
        foreach (var ps in remain)
        {
            if (finalSkillIDs.Count >= _mods.Count) break;
            finalSkillIDs.Add(ps.SkillSO.SkillID);
        }

        // 将技能绑定到槽位 UI
        for (int i = 0; i < _mods.Count; i++)
        {
            if (i < finalSkillIDs.Count)
            {
                _mods[i].Init(finalSkillIDs[i], controller);
                _mods[i].gameObject.SetActive(true);
            }
            else
            {
                _mods[i].gameObject.SetActive(false);
            }
        }

        // 加载本地键位覆盖（如果有）并刷新热键标签
        KeybindingStorage.LoadOverrides(slotActions);
        RefreshHotkeyLabels();
    }

    /// <summary>
    /// 将索引对应槽位的按钮触发一次（用于热键触发）。
    /// </summary>
    public void CastSkillByIndex(int index)
    {
        if (_controller == null) return;
        if (index < 0 || index >= _mods.Count) return;
        if (!_mods[index].gameObject.activeInHierarchy) return;
        var btn = _mods[index].GetComponentInChildren<Button>();
        if (btn != null) btn.onClick.Invoke();
    }

    // 订阅/转发 GCD（Global Cooldown）相关事件：把全局冷却状态转发到每个 mod
    private void SubscribeToGcd(GlobalCooldownController gcd)
    {
        UnsubscribeFromGcd();
        if (gcd == null) return;
        _subscribedGcd = gcd;
        gcd.GcdStarted += OnGcdStartedForward;
        gcd.GcdUpdated += OnGcdUpdatedForward;
        gcd.GcdEnded += OnGcdEndedForward;
    }
    private void UnsubscribeFromGcd()
    {
        if (_subscribedGcd == null) return;
        _subscribedGcd.GcdStarted -= OnGcdStartedForward;
        _subscribedGcd.GcdUpdated -= OnGcdUpdatedForward;
        _subscribedGcd.GcdEnded -= OnGcdEndedForward;
        _subscribedGcd = null;
    }

    private void OnGcdStartedForward(float duration)
    {
        if (_mods == null) return;
        foreach (var mod in _mods)
        {
            if (mod == null) continue;
            mod.OnGcdStarted(duration);
        }
    }
    private void OnGcdUpdatedForward(float remaining)
    {
        if (_mods == null) return;
        foreach (var mod in _mods)
        {
            if (mod == null) continue;
            mod.OnGcdUpdated(remaining);
        }
    }
    private void OnGcdEndedForward()
    {
        if (_mods == null) return;
        foreach (var mod in _mods)
        {
            if (mod == null) continue;
            mod.OnGcdEnded();
        }
    }

    // ========== UnityEvent 回调入口：由 InputAction 的 UnityEvent 直接调用 ==========
    // 注意：这些索引是“动作数组”的索引，将根据 reverseBindingOrder 映射到 UI 槽位
    public void OnQuickSlot0(InputAction.CallbackContext ctx) { if (ctx.performed) CastByMappedActionIndex(0); }
    public void OnQuickSlot1(InputAction.CallbackContext ctx) { if (ctx.performed) CastByMappedActionIndex(1); }
    public void OnQuickSlot2(InputAction.CallbackContext ctx) { if (ctx.performed) CastByMappedActionIndex(2); }
    public void OnQuickSlot3(InputAction.CallbackContext ctx) { if (ctx.performed) CastByMappedActionIndex(3); }
    public void OnQuickSlot4(InputAction.CallbackContext ctx) { if (ctx.performed) CastByMappedActionIndex(4); }

    private void CastByMappedActionIndex(int actionIndex)
    {
        if (_mods == null || _mods.Count == 0) return;
        int mappedIndex = reverseBindingOrder ? (_mods.Count - 1 - actionIndex) : actionIndex;
        if (debugHotkeyMapping) Debug.Log($"[热键] 触发: 动作索引={actionIndex} -> 槽位={mappedIndex}");
        CastSkillByIndex(mappedIndex);
    }

    private void SafeCastByActionIndex(int actionIndex)
    {
        int frame = Time.frameCount;
        if (_lastCastFrameByActionIndex.TryGetValue(actionIndex, out var last) && last == frame)
        {
            // 同一帧已触发（可能来自 UnityEvent），避免重复施放
            return;
        }
        _lastCastFrameByActionIndex[actionIndex] = frame;
        CastByMappedActionIndex(actionIndex);
    }

    private void FallbackWireActions()
    {
        if (!fallbackRuntimeWire) return;
        UnwireFallbackActions();
        if (slotActions == null || slotActions.Length == 0) return;

        int count = Mathf.Min(slotActions.Length, _mods.Count);
        for (int i = 0; i < count; i++)
        {
            Debug.Log($"[热键] 处理槽位 {i}: slotActions[{i}] = {slotActions[i]?.action?.name ?? "null"}");

            var action = ResolveRuntimeAction(slotActions[i]);
            if (action == null)
            {
                Debug.LogWarning($"[热键] 槽位 {i}: 无法解析 InputAction，跳过");
                continue;
            }

            Debug.Log($"[热键] 槽位 {i}: 解析到动作 {action.name}, 当前启用状态={action.enabled}");

            int idx = i;
            System.Action<InputAction.CallbackContext> cb = (ctx) =>
            {
                if (ctx.performed)
                {
                    Debug.Log($"[热键] 动作 {action.name} 被触发！槽位索引={idx}");
                    SafeCastByActionIndex(idx);
                }
            };

            action.performed += cb;
            if (!action.enabled)
            {
                action.Enable();
                Debug.Log($"[热键] 槽位 {i}: 已启用动作 {action.name}");
            }

            _wiredRuntimeActions.Add(action);
            _runtimeCallbacks[action] = cb;
            Debug.Log($"[热键] 槽位 {i}: 布线成功");
        }
        if (debugQuickbarLogs) Debug.Log($"[热键] 兜底布线完成: 动作数={_wiredRuntimeActions.Count}");
    }

    private void UnwireFallbackActions()
    {
        foreach (var kv in _runtimeCallbacks)
        {
            var action = kv.Key; var cb = kv.Value;
            if (action != null && cb != null) action.performed -= cb;
        }
        _runtimeCallbacks.Clear();
        _wiredRuntimeActions.Clear();
        _lastCastFrameByActionIndex.Clear();
    }

    // 解析运行时动作（优先使用玩家的 PlayerInput），否则回退到引用的资产动作
    private InputAction ResolveRuntimeAction(InputActionReference aref)
    {

        if (aref == null || aref.action == null) return null;
        string actionName = aref.action.name;
        try
        {
            var players = FindAllPlayerInputs();
            foreach (var pi in players)
            {
                if (pi == null) continue;

                InputActionAsset asset = null;

                // #if UNITY_2023_2_OR_NEWER
                //                 asset = pi.GetComponent<InputActionAsset>();
                // #else
                try { asset = pi.actions; } catch { }
                // #endif

                if (asset == null) continue;
                var act = asset.FindAction(actionName, false);
                if (act != null) return act;
            }
        }
        catch { }
        return aref.action;
    }


    private static PlayerInput[] FindAllPlayerInputs()
    {
        if (_playerInputsCacheDirty || _cachedPlayerInputs == null)
        {
#if UNITY_2023_1_OR_NEWER
            _cachedPlayerInputs = UnityEngine.Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
#else
            _cachedPlayerInputs = UnityEngine.Object.FindObjectsOfType<PlayerInput>(true);
#endif
            _playerInputsCacheDirty = false;
        }
        return _cachedPlayerInputs;
    }

    /// <summary>
    /// 在运行时（或设置面板）刷新所有槽位的快捷键显示文本，保持显示与实际绑定一致。
    /// </summary>
    public void RefreshHotkeyLabels()
    {
        // 重新加载保存的覆盖到资产
        KeybindingStorage.LoadOverrides(slotActions);

        // 同步到所有 PlayerInput 实例，使运行时动作获得最新覆盖
        InputBindingRuntimeSync.ApplySavedOverridesToAllPlayersFor(slotActions);

        if (slotActions == null || slotActions.Length == 0) return;

        int count = Mathf.Min(slotActions.Length, _mods.Count);

        for (int i = 0; i < count; i++)
        {
            int mappedIndex = reverseBindingOrder ? (_mods.Count - 1 - i) : i;
            var actionRef = slotActions[i];
            if (actionRef == null || actionRef.action == null) continue;

            // 生成显示文本（直接基于当前覆盖）
            string display = HotkeyDisplayUtility.GetActionDisplayString(actionRef);

            // 更新槽位显示
            _mods[mappedIndex].SetHotkeyLabel(display);

            if (debugHotkeyMapping)
            {
                Debug.Log($"[热键] 标签: 动作索引={i} -> 槽位={mappedIndex} 文本={display}");
            }
        }

        if (debugQuickbarLogs) Debug.Log("[快捷栏] 热键标签已刷新");
    }

    /// <summary>
    /// 运行时修改绑定顺序（例如设置界面中动态修正），会刷新显示。
    /// </summary>
    public void SetReverseBindingOrder(bool reverse)
    {
        if (reverseBindingOrder == reverse) return;
        reverseBindingOrder = reverse;
        RefreshHotkeyLabels();
    }

    /// <summary>
    /// 将当前 UI 顺序（槽位 -> 技能 ID）保存到本地（用于下次还原）。
    /// </summary>
    public void SaveCurrentLayout()
    {
        var order = new List<string>(_mods.Count);
        foreach (var mod in _mods)
        {
            if (mod.gameObject.activeInHierarchy)
                order.Add(mod.GetSkillID());
        }
        QuickbarLayoutStorage.SaveOrder(order);
    }
}

/// <summary>
/// 快捷栏布局存取：仅保存技能ID顺序。
/// </summary>
public static class QuickbarLayoutStorage
{
    private const string Key = "QuickbarLayout.Order";

    public static void SaveOrder(List<string> skillIDs)
    {
        if (skillIDs == null) return;
        var data = new QuickbarOrderDTO { slots = skillIDs };
        var json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(Key, json);
        PlayerPrefs.Save();
    }

    public static List<string> LoadOrder()
    {
        if (!PlayerPrefs.HasKey(Key)) return null;
        var json = PlayerPrefs.GetString(Key, string.Empty);
        if (string.IsNullOrEmpty(json)) return null;
        var data = JsonUtility.FromJson<QuickbarOrderDTO>(json);
        return data?.slots ?? null;
    }

    [System.Serializable]
    private class QuickbarOrderDTO
    {
        public List<string> slots;
    }
}

/// <summary>
/// 输入动作显示辅助：统一拿绑定的可读字符串。
/// </summary>
public static class HotkeyDisplayUtility
{
    // 首选：给定运行时动作，基于“有覆盖的条目”或“键盘设备条目”构造显示。
    public static string GetActionDisplayString(InputAction action)
    {
        if (action == null) return string.Empty;
        try
        {
            // 1) 若存在复合绑定（modifier+binding），优先使用带覆盖的部分；否则用有效路径
            if (TryBuildCompositeDisplay(action, out var compositeDisplay))
                return compositeDisplay;

            // 2) 非复合：优先带 override 的条目；否则优先键盘设备；最后兜底第一个非复合条目
            int bestIdx = FindBestNonCompositeBindingIndex(action);
            if (bestIdx >= 0)
            {
                var path = action.bindings[bestIdx].effectivePath;
                if (!string.IsNullOrEmpty(path))
                    return Humanize(path);
            }

            // 3) 再兜底：内置 API 或 Action 名称
            string fallback = action.GetBindingDisplayString();
            return string.IsNullOrEmpty(fallback) ? action.name : fallback;
        }
        catch
        {
            return action.name;
        }
    }

    public static string GetActionDisplayString(InputActionReference actionRef)
    {
        return GetActionDisplayString(actionRef != null ? actionRef.action : null);
    }

    private static bool TryBuildCompositeDisplay(InputAction action, out string display)
    {
        display = null;
        var bs = action.bindings;
        for (int i = 0; i < bs.Count; i++)
        {
            if (!bs[i].isComposite) continue;
            // 找到包含 modifier/binding 两个 part 的复合
            int mod = -1, bind = -1;
            string modPath = null, bindPath = null;
            for (int j = i + 1; j < bs.Count && bs[j].isPartOfComposite; j++)
            {
                var part = bs[j];
                if (string.Equals(part.name, "modifier", System.StringComparison.OrdinalIgnoreCase))
                {
                    mod = j;
                    modPath = !string.IsNullOrEmpty(part.overridePath) ? part.overridePath : part.effectivePath;
                }
                else if (string.Equals(part.name, "binding", System.StringComparison.OrdinalIgnoreCase))
                {
                    bind = j;
                    bindPath = !string.IsNullOrEmpty(part.overridePath) ? part.overridePath : part.effectivePath;
                }
            }
            if (mod >= 0 && bind >= 0)
            {
                if (!string.IsNullOrEmpty(modPath) && !string.IsNullOrEmpty(bindPath))
                {
                    display = Humanize(modPath) + "+" + Humanize(bindPath);
                    return true;
                }
            }
        }
        return false;
    }

    private static int FindBestNonCompositeBindingIndex(InputAction action)
    {
        int firstNonComposite = -1;
        int firstKeyboard = -1;
        int firstOverride = -1;
        var bs = action.bindings;
        for (int i = 0; i < bs.Count; i++)
        {
            var b = bs[i];
            if (b.isComposite || b.isPartOfComposite) continue;
            if (firstNonComposite < 0) firstNonComposite = i;
            // 有覆盖优先
            if (!string.IsNullOrEmpty(b.overridePath) && firstOverride < 0)
            {
                firstOverride = i;
            }
            // 键盘优先（若无覆盖）
            var path = !string.IsNullOrEmpty(b.overridePath) ? b.overridePath : b.effectivePath;
            if (firstKeyboard < 0 && !string.IsNullOrEmpty(path) && path.StartsWith("<Keyboard>", System.StringComparison.OrdinalIgnoreCase))
            {
                firstKeyboard = i;
            }
        }
        if (firstOverride >= 0) return firstOverride;
        if (firstKeyboard >= 0) return firstKeyboard;
        return firstNonComposite;
    }

    private static string Humanize(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        return UnityEngine.InputSystem.InputControlPath.ToHumanReadableString(
            path,
            UnityEngine.InputSystem.InputControlPath.HumanReadableStringOptions.OmitDevice |
            UnityEngine.InputSystem.InputControlPath.HumanReadableStringOptions.UseShortNames
        );
    }
}
