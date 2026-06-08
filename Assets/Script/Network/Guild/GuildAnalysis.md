# MMO 公会系统改造分析

## 现状

### 当前架构 (全本地化)
- GuildManager 直接操作 MongoDB (CRUD 直连)
- 无服务端中间层，无跨客户端同步
- GuildPanel UI 刷新依赖本地事件
- 成员在线状态无法跨客户端感知

### MMO 改造目标
1. 服务端权威公会数据（WorldServer 持有 GuildEntity）
2. 客户端操作 → TCP 消息 → WorldServer → MongoDB + 快照广播
3. 成员在线状态实时同步（心跳 + 快照）
4. 公会聊天频道
5. 成员操作通知（加入/离开/晋升/踢出）

## 数据流

客户端 GuildManager       Gateway          WorldServer          MongoDB
     │                     │                   │                  │
     ├─ guild_create ──→   ├─ Redis ──→        ├─ InsertOne ──→  │
     │                     │                   │                  │
     ├─ guild_join ────→   ├─ Redis ──→        ├─ UpdateOne ──→ │
     │                     │                   │                  │
     │                     │   快照广播 ←──────   快照 (含 guild 信息)
     │                     │                   │
公司信息通过 PlayerEntity 的 guildId 字段在快照中同步
