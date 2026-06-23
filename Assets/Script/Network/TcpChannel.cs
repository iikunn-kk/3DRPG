// Unity 客户端：Assets/Script/Network/TcpChannel.cs
// 完整替换文件，在原有基础上增加 ProtoBuf 支持

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Proto = Mmo;

/// <summary>
/// TCP 通道 — 支持 JSON (旧) / ProtoBuf (新) 双格式。
/// 通过首字节区分: 0x01 = ProtoBuf, 其他 = JSON
/// </summary>
public class TcpChannel
{
    public event Action<string> OnSnapshotReceived;      // JSON 快照 (兼容旧)
    public event Action<string> OnChatReceived;          // 聊天消息 (JSON)
    public event Action<byte[]> OnSnapshotProto;         // ProtoBuf 快照 (新)

    public bool IsConnected { get; private set; }
    public string AuthenticatedUid { get; private set; }
    public string SessionId { get; private set; }

    private NetworkStream _stream;
    private TcpClient _client;
    private readonly Queue<byte[]> _sendQueue = new();

    // ================ 连接 / 认证 ================

    public async UniTask<bool> ConnectAsync(string host, int port, string token)
    {
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();

            // 发送 AuthRequest (保持 JSON)
            var authJson = $"{{\"token\":\"{token}\"}}";
            var authBytes = Encoding.UTF8.GetBytes(authJson);
            var authPacket = BuildPacket(authBytes);
            _stream.Write(authPacket, 0, authPacket.Length);

            // 读 AuthResponse (保持 JSON)
            var lenBuf = new byte[4];
            await ReadExactlyAsync(lenBuf, 0, 4);
            int len = BitConverter.ToInt32(lenBuf, 0);
            var buf = new byte[len];
            await ReadExactlyAsync(buf, 0, len);
            var json = Encoding.UTF8.GetString(buf);

            // 解析
            var successIdx = json.IndexOf("\"success\"");
            if (successIdx < 0 || json.IndexOf("true", successIdx) < 0)
            {
                Debug.LogError($"[TCP] 认证失败: {json}");
                return false;
            }

            var uidIdx = json.IndexOf("\"playerUid\"");
            var uidStart = json.IndexOf("\"", uidIdx + 12) + 1;
            var uidEnd = json.IndexOf("\"", uidStart);
            AuthenticatedUid = json[uidStart..uidEnd];

            SessionId = "";

            IsConnected = true;
            Debug.Log($"[TCP] 连接成功, Uid={AuthenticatedUid}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TCP] 连接异常: {ex.Message}");
            return false;
        }
    }

    // ================ 发送 (全部进队列，统一在 Update 中同步写入) ================

    public void Send(string json)
    {
        if (!IsConnected || _stream == null) return;
        var data = Encoding.UTF8.GetBytes(json);
        var packet = BuildPacket(data);
        lock (_sendQueue) _sendQueue.Enqueue(packet);
    }

    /// <summary>发送 ProtoBuf 二进制消息（进队列，避免与 Update 并发写 stream）</summary>
    public void SendProto(byte[] bytes)
    {
        if (!IsConnected || _stream == null) return;
        var packet = BuildPacket(bytes);
        lock (_sendQueue) _sendQueue.Enqueue(packet);
    }

    // ================ 帧更新：统一发送队列 + 接收 ================

    public void Update()
    {
        // 统一发送（同步 Write，避免 WriteAsync fire-and-forget）
        lock (_sendQueue)
        {
            while (_sendQueue.TryDequeue(out var packet))
            {
                try { _stream?.Write(packet, 0, packet.Length); }
                catch { IsConnected = false; return; }
            }
        }

        // 接收
        if (!IsConnected || _stream == null || !_stream.DataAvailable) return;
        PumpMessages();
    }

    // ================ 核心：消息泵 (JSON + ProtoBuf 双格式) ================

    private void PumpMessages()
    {
        var buf = new byte[4];
        byte[]? latestProto = null;
        try
        {
            while (_stream.DataAvailable)
            {
                int r = _stream.Read(buf, 0, 4);
                if (r < 4) break;
                int len = BitConverter.ToInt32(buf, 0);
                if (len <= 0 || len > 65536)
                {
                    Debug.LogWarning($"[TCP] 非法包长度={len}，断开连接");
                    IsConnected = false;
                    return;
                }
                var msg = new byte[len];
                int total = 0;
                while (total < len)
                {
                    int n = _stream.Read(msg, total, len - total);
                    if (n == 0) { IsConnected = false; return; }
                    total += n;
                }

                // === 格式识别 ===
                if (msg.Length > 0 && msg[0] == 0x01)
                {
                    // ProtoBuf 快照：只保留最新一条，攒到批处理结束再应用（防跳帧）
                    latestProto = msg.AsSpan(1).ToArray();
                }
                else
                {
                    var str = Encoding.UTF8.GetString(msg);
                    if (str.Contains("\"chat\""))
                        OnChatReceived?.Invoke(str);
                    else
                        OnSnapshotReceived?.Invoke(str);
                }
            }
            // 批处理结束：只应用最新一条 ProtoBuf 快照
            if (latestProto != null)
                OnSnapshotProto?.Invoke(latestProto);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TCP] 接收异常: {ex.Message}");
            IsConnected = false;
        }
    }

    // ================ 工具 ================

    private byte[] BuildPacket(byte[] payload)
    {
        var pkt = new byte[4 + payload.Length];
        BitConverter.GetBytes(payload.Length).CopyTo(pkt, 0);
        payload.CopyTo(pkt, 4);
        return pkt;
    }

    private async UniTask ReadExactlyAsync(byte[] buf, int off, int len)
    {
        int total = 0;
        while (total < len)
        {
            int n = await _stream.ReadAsync(buf, off + total, len - total, CancellationToken.None);
            if (n == 0) throw new Exception("连接断开");
            total += n;
        }
    }

    public void Disconnect()
    {
        IsConnected = false;
        lock (_sendQueue) _sendQueue.Clear();
        _stream?.Dispose();
        _client?.Dispose();
    }
}
