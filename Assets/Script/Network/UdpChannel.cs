using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// UDP 通道：上行发送位置（30Hz）+ 下行接收快照。
/// Update() 中同步批量读取 UDP 缓冲区，消除 async await 的帧延迟累积（参考 MMO_DEBUG_LOG 问题6）。
/// </summary>
public class UdpChannel
{
    public bool IsConnected { get; private set; }

    /// <summary>收到 UDP 下行数据（ProtoBuf 快照，首字节 0x01）。在主线程 Update 中触发。</summary>
    public event Action<byte[]> OnDataReceived;

    private UdpClient _client;
    private CancellationTokenSource _cts;

    public async Cysharp.Threading.Tasks.UniTask ConnectAsync(string host, int port, string sessionId)
    {
        try
        {
            _cts = new CancellationTokenSource();
            _client = new UdpClient();
            _client.Connect(host, port);
            IsConnected = true;

            var payload = System.Text.Encoding.UTF8.GetBytes($"BIND:{sessionId}");
            await _client.SendAsync(payload, payload.Length);
            Debug.Log($"[UDP] 绑定成功, SessionId={sessionId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UDP] 连接异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 主线程每帧调用：同步批量读取所有可用的 UDP 数据报，立即触发回调。
    /// 关键：不使用 async await，避免每次接收 1 帧的 PlayerLoop 调度延迟累积。
    /// </summary>
    public void Update()
    {
        if (_client == null || !IsConnected) return;

        try
        {
            // 同步批量读取：Available > 0 表示有数据报在 OS 缓冲区
            while (_client.Available > 0)
            {
                IPEndPoint remoteEp = null;
                byte[] data = _client.Receive(ref remoteEp);
                if (data.Length > 0)
                {
                    OnDataReceived?.Invoke(data);
                }
            }
        }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UDP] 接收异常: {ex.Message}");
        }
    }

    public void Send(byte[] payload)
    {
        if (!IsConnected || _client == null) return;
        try { _client.SendAsync(payload, payload.Length); }
        catch { IsConnected = false; }
    }

    /// <summary>发送文本（JSON），UTF8 编码后走 UDP</summary>
    public void SendString(string text)
    {
        if (!IsConnected || _client == null) return;
        try
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            _client.SendAsync(bytes, bytes.Length);
        }
        catch { IsConnected = false; }
    }

    public void Disconnect()
    {
        IsConnected = false;
        _cts?.Cancel();
        _client?.Dispose();
    }
}
