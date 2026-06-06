using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 统一网络入口 (Singleton)。管理 TCP/UDP 双通道 + HTTP 登录。
/// </summary>
public class NetworkManager : Singleton<NetworkManager>
{
    [Header("连接配置")]
    [SerializeField] private string _serverHost = "localhost";
    [SerializeField] private int _tcpPort = 17777;
    [SerializeField] private int _udpPort = 17778;
    [SerializeField] private int _httpPort = 15000;

    public string ServerHost => _serverHost;
    public string HttpBaseUrl => $"http://{_serverHost}:{_httpPort}";

    public TcpChannel Tcp { get; private set; }
    public UdpChannel Udp { get; private set; }
    public bool IsConnected => Tcp?.IsConnected == true;

    public string PlayerUid { get; private set; }
    public string SessionId { get; private set; }
    public string BearerToken { get; private set; }

    protected override void Awake()

    {
        base.Awake();
        Tcp = new TcpChannel();
        Udp = new UdpChannel();
        DontDestroyOnLoad(gameObject);

        // 绑定快照回调到 EntitySyncManager
        Tcp.OnSnapshotReceived += json =>
        {
            var sync = FindObjectOfType<EntitySyncManager>();
            if (sync) sync.ApplySnapshot(json);
        };
    }

    /// <summary>Inspector 右键 → Test Gateway Connection，验证 TCP/UDP 链路</summary>
    [ContextMenu("🔍 Test Gateway Connection (Player1)")]
    public async void TestConnectionPlayer1() => await ConnectAsync("player1", "123");

    [ContextMenu("🔍 Test Gateway Connection (Player2)")]
    public async void TestConnectionPlayer2() => await ConnectAsync("player2", "123");

    [ContextMenu("⚔️ Test Attack (1→2)")]
    public void TestAttack()
    {
        if (!Tcp.IsConnected) { Debug.LogError("[NetworkManager] TCP 未连接!"); return; }
        var msg = "{\"type\":\"attack\",\"attackerId\":1,\"targetId\":2,\"skillId\":1,\"attackerAtk\":20,\"attackerCritRate\":10,\"attackerCritDmg\":1.5,\"targetDef\":5,\"targetHp\":100,\"skillMultiplier\":2.0,\"distance\":2.0,\"maxRange\":3.0,\"attackerMp\":100,\"skillMpCost\":10,\"cooldownTicks\":20}";
        Tcp.Send(msg);
        Debug.Log("[NetworkManager] ⚔️ 发送攻击请求: player1 → player2");
    }

    [System.Obsolete]
    public async void TestConnection()
    {
        Debug.Log("<color=cyan>══════════ [NetworkManager] 开始连通性测试 ══════════</color>");
        Debug.Log($"[Test] HTTP Base: {HttpBaseUrl}");
        Debug.Log($"[Test] TCP: {_serverHost}:{_tcpPort}");
        Debug.Log($"[Test] UDP: {_serverHost}:{_udpPort}");

        var ok = await ConnectAsync("testuser", "123456");
        Debug.Log(ok
            ? "<color=green>✅ 全部链路连通！TCP/UDP/HTTP 均正常。</color>"
            : "<color=red>❌ 连接失败，检查 Gateway 容器的 docker logs。</color>");
    }

    private bool _connecting;

    /// <summary>完整连接流程：HTTP 登录 → TCP 连接 → UDP 绑定</summary>
    public async UniTask<bool> ConnectAsync(string username, string password)
    {
        // 已连接或正在连接中，跳过重复调用
        if (Tcp is { IsConnected: true })
        {
            Debug.Log($"[NetworkManager] 已连接 {PlayerUid}，跳过重复连接");
            return true;
        }
        if (_connecting)
        {
            Debug.LogWarning("[NetworkManager] 正在连接中，跳过重复调用");
            return true;
        }
        _connecting = true;
        try
        {
            // 确保 Tcp/Udp 已初始化（Singleton AddComponent 时序保护）
            Tcp ??= new TcpChannel();
            Udp ??= new UdpChannel();

            // 1. HTTP 登录获取 JWT
            var loginSuccess = await HttpLoginAsync(username, password);
            if (!loginSuccess) return false;

            // 2. TCP 连接 + 首包鉴权
            var tcpOk = await Tcp.ConnectAsync(_serverHost, _tcpPort, BearerToken);
            if (!tcpOk) return false;
            PlayerUid = Tcp.AuthenticatedUid;
            SessionId = Tcp.SessionId;

            // 3. UDP 绑定
            await Udp.ConnectAsync(_serverHost, _udpPort, SessionId);
            Debug.Log($"[NetworkManager] 连接成功! Uid={PlayerUid}, Session={SessionId}");
            return true;
        }
        finally
        {
            _connecting = false;
        }
    }

    private async UniTask<bool> HttpLoginAsync(string username, string password)
    {
        try
        {
            var json = $"{{\"username\":\"{username}\",\"password\":\"{password}\"}}";
            using var www = new UnityWebRequest($"{HttpBaseUrl}/api/auth/login", "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            await www.SendWebRequest().ToUniTask();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[NetworkManager] 登录失败: {www.error}");
                return false;
            }

            var responseJson = www.downloadHandler.text;
            var result = JsonUtility.FromJson<LoginResponse>(responseJson);
            if (result == null || string.IsNullOrEmpty(result.token))
            {
                Debug.LogError("[NetworkManager] 登录响应无效");
                return false;
            }

            BearerToken = result.token;
            Debug.Log($"[NetworkManager] HTTP 登录成功, Token={BearerToken[..Math.Min(20, BearerToken.Length)]}...");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] HTTP 登录异常: {ex.Message}");
            // Debug.LogError($"[NetworkManager] HTTP 登录异常: {ex.Message}");
            return false;
        }
    }

    public void SendPosition(Vector3 pos, Quaternion rot)
    {
        if (!Tcp.IsConnected) return;
        var payload = new PositionPayload { type = "position", uid = PlayerUid, x = pos.x, y = pos.y, z = pos.z };
        Tcp.Send(JsonUtility.ToJson(payload));
    }

    public void SendMonsterSpawn(uint instId, int maxHp, Vector3 pos)
    {
        if (!Tcp.IsConnected) return;
        var payload = new MonsterSpawnPayload { type = "monster_spawn", instId = instId, maxHp = maxHp, x = pos.x, y = pos.y, z = pos.z };
        Tcp.Send(JsonUtility.ToJson(payload));
        Debug.Log($"[NetworkManager] 注册怪物 instId={instId} hp={maxHp}");
    }

    public void SendAttack(uint attackerId, uint targetId, int attackerAtk, int targetHp, int targetDef, float critRate, float skillMultiplier = 1f, float distance = 2f)
    {
        if (!Tcp.IsConnected) return;
        var json = $"{{\"type\":\"attack\",\"attackerId\":{attackerId},\"targetId\":{targetId},\"skillId\":1,\"attackerAtk\":{attackerAtk},\"attackerCritRate\":{Mathf.RoundToInt(critRate)},\"attackerCritDmg\":2,\"targetDef\":{targetDef},\"targetHp\":{targetHp},\"skillMultiplier\":{skillMultiplier:F2},\"distance\":{distance:F2},\"maxRange\":5,\"attackerMp\":100,\"skillMpCost\":0,\"cooldownTicks\":0}}";
        Tcp.Send(json);
        Debug.Log($"[NetworkManager] 发送攻击: {attackerId}→{targetId} atk={attackerAtk} def={targetDef}");
    }

    private void Update()
    {
        Tcp?.Update();
        Udp?.Update();
    }

    private void OnDestroy()
    {
        Tcp?.Disconnect();
        Udp?.Disconnect();
    }

    [System.Serializable]
    private struct PositionPayload { public string type; public string uid; public float x; public float y; public float z; }

    [System.Serializable]
    private struct MonsterSpawnPayload { public string type; public uint instId; public int maxHp; public float x; public float y; public float z; }

    [System.Serializable]
    private class LoginResponse
    {
        public string token;
        public string playerUid;
    }
}
