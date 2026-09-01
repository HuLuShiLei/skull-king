---
name: db-migration
description: 骷髅王项目给 SQLite 加字段或改 schema 的完整步骤，覆盖 EF 迁移生成、归档读写两端、恢复路径。当需要新增数据库列、修改实体、生成 EF 迁移，或数据存了却读不回来时使用。
---

# 加数据库字段

数据要真的存下来并读回来，得同时改五处。少改一处不会报错，只会静默丢数据——重启后字段变回默认值。

## 步骤

按顺序改，每处都别跳：

1. **实体** `src/SkullKing.Infrastructure/Entities.cs` —— 加属性，可空。
2. **归档契约** `src/SkullKing.Application/Abstractions/IGameArchive.cs` —— `PersistedRoom` 等是位置记录（positional record），加参数会连带所有构造点，编译器会帮你找出来。
3. **归档实现** `src/SkullKing.Infrastructure/EfGameArchive.cs` —— **写和读都要改**：`UpsertRoomAsync` 里 `row.X = room.X`，`LoadResumableRoomsAsync` 里构造 `PersistedRoom` 时带上 `r.X`。这是最容易漏读的一处。
4. **恢复路径** `src/SkullKing.Application/Rooms/RoomService.Restore.cs` 的 `Rebuild` —— 把 `snapshot.X` 填回内存对象。漏了就是「存进去了但重启后没了」。
5. **落库入口** `RoomService.ToPersisted` —— 把内存值带进快照。

## 生成迁移

```bash
dotnet ef migrations add 迁移名 --project src/SkullKing.Infrastructure --startup-project src/SkullKing.Server
```

报 `Build failed` 时先单独跑 `dotnet build` 看真实错误。最常见的原因是上一轮的 `SkullKing.Server` 进程还在跑、锁住了 bin 目录，见 skill `verify-changes` 的收尾一节。

迁移文件生成后打开看一眼，应该只有预期的 `AddColumn` / `DropColumn`，多出来的表示模型快照和数据库不一致。

## 应用迁移

不需要手工执行。`Program.cs` 启动时调 `MigrateAsync()`，服务端一起来就会应用，日志里会打 `Applying migration '...'`。

## 验证

单测用的是内存假货（`FakeArchive`），**碰不到 EF 这一层**，所以必须手工过一遍「建房 → 停服务端 → 重启 → 数据还在」。具体命令在 skill `verify-changes` 的最后一节。
