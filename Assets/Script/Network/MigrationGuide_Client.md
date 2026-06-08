# 客户端 Protobuf 迁移指南

## 改动清单

### 1. 安装依赖
Unity Package Manager → 安装 `com.cysharp.messagepack` (MessagePack)
或将 `Google.Protobuf.dll` 放入 `Assets/Plugins/`

### 2. Assets/Script/Network/TcpChannel.cs — PumpMessages
```csharp
// 旧：UTF8.GetString → JSON
var str = Encoding.UTF8.GetString(msg);

// 新：首字节区分格式
if (msg[0] == 0x01) {
    // ProtoBuf 格式
    var snap = Proto.PlayerSnapshot.Parser.ParseFrom(msg);
    OnSnapshotReceived?.InvokeProto(snap);  // 新事件
} else {
    // JSON 兼容（旧客户端）
    var str = Encoding.UTF8.GetString(msg);
    OnSnapshotReceived?.InvokeJson(str);
}
```

### 3. Assets/Script/Network/EntitySyncManager.cs
```csharp
// 新方法：接收 proto binary 快照
public void ApplySnapshotProto(Proto.PlayerSnapshot snap) {
    foreach (var e in snap.Entities) {
        var localPos = new Vector3(e.Position.X, e.Position.Y, e.Position.Z);
        // ... 逻辑同 ApplyEntity
    }
}
```

### 4. Assets/Script/Network/UdpChannel.cs — 位置消息
```csharp
// 旧：UTF8 JSON
var json = $"{{\"type\":\"position\",...}}";
var bytes = Encoding.UTF8.GetBytes(json);

// 新：ProtoBuf binary
var pos = new Proto.PositionUpdate { Uid = uid, X = x, Y = y, Z = z };
var bytes = pos.ToByteArray();
Send(bytes);
```

### 5. 流量对比 (30Hz 位置)
- JSON: ~80 bytes/msg × 30Hz = 2.4 KB/s
- ProtoBuf: ~30 bytes/msg × 30Hz = 0.9 KB/s
- 节省: 62%
