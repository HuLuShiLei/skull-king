---
name: verify-changes
description: 骷髅王项目改完代码后的验证流程：单测、前端构建、端到端脚本，以及临时服务端的启停与收尾。当需要验证改动、跑测试、准备提交、或端到端确认 Hub 行为时使用。
---

# 验证改动

按需要的深度选，能停在哪一步就停在哪一步。

## 第 1 步：编译与单测（改了任何 C# 都要跑）

```bash
dotnet build
dotnet test
```

## 第 2 步：前端类型检查与构建（改了任何 client 下的文件都要跑）

```bash
npm --prefix client run build
```

这两步等价于 CI，过了 CI 就会过。改了前后端共用的 DTO（`src/SkullKing.Contracts`）时两步都要跑，`client/src/api/types.ts` 是手写的、不会自动同步，漏改会在这一步暴露。

## 第 3 步：端到端脚本（改了 Hub 方法、鉴权、序列化、房间流程时要跑）

单测用的是内存假货，这一步才会真连 Hub。

```bash
# 后台起服务端，日志重定向到临时文件
dotnet run --project src/SkullKing.Server --no-build --urls http://localhost:5080 *> $env:TEMP\sk-server.log

# 等几秒再跑
node client/scripts/smoke.mjs http://localhost:5080
```

打到一半断开、验证重启恢复用 `node client/scripts/smoke.mjs --half`，然后重启服务端，日志里应该出现「已恢复 N 个房间」。

## 第 4 步：收尾（必做）

`dotnet run` 会派生一个 `SkullKing.Server` 子进程，杀父进程杀不掉它，留着会锁住 `bin` 目录导致下次编译报 MSB3027：

```powershell
Get-Process -Name SkullKing.Server -ErrorAction SilentlyContinue | Stop-Process -Force
```

下次 `dotnet build` 报「文件被 SkullKing.Server 锁定」时，就是上一轮忘了这一步。

## 手工验证数据库相关改动

迁移和恢复逻辑没有单测覆盖，改了就手工过一遍：建房 → 停服务端 → 重启 → probe 还在不在。

```powershell
$auth = Invoke-RestMethod -Uri http://localhost:5080/api/auth/anonymous -Method Post `
  -ContentType 'application/json' -Body '{"nickname":"验证"}'
$h = @{ Authorization = "Bearer $($auth.token)" }
$room = Invoke-RestMethod -Uri http://localhost:5080/api/rooms -Method Post -Headers $h `
  -ContentType 'application/json' `
  -Body '{"name":"验证群","isPublic":true,"maxPlayers":6,"maxRounds":5,"turnSeconds":60}'

# 重启服务端后
Invoke-RestMethod -Uri "http://localhost:5080/api/rooms/$($room.code)/probe" -Headers $h
```

启动日志里会打出 `Applying migration '...'`，确认迁移真的执行了。
