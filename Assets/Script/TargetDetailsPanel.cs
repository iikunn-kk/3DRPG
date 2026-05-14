using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TargetDetailsPanel : MonoBehaviour
{
    // --- Inspector fields ---
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private Image hpBar;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text distanceText;

    // --- runtime state ---
    private MonsterBase _targetMonster;
    private MonsterCombat _targetMonsterCombat;

    private CancellationTokenSource _distanceCts;
    private Transform _playerTransform;
    private float _distanceUpdateInterval = 1f;

    // --- Unity callbacks ---
    private void OnDisable()
    {
        // Ensure async tasks and references cleaned up when panel disabled
        if (_distanceCts != null)
        {
            _distanceCts.Cancel();
            _distanceCts.Dispose();
            _distanceCts = null;
        }

        _targetMonster = null;
        _targetMonsterCombat = null;
    }

    // 新增：每帧守护，若目标已被销毁或死亡，自动隐藏面板
    private void Update()
    {
        if (!gameObject.activeInHierarchy) return;
        if (_targetMonster == null || _targetMonsterCombat == null)
        {
            Hide();
            return;
        }
        if (_targetMonsterCombat.IsDead)
        {
            Hide();
            return;
        }
    }

    // --- Public API (single entry point) ---
    /// <summary>
    /// 主入口：设置或切换当前显示的目标信息。
    /// - 如果传入 null 则隐藏面板。
    /// - 如果当前没有目标则加载目标信息。
    /// - 如果当前已有目标且与传入不同，则切换到新目标（清理旧状态并初始化新状态）。
    /// </summary>
    public void Init(MonsterBase targetMonster)
    {
        // Null 表示取消锁定 / 隐藏
        if (targetMonster == null)
        {
            Hide();
            return;
        }

        // 如果和当前目标相同，则只刷新显示
        if (_targetMonster == targetMonster)
        {
            Refresh();
            return;
        }

        // 切换目标：先清理上一个目标的异步任务和引用（如果有）
        if (_distanceCts != null)
        {
            _distanceCts.Cancel();
            _distanceCts.Dispose();
            _distanceCts = null;
        }

        _targetMonster = targetMonster;
        _targetMonsterCombat = targetMonster.GetComponent<MonsterCombat>();

        // 如果没有 MonsterCombat，则无法显示血量信息，隐藏面板
        if (_targetMonsterCombat == null)
        {
            Hide();
            return;
        }

        // 缓存玩家位置（优先查找带 "Player" Tag 的对象，回退至主摄像机）
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            _playerTransform = playerObj.transform;
        else if (Camera.main != null)
            _playerTransform = Camera.main.transform;
        else
            _playerTransform = null;

        // 启动距离显示异步任务
        _distanceCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        DistanceUpdateLoopAsync(_distanceCts.Token).Forget();

        Refresh();
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 外部调用：更新当前显示目标的血量（通常由订阅目标的 MonsterCombat 或控制器转发）
    /// </summary>
    public void UpdateHealth(int current, int max)
    {
        if (hpText != null)
            hpText.text = $"HP: {current}/{max}";
        if (hpBar != null)
            hpBar.fillAmount = (max > 0) ? (float)current / max : 0f;
    }

    /// <summary>
    /// 若事件系统以 payload 形式广播血量变化，保留此方法供转发器调用
    /// </summary>
    public void HandleTargetHealthRaised(TargetHealthPayload payload)
    {
        // TargetHealthPayload is a struct (value type). Check the reference inside (payload.target) instead of payload itself.
        if (payload.target == null) return;
        // 仅当 payload 指向当前正在显示的目标时才更新
        if (_targetMonster == null) return;
        if (payload.target != _targetMonster) return;
        UpdateHealth(payload.current, payload.max);
    }

    // --- Private helpers ---
    private void Refresh()
    {
        if (_targetMonster == null || _targetMonsterCombat == null)
        {
            Hide();
            return;
        }

        if (nameText != null)
            nameText.text = _targetMonster.monsterData.monsterName;

        if (hpText != null)
            hpText.text = $"HP: {_targetMonsterCombat.CurrentHealth}/{_targetMonsterCombat.MaxHealth}";

        if (hpBar != null)
            hpBar.fillAmount = (_targetMonsterCombat.MaxHealth > 0) ? (float)_targetMonsterCombat.CurrentHealth / _targetMonsterCombat.MaxHealth : 0f;

        if (icon != null)
            icon.sprite = _targetMonster.monsterData != null ? _targetMonster.monsterData.monsterSprite : null;
    }

    private void Hide()
    {
        if (_distanceCts != null)
        {
            _distanceCts.Cancel();
            _distanceCts.Dispose();
            _distanceCts = null;
        }

        _targetMonster = null;
        _targetMonsterCombat = null;
        gameObject.SetActive(false);
    }

    private async UniTaskVoid DistanceUpdateLoopAsync(CancellationToken token)
    {
        try
        {
            while (_targetMonster != null && _targetMonsterCombat != null && !_targetMonsterCombat.IsDead && gameObject.activeInHierarchy)
            {
                UpdateDistanceText();
                await UniTask.Delay(TimeSpan.FromSeconds(_distanceUpdateInterval), cancellationToken: token);
            }

            // 由于目标失效/死亡或面板不再活动，确保隐藏
            Hide();
        }
        catch (OperationCanceledException)
        {
            // 取消操作时无需处理
        }
    }

    private void UpdateDistanceText()
    {
        if (distanceText == null) return;
        if (_targetMonster == null || _playerTransform == null)
        {
            distanceText.text = string.Empty;
            return;
        }

        float dist = Vector3.Distance(_playerTransform.position, _targetMonster.transform.position);

        if (dist < 1f)
        {
            distanceText.text = "<1m";
        }
        else
        {
            int d = Mathf.RoundToInt(dist);
            distanceText.text = d + "m";
        }
    }
}