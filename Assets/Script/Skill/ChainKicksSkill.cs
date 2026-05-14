using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEditor; // 用于 Selection

/// <summary>
/// 连环踢（3段连击）：
/// - 每次按下触发一次阶段；若在输入窗口内再次按下，进入下一段，否则结束并开始冷却。
/// - 必须等待上一段动画播放完成后才能进入下一段（动画时段内的按下将被忽略）。
/// - 伤害在每段动画开始后的短延迟进行判定，范围为角色前方扇形/球形区域。
/// - 释放期间锁定玩家控制（不能移动/翻滚/跳跃）。
/// - 若存在锁定目标，将先朝向锁定目标再出招。
/// - 每段踢击伤害递增15%（第1段100%，第2段115%，第3段132.25%）
/// - Lv5：最后一击必定暴击，造成200%伤害。
/// - Lv10：最后一击后生成旋风（5s），每秒造成与单段相同的伤害；有敌人时追击，无敌人时游荡。
/// 
/// 注意：
/// - 为了与现有控制器兼容，本技能在开始时将重置技能冷却（覆盖控制器提前设置的冷却），
///   并在连击实际结束时再设置冷却时间。
/// - 多次按键会多次实例化该Prefab；本脚本通过静态字典确保只有“第一个实例”作为编排者，
///   其余实例仅用来登记“下一段输入”。
/// </summary>
public class ChainKicksSkill : Skill
{
    [Header("Animator Triggers")]
    [SerializeField] private string stage1Trigger = "ChainKick1";
    [SerializeField] private string stage2Trigger = "ChainKick2";
    [SerializeField] private string stage3Trigger = "ChainKick3";

    [Header("Stage Timings (seconds)")]
    [SerializeField] private float stage1AnimTime = 0.6f;
    [SerializeField] private float stage2AnimTime = 0.6f;
    [SerializeField] private float stage3AnimTime = 0.8f;
    [SerializeField] private float stage1HitDelay = 0.2f;
    [SerializeField] private float stage2HitDelay = 0.25f;
    [SerializeField] private float stage3HitDelay = 0.3f;

    [Header("Combo Window")]
    [Tooltip("每段动画结束后允许再次按下的窗口（秒）")]
    [SerializeField] private float inputWindow = 0.7f;

    [Header("Area Hit Config")]
    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private float forwardOffset = 1.0f;
    [SerializeField] private float forwardOffsetY = 1.0f;
    [SerializeField] private float hitWidth = 1.2f;
    [SerializeField] private float hitLength = 1.6f;

    [Header("VFX (optional)")]
    [SerializeField] private GameObject whirlwindPrefab; // Lv10 旋风
    [SerializeField] private float whirlwindSpawnForward = 1.2f;

    // 性能优化：重用缓冲区，避免每次 OverlapSphere 分配数组
    private const int DefaultOverlapBufferSize = 32;
    private readonly Collider[] _overlapBuffer = new Collider[DefaultOverlapBufferSize];

    // 性能：大缓冲用于在默认缓冲装满时的二次NonAlloc扫描
    private static readonly Collider[] _largeBuffer = new Collider[128];

    // 高度半扩展（Y轴）用于盒形检测的垂直范围
    private float _boxHalfHeight = 1.2f;

    private static readonly Dictionary<Transform, ChainKicksSkill> SActive = new();

    public static bool IsActive(Transform caster)
    {
        return caster != null && SActive.ContainsKey(caster);
    }
    public static bool RegisterPressIfActive(Transform caster)
    {
        if (caster != null && SActive.TryGetValue(caster, out var inst) && inst != null)
        {
            return inst.TryQueuePress();
        }
        return false;
    }

    // 新增：阶段/结束事件（由 SkillController 订阅来启动公共冷却/做统计，不让技能直接依赖GCD组件）
    public static event System.Action<Transform, int, bool> StageCompleted; // (施放者, 阶段序号1..3, 是否最后一段)
    public static event System.Action<Transform> ComboEnded; // (施放者)

    private CharacterAnimationController _animController;
    private MoveMent _move;
    private LockOnController _lockOn;
    private bool _nextPressQueued;
    private bool _inWindow;
    private bool _pendingWindowRequest; // 新增：等待外部开启窗口
    // 新增：引用 SkillController 以便在每段开始时触发公共冷却
    private SkillController _skillController;

    public override void Execute(Transform caster, PlayerSkill playerSkill)
    {
        base.Execute(caster, playerSkill);
        if (Caster == null || PlayerSkill == null || PlayerSkill.SkillSO == null)
        {
            Destroy(gameObject);
            return;
        }

        // 获取组件引用：通过玩家上的 SkillController 获取 CharacterAnimationController
        var sc = Caster.GetComponent<SkillController>();
        _skillController = sc; // 缓存以便触发GCD
        _animController = sc != null ? sc.AnimationController : Caster.GetComponent<CharacterAnimationController>();
        _move = Caster.GetComponent<CharacterState>()?.Movement; // 通过 CharacterState 获取 MoveMent，解耦
        _lockOn = Caster.GetComponent<LockOnController>();

        // 若已有编排者在运行，则仅登记“下一段按下”并立即销毁
        if (SActive.TryGetValue(Caster, out var active) && active != null && active != this)
        {
            active.TryQueuePress(); // 只有在窗口内才会登记
            if (PlayerSkill != null) PlayerSkill.CooldownTimer = 0f; // 防止UI提前冷却
            Destroy(gameObject);
            return;
        }

        SActive[Caster] = this;
        PlayerSkill.CooldownTimer = 0f; // 真正结束时再设置
        // 播放三段踢释放音效（只在首个实例）
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayWeaponSound(SkillSoundType.三段踢释放);
        }
        DoComboAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private bool TryQueuePress()
    {
        if (_inWindow)
        {
            _nextPressQueued = true;
            return true;
        }
        return false; // 不在输入窗口内则返回失败，允许外层显示GCD提示
    }

    private async UniTaskVoid DoComboAsync(CancellationToken token)
    {
        // 锁定玩家控制通过动画控制器统一入口
        _animController?.LockPlayerControl();
        const int maxStages = 3;
        try
        {
            for (int stage = 1; stage <= maxStages; stage++)
            {
                token.ThrowIfCancellationRequested();
                _nextPressQueued = false;
                _inWindow = false;
                _pendingWindowRequest = false;

                FaceLockOnTarget();

                // 每段开始时触发一次公共冷却（确保每次按下都会触发GCD，且不重复触发）
                _skillController?.GlobalCooldown?.StartGCD();

                // 使用动画控制器触发阶段动画
                string trig = stage == 1 ? stage1Trigger : (stage == 2 ? stage2Trigger : stage3Trigger);
                if (!string.IsNullOrEmpty(trig))
                {
                    _animController?.TriggerByName(trig);
                }

                float hitDelay = GetHitDelay(stage);
                float animTime = GetAnimTime(stage);
                if (hitDelay > 0f) await UniTask.Delay(TimeSpan.FromSeconds(hitDelay), cancellationToken: token);
                DoAreaDamageForStage(stage);
                float remain = Mathf.Max(0f, animTime - hitDelay);
                if (remain > 0f) await UniTask.Delay(TimeSpan.FromSeconds(remain), cancellationToken: token);

                bool isLast = stage >= maxStages;
                StageCompleted?.Invoke(Caster, stage, isLast);
                if (isLast) break;

                // 等待控制器通知开启窗口
                await UniTask.WaitWhile(() => !_pendingWindowRequest, cancellationToken: token);

                _inWindow = true;
                float elapsed = 0f;
                while (elapsed < inputWindow)
                {
                    if (_nextPressQueued) break;
                    elapsed += Time.deltaTime;
                    await UniTask.Yield(token);
                }
                _inWindow = false;
                if (!_nextPressQueued) break;
            }

            float cd = SkillManager.Instance != null
                ? SkillManager.Instance.GetCooldownAtLevel(PlayerSkill.SkillSO, PlayerSkill.Level)
                : PlayerSkill.SkillSO.cooldown * Mathf.Max(0f, 1f - PlayerSkill.SkillSO.perLevelCooldownReducePercent * PlayerSkill.Level);
            PlayerSkill.CooldownTimer = cd;

            if (PlayerSkill.Level >= 10 && whirlwindPrefab != null) SpawnWhirlwind();
            ComboEnded?.Invoke(Caster);
        }
        catch (OperationCanceledException)
        {
            // 技能被取消时不做特殊处理
        }
        finally
        {
            // 通过动画控制器解除控制锁
            _animController?.UnlockPlayerControl();
            if (SActive.TryGetValue(Caster, out var me) && me == this) SActive.Remove(Caster);
            Destroy(gameObject);
        }
    }

    private void FaceLockOnTarget()
    {
        if (_lockOn == null) return;
        var monster = _lockOn.GetCurrentTarget();
        if (monster == null) return;
        Vector3 dir = monster.transform.position - Caster.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
        {
            Caster.rotation = Quaternion.LookRotation(dir.normalized);
        }
    }

    private float GetHitDelay(int stage) => stage == 1 ? stage1HitDelay : (stage == 2 ? stage2HitDelay : stage3HitDelay);
    private float GetAnimTime(int stage) => stage == 1 ? stage1AnimTime : (stage == 2 ? stage2AnimTime : stage3AnimTime);

    // 对某一段进行范围判定和伤害（已优化）
    private void DoAreaDamageForStage(int stage)
    {
        // 获取基础伤害
        float baseDmg = PlayerSkill.GetDamage();
        // 计算递增后的伤害，每段增加15%
        float stageDmg = baseDmg * Mathf.Pow(1.15f, stage - 1);
        bool stage3Crit = (stage == 3 && PlayerSkill.Level >= 5);

        Vector3 casterPos = Caster.position;
        Vector3 forward = Caster.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.0001f) forward.Normalize();

        Vector3 center = casterPos + forward * Mathf.Max(0f, forwardOffset);
        center.y += forwardOffsetY; // 添加Y轴偏移

        // 使用有方向的盒形（矩形）区域：中心在 forwardOffset + hitLength/2 处
        Vector3 boxCenter = casterPos + forward * (Mathf.Max(0f, forwardOffset) + hitLength * 0.5f);
        boxCenter.y += forwardOffsetY; // 添加Y轴偏移
        Vector3 halfExtents = new Vector3(Mathf.Max(0.01f, hitWidth * 0.5f), _boxHalfHeight, Mathf.Max(0.01f, hitLength * 0.5f));
        Quaternion orientation = Quaternion.LookRotation(forward);

        bool anyHit = false; // 标记是否真正命中（至少一个 collider 应用过伤害）
        int found = Physics.OverlapBoxNonAlloc(boxCenter, halfExtents, _overlapBuffer, orientation, enemyLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < found; i++)
        {
            var col = _overlapBuffer[i];
            if (col == null) continue;
            ApplyDamage(col.transform, stageDmg, stage3Crit);
            anyHit = true;
        }

        // 缓冲区满则再用大缓冲NonAlloc扫描一次
        if (found == _overlapBuffer.Length)
        {
            int foundLarge = Physics.OverlapBoxNonAlloc(boxCenter, halfExtents, _largeBuffer, orientation, enemyLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < foundLarge; i++)
            {
                var col = _largeBuffer[i];
                if (col == null) continue;
                ApplyDamage(col.transform, stageDmg, stage3Crit);
                anyHit = true;
            }
        }
        
        // 命中时播放打击音效
        if (anyHit && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayWeaponSound(SkillSoundType.三段踢攻击到敌人);
        }
        // 不再手动广播暴击事件：统一由 CharacterState.DealDamageTo 在触发暴击时广播
    }

    private void SpawnWhirlwind()
    {
        // 旋风持续时间与伤害：固定为5秒，每秒造成与单段相同的伤害（不含Lv5的2倍效果）
        const float duration = 5f;
        const float tickInterval = 1f; // 保留注释说明；具体由 WhirlwindBehavior 内部按秒结算
        float baseDmg = PlayerSkill != null ? PlayerSkill.GetDamage() : 0f; // 单段基础伤害
        int dps = Mathf.Max(0, Mathf.RoundToInt(baseDmg));

        // 生成位置：玩家前方一定距离
        Vector3 pos = Caster.position + Caster.forward * whirlwindSpawnForward;
        pos.y += forwardOffsetY;

        if (whirlwindPrefab == null) return;
        var go = Instantiate(whirlwindPrefab, pos, Quaternion.LookRotation(Caster.forward));

        // 确保存在 WhirlwindBehavior，如果Prefab没有，运行时添加一个
        var wb = go.GetComponent<WhirlwindBehavior>();
        if (wb == null)
        {
            wb = go.AddComponent<WhirlwindBehavior>();
        }
        // 使用消息式配置，先注入再开始，确保开局第一拍就有 caster 和参数
        wb.SetLayerMask(enemyLayers.value);
        wb.SetDamagePerSecond(dps);
        wb.SetDuration(duration);
        wb.SetCasterState(CasterState);
        wb.Begin();
    }

    // 统一的伤害应用：通过基类的 DealDamage 走暴击流程
    private void ApplyDamage(Transform target, float dmg, bool forceCrit)
    {
        DealDamage(target, dmg, forceCrit);
    }
    
    private void DrawGizmosHelper()
    {
        // 保存原始颜色和矩阵
        Color originalColor = Gizmos.color;
        Matrix4x4 originalMatrix = Gizmos.matrix;
        
        try
        {
            // 设置Gizmos颜色为半透明黄色
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            
            // 使用当前组件的Transform作为Caster，如果Caster为空
            Transform casterTransform = Caster != null ? Caster : transform;
            if (casterTransform == null) return;
            
            Vector3 casterPos = casterTransform.position;
            Vector3 forward = casterTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f) forward.Normalize();
            
            // 绘制矩形范围表示（有向盒）
            Vector3 boxCenter = casterPos + forward * (Mathf.Max(0f, forwardOffset) + hitLength * 0.5f);
            boxCenter.y += forwardOffsetY; // 添加Y轴偏移
            Vector3 halfExtents = new Vector3(Mathf.Max(0.01f, hitWidth * 0.5f), _boxHalfHeight, Mathf.Max(0.01f, hitLength * 0.5f));
            Gizmos.matrix = Matrix4x4.TRS(boxCenter, Quaternion.LookRotation(forward), Vector3.one);
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawCube(Vector3.zero, halfExtents * 2f);
            Gizmos.color = new Color(1f, 1f, 0f, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        }
        finally
        {
            // 还原Gizmos设置
            Gizmos.color = originalColor;
            Gizmos.matrix = originalMatrix;
        }
    }

    public static void RequestOpenWindow(Transform caster)
    {
        if (caster != null && SActive.TryGetValue(caster, out var inst) && inst != null)
        {
            inst._pendingWindowRequest = true;
        }
    }
}
