
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class MapManager : MonoBehaviour
{
    [Header("唯一传送出生点")][SerializeField] private TeleportSpawnPoint teleportSpawnPoint;
    [Header("所有的可能的出生点(备用)")][SerializeField] private List<Transform> spawnPoints; // 保留兼容
    [Header("所有的模型数据")][SerializeField] private CharacterSelectDataSO characterSelectDataSo;
    [Header("动画控制器")][SerializeField] private RuntimeAnimatorController playingController;
    [Header("Camera")][SerializeField] private CameraController cameraController;
    [Header("所有的怪物刷新点")] public List<MonsterSpawner> monsterSpawners;
    [Header("玩家成功生成事件")][SerializeField] private CharacterStateEventSO playerSpawned;
    [Header("本地图传送点")] public TeleportPoint teleportPoint;
    [Header("小地图")]
    [SerializeField] private Camera miniMapCamera;
    private void OnEnable()
    {
        CharacterRuntimeManager.Instance.SetMapManager(this);
    }

    private void Start()
    {
        SpawnCurrentPlayer();
    }


    //old

    /// <summary>
    /// 生成并初始化GameManager中当前选定的角色。
    /// 这是最常用的入口点。
    /// </summary>
    public void SpawnCurrentPlayer()
    {
        CharacterData currentCharacter = SessionManager.Instance.CurrentCharacter;
        if (currentCharacter != null)
        {
            SpawnPlayer(currentCharacter);
        }
        else
        {
            Debug.LogError("GameManager中没有当前角色数据，无法生成角色！");
        }
    }

    /// <summary>
    /// 根据指定的角色数据，在出生点生成并初始化一个可玩的角色。
    /// </summary>
    /// <param name="characterData">要生成的角色的数据。</param>
    public void SpawnPlayer(CharacterData characterData)
    {
        var characterPrefabData = characterSelectDataSo.data.FirstOrDefault(x => x.job == characterData.profession);
        if (characterPrefabData == null || characterPrefabData.model == null)
        {
            Debug.LogError($"在CharacterSelectDataSO中找不到职业为 {characterData.profession} 的角色模型信息！");
            return;
        }

        Vector3 spawnPosition = GetSpawnPosition(characterData.position);

        GameObject playerInstance = Instantiate(characterPrefabData.model, spawnPosition, Quaternion.identity);


        var playerCharacter = playerInstance.GetComponent<CharacterState>();
        if (playerCharacter != null)
        {
            playerCharacter.Init(characterData, spawnPosition);
            CharacterRuntimeManager.Instance.SetPlayerCharacter(playerCharacter);
            // --- 在玩家生成后初始化任务（延迟到此，确保角色数据已就绪） ---
            if (TaskManager.Instance != null)
            {
                TaskManager.Instance.InitializeForCurrentCharacter();
            }
            // 任务事件桥接：玩家生成后激活事件转发
            TaskEventBridge.Instance.Attach();
            // 恢复跨场景保存的临时 Buff
            CharacterRuntimeManager.Instance.RestoreTransientPlayerState(playerCharacter);
            cameraController.SetTarget(playerInstance.transform);

            // MMO 模式：挂网络同步组件并连接到 Gateway（职业在连接内同步发送）
            if (GameModeConfig.IsMmoMode)
            {
                playerInstance.AddComponent<NetworkPlayerMover>();
                AutoConnectMMO((byte)characterData.profession);
            }



            // 事件最后广播，保证监听方能立即读取到已初始化的任务数据
            playerSpawned.RaiseEvent(playerCharacter, this);
            // 生成完成后保存一次（含任务初始化结果）
            SaveCoordinator.Instance.SaveCurrentCharacterData().Forget();
            // 新增：延迟多次尝试强制初始化装备，防止极端竞态
            var equipCtrl = playerCharacter.GetComponent<EquipmentController>();
            EnsureEquipmentInitAsync(equipCtrl, this.GetCancellationTokenOnDestroy()).Forget();
        }
        else
        {
            Debug.LogError($"角色预制体 {characterPrefabData.model.name} 上缺少 CharacterState 脚本！");
        }
    }


    private async UniTaskVoid EnsureEquipmentInitAsync(EquipmentController equipCtrl, CancellationToken token)
    {
        try
        {
            if (equipCtrl == null) return;
            // 第一帧之后（等其它 OnEnable / Init 完成）
            await UniTask.Yield(token);
            equipCtrl.EnsureInitialized();
            // 再延迟 0.2 秒再试一次（背包异步稍晚完成）
            await UniTask.Delay(TimeSpan.FromSeconds(0.2f), cancellationToken: token);
            equipCtrl.EnsureInitialized();
            // 再延迟 1 秒兜底最后一次（网络慢 / Mongo 延迟场景）
            await UniTask.Delay(TimeSpan.FromSeconds(0.8f), cancellationToken: token);
            equipCtrl.EnsureInitialized();
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>角色实例化后自动连接 MMO 网络层（同步发送职业信息）</summary>
    private void AutoConnectMMO(byte profession)
    {
        if (NetworkManager.Instance == null || NetworkManager.Instance.IsConnected) return;
        var username = PlayerLogInManager.Instance.GetLoggedInUsername();
        if (string.IsNullOrEmpty(username))
        {
            Debug.LogWarning("[MapManager] 无法获取游戏用户名，跳过 MMO 自动连接");
            return;
        }
        Debug.Log($"[MapManager] 自动连接 MMO: {username} profession={(CharacterProfession)profession}({profession})");
        _ = NetworkManager.Instance.ConnectAsync(username, "123", profession);
    }

    public Vector3 GetSpawnPosition(Vector3 prevPosition)
    {
        if (teleportSpawnPoint != null)
        {
            return teleportSpawnPoint.transform.position;
        }
        // 兼容旧逻辑：找最近出生点
        return GetNearestSpawnPoint(prevPosition);
    }

    /// <summary>
    /// 对外公开：根据给定的位置返回最近出生点（备用逻辑）。
    /// </summary>
    public Vector3 GetNearestSpawnPoint(Vector3 targetPosition)
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("出生点列表为空！返回世界原点(0,0,0)。");
            return Vector3.zero;
        }
        return spawnPoints.OrderBy(t => (t.position - targetPosition).sqrMagnitude).FirstOrDefault()!.position;
    }
}
