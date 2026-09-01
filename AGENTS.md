# 骷髅王在线版 · 开发须知

产品是什么、怎么玩、规则实现到哪一步，都在 [README.md](README.md)。这里只写动代码时必须知道的事。

## 沟通与写法

- 一律用中文回答。
- 注释只解释**为什么**，尤其是踩过的坑和取舍；不解释代码在做什么。仓库里现有注释就是标准，照着写。
- 提交信息用一句中文陈述句说清改了什么，不带 `feat:` / `fix:` 之类前缀。例：`修掉老板键退房等几个线上问题，限时支持中途关掉`。
- 界面文案一律用伪装黑话（出牌＝发消息，叫牌＝群接龙，房间＝群聊，玩家＝群成员）。词表在 `client/src/skins/im/vocabulary.ts`。
- 改完并通过验证后自动提交、推送到当前分支，不必再问。提交信息仍用一句中文陈述句。

## 命令

| 用途 | 命令 |
| --- | --- |
| 后端编译 | `dotnet build` |
| 全部单测（132 个） | `dotnet test` |
| 前端类型检查 + 构建 | `npm --prefix client run build` |
| 端到端脚本（服务端要先跑着） | `node client/scripts/smoke.mjs` |

CI 跑的就是 `dotnet test` 加前端 `npm run build`（`.github/workflows/ci.yml`），本地这两条过了 CI 就会过。

前端 `npm run build` 的产物会直接写进 `src/SkullKing.Server/wwwroot`，那个目录不进版本库。

## 目录地图

```
src/SkullKing.Domain/         规则引擎。纯函数、零外部依赖，Apply(State, Command) -> (State, Events)
src/SkullKing.Contracts/      DTO 与 Hub 消息契约，前后端共用的形状都在这
src/SkullKing.Application/    房间、大厅、对局编排、超时托管、重启恢复、回放
src/SkullKing.Infrastructure/ EF Core + SQLite，归档与迁移
src/SkullKing.Server/         Minimal API + SignalR Hub + 托管前端产物
client/src/stores/            Pinia：session 身份、connection 连接、room 房间、lobby 大厅、stealth 伪装
client/src/skins/im/          IM 伪装皮肤的全部组件与词表
tests/                        xUnit，方法名用中文描述行为
```

## 五条铁律

1. **广播的内容必须脱敏。** 手牌只单播给本人，叫牌数字在全员叫完前也只发本人。往 `GameProjector.ToBroadcastDto` 里加字段前先问一句：这条会不会让人看到别人的牌。
2. **改房间状态一律在 `room.Gate` 锁内**，顺序是「校验 → 改内存 → 落库 → 广播事件 → 单播快照」。绝不同时持有两把房间锁。
3. **时间一律走注入的 `TimeProvider`**（`RoomService.Now`），不要写 `DateTimeOffset.UtcNow`，否则测试没法用假时钟推进。
4. **伪装期间不能卸载 `RoomView`。** `App.vue` 用 `v-show` 藏真实界面，改成 `v-if` 会触发 `onBeforeUnmount` 里的退房，按一下老板键就等于退出对局。
5. **动了托管或超时逻辑，必须确认 `room.TurnDeadline` 在每个入口都会被重新计算**（出牌后、改限时后、重启恢复后）。它是 null 时设了限时的房间永远不会触发托管，整局会卡死。

## 更细的约定

按文件类型自动生效的规则在 `.cursor/rules/`。三个高频工作流做成了 skill，需要时读 `.cursor/skills/` 下对应的 `SKILL.md`：

- `verify-changes` —— 改完怎么验，含端到端脚本和临时服务端的收尾
- `db-migration` —— 给数据库加字段的完整步骤，漏一处就会静默丢数据
- `add-room-setting` —— 新增一个房间设置项要改的所有地方
