using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// TCP 通道：用 Unity 内置 JsonUtility 序列化（轻量，不需额外 DLL）。
/// </summary>
public class TcpChannel
{
    /// <summary>收到服务端世界快照时触发（JSON）</summary>
    public event Action<string> OnSnapshotReceived;

    public bool IsConnected { get; private set; }
    public string AuthenticatedUid { get; private set; }
    public string SessionId { get; private set; }

    private TcpClient _client;
    private NetworkStream _stream;
    private CancellationTokenSource _cts;
    private readonly Queue<byte[]> _sendQueue = new();

    public async Cysharp.Threading.Tasks.UniTask<bool> ConnectAsync(string host, int port, string token)
    {
        try
        {
            _cts = new CancellationTokenSource();
            _client = new TcpClient { NoDelay = true };
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();

            // 发送 JSON AuthRequest
            var json = $"{{\"token\":\"{token}\"}}";
            var payload = Encoding.UTF8.GetBytes(json);
            var packet = BuildPacket(payload);
            await _stream.WriteAsync(packet, 0, packet.Length);

            // 读取 4 字节长度头 + payload
            var lenBuf = new byte[4];
            int read = await ReadExactlyAsync(_stream, lenBuf, 0, 4, _cts.Token);
            if (read < 4) { Debug.LogError("[TCP] Auth response too short"); return false; }
            int respLen = BitConverter.ToInt32(lenBuf, 0);
            var respBuf = new byte[respLen];
            await ReadExactlyAsync(_stream, respBuf, 0, respLen, _cts.Token);
            var respJson = Encoding.UTF8.GetString(respBuf);
            var resp = JsonUtility.FromJson<AuthResponse>(respJson);

            if (!resp.success)
            {
                Debug.LogError($"[TCP] 认证失败: {resp.errorMsg}");
                return false;
            }

            AuthenticatedUid = resp.playerUid;
            SessionId = Guid.NewGuid().ToString("N");
            IsConnected = true;
            Debug.Log($"[TCP] 连接成功, Uid={AuthenticatedUid}");

            // 后台持续接收服务端推送的快照
            _ = ReceiveLoopAsync(_cts.Token);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TCP] 连接异常: {ex.Message}");
            return false;
        }
    }

    public void Send(string jsonMessage)
    {
        if (!IsConnected) { Debug.LogWarning("[TcpChannel] Send 跳过: IsConnected=false"); return; }
        var payload = Encoding.UTF8.GetBytes(jsonMessage);
        lock (_sendQueue) { _sendQueue.Enqueue(BuildPacket(payload)); }
        Debug.Log($"[TcpChannel] Send 入队, queueCount={_sendQueue.Count}, len={payload.Length}");
    }

    public void Update()
    {
        lock (_sendQueue)
        {
            while (_sendQueue.Count > 0)
            {
                var pkt = _sendQueue.Dequeue();
                try
                {
                    _stream?.Write(pkt, 0, pkt.Length);
                    Debug.Log("[TcpChannel] Write 成功");
                }
                catch (Exception ex) { Debug.LogError($"[TcpChannel] Write 失败! {ex.GetType().Name}: {ex.Message}"); IsConnected = false; break; }
            }
        }
    }

    public void Disconnect()
    {
        IsConnected = false;
        _cts?.Cancel();
        _stream?.Dispose();
        _client?.Dispose();
    }

    private static async System.Threading.Tasks.Task<int> ReadExactlyAsync(
        NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken ct)
    {
        int total = 0;
        while (total < count) { int r = await stream.ReadAsync(buffer, offset + total, count - total, ct); if (r == 0) break; total += r; }
        return total;
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid ReceiveLoopAsync(CancellationToken ct)
    {
        var buf = new byte[4];
        while (!ct.IsCancellationRequested && IsConnected)
        {
            try
            {
                int r = await ReadExactlyAsync(_stream, buf, 0, 4, ct);
                if (r < 4) break;
                int len = BitConverter.ToInt32(buf, 0);
                if (len <= 0 || len > 65536) break;
                var msg = new byte[len];
                await ReadExactlyAsync(_stream, msg, 0, len, ct);
                var str = Encoding.UTF8.GetString(msg);
                Debug.Log($"[TCP] 收到服务端消息, 长度={len}");
                OnSnapshotReceived?.Invoke(str);
            }
            catch { break; }
        }
    }

    private byte[] BuildPacket(byte[] payload)
    {
        var pkt = new byte[4 + payload.Length];
        Buffer.BlockCopy(BitConverter.GetBytes(payload.Length), 0, pkt, 0, 4);
        Buffer.BlockCopy(payload, 0, pkt, 4, payload.Length);
        return pkt;
    }

    [Serializable]
    private class AuthResponse { public bool success; public string playerUid; public string errorMsg; }
}
