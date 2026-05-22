using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// UDP 高频通道：Phase 1 简化为纯连通性验证，Phase 2 升级二进制协议。
/// </summary>
public class UdpChannel
{
    public bool IsConnected { get; private set; }
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

    public void Send(byte[] payload)
    {
        if (!IsConnected || _client == null) return;
        try { _client.SendAsync(payload, payload.Length); }
        catch { IsConnected = false; }
    }

    public void Update() { }

    public void Disconnect()
    {
        IsConnected = false;
        _cts?.Cancel();
        _client?.Dispose();
    }
}
