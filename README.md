# 骷髅王在线版

服务端权威的 Skull King（骷髅王）在线对战。后端 ASP.NET Core + SignalR + SQLite，前端 Vue 3，界面伪装成一个企业 IM 聊天窗口——出牌是发消息，叫牌是群接龙，记分板是群成员列表的未读数。

## 跑起来

需要 .NET 10 SDK 和 Node 22。

```bash
# 后端（http://localhost:5080）
dotnet run --project src/SkullKing.Server

# 前端（另开一个终端，http://localhost:5173，已代理 /api 和 /hub 到 5080）
cd client && npm install && npm run dev
```

前端 `npm run build` 的产物默认落在 `src/SkullKing.Server/wwwroot`，所以本机跑一个进程就能通全链路：

```bash
cd client && npm run build
dotnet run --project src/SkullKing.Server -c Release
```

数据库首次启动自动迁移，不需要手工建表。默认落在工作目录的 `skullking.db`，改 `ConnectionStrings__Default` 可以换位置。

## 部署

前后端各一个容器：后端只提供 API 和 Hub，前端是 nginx 托管的静态站。后端地址不写死在前端构建里，而是容器启动时注入，所以同一个前端镜像可以连不同环境的后端。

```bash
docker compose up -d --build   # 本地自测，打开 http://localhost:8081
```

Portainer + Traefik 的完整步骤、环境变量清单、以及「没有 CI/CD 也能持续部署」的两条路线，都在 [deploy/README.md](deploy/README.md)。仓库里已经带了 GitHub Actions 工作流，推代码就会自动构建镜像推到 GHCR。

## 怎么玩

1. 打开首页，随便填个昵称就进去了，不用注册。
2. 左栏「新建群聊」开房，会拿到一个 6 位群号。
3. 把群号或邀请链接 `http://<host>/j/<群号>` 发给同事，对方打开直接入座。
4. 非房主点「确认参加」，房主点「发起议程」开局。2 人起，最多 6 人，多出来的自动进旁观席。
5. 每轮先在接龙条上选承接量（叫牌），全员选完一次性揭示；然后点底部的快捷回复条出牌，灰掉的是不能出的。

一局打完可以原地再开一轮，重新确认一次即可。历史记录在大厅点开能翻回当时的完整消息流。

## 摸鱼相关

- **老板键**：默认 `Esc`，一键切到一屏预置的工作对话，再按回来。设置里能改键。
- **失焦自动伪装**：窗口失去焦点几秒后自动切伪装态，可关。
- **标题伪装**：`document.title` 和 favicon 全程是办公工具的样子，可关。
- **不弹窗、不响铃**：轮到你行动只用一个很淡的红点和底部文案提示，全程无音效。
- **换皮**：皮肤收在 `client/src/skins/`，实现 `SkinDefinition` 里那几个组件就能整套替换，游戏逻辑一行都不用动。

## 项目结构

```
src/
  SkullKing.Domain/          规则引擎。纯函数，零外部依赖，Apply(Command) -> (State, Events)
  SkullKing.Contracts/       DTO 与 Hub 消息契约
  SkullKing.Application/     大厅、房间、对局编排、超时托管、回放
  SkullKing.Infrastructure/  EF Core + SQLite
  SkullKing.Server/          Minimal API + SignalR Hub + 托管前端产物
client/                      Vue 3 + TS + Vite（IM 皮肤）
deploy/                      两个 Dockerfile、nginx 配置、Portainer stack、部署说明
tests/                       xUnit，126 个用例
```

### 几个设计上的取舍

**服务端权威**。客户端只发意图，连「我手上哪几张能出」都以服务端下发的 `playableCardIds` 为准。手牌只单播给本人，叫牌在本轮全员叫完之前也只发给本人，所以改前端代码看不到别人的牌。

**存命令不存状态**。规则引擎是确定性的，同一个随机种子加同一串命令必然重现同一局。所以库里只存种子和命令日志，进程重启时重放恢复，历史回放也是同一套重放逻辑跑出来的——回放看到的和当时看到的必然一致。

**事件驱动表现，快照驱动状态**。Hub 广播增量事件让界面能一张张地演出牌动画，随后单播的房间快照才是权威状态。两者走同一条队列，否则快照会在动画播完前抢先落地，桌上的牌会瞬间清空。

**掉线不塌陷座位**。对局中掉线只标记离线并保留座位，超过宽限期由系统代打（叫 0 / 出第一张合法牌），重连后凭 token 认回原座位并补发快照。服务重启后额外留 90 秒重连窗口，期间不启动托管。

## 规则实现范围

核心 70 张牌：4 个花色各 1-14（黑色 JollyRoger 是王牌）、5 张逃跑、5 张海盗、1 张 Tigress（出牌时选当海盗还是逃跑）、2 张美人鱼、1 张骷髅王。

第 N 轮发 N 张，最大轮数 `min(设置上限, 70 / 人数)`。

吃墩优先级：美人鱼吃骷髅王 > 骷髅王吃海盗 > 海盗吃美人鱼和数字牌 > 黑色王牌 > 跟牌花色最大 > 全逃跑时最先出的收墩。

计分：叫 0 成功得 `轮次 × 10`，失败扣同样多；叫 N 成功得 `N × 20` 加奖励分，失败按差额每墩扣 10 且没有奖励分。奖励分只在叫牌命中时计入——用 14 吃墩 +10（黑 14 是 +20），骷髅王吃掉每个海盗 +30，海盗吃掉骷髅王 +30，美人鱼吃掉骷髅王 +50。

`Loot`、`Kraken`、`WhiteWhale` 这几张扩展牌留了定义和房间开关，但没发到牌组里。

## 测试

```bash
dotnet test
```

规则引擎的测试覆盖三角克制、Tigress 双形态、全逃跑墩、首家出特殊牌后由谁定花色、叫 0 成败、各类奖励分叠加；编排层的测试覆盖手牌可见性、叫牌保密、超时托管、掉线重连、重启重放、回放一致性。

单测用的是内存假货，所以还有一个端到端脚本，让三个匿名玩家真的连上 Hub 打完一局再把回放拉回来对一遍，覆盖鉴权、Hub 签名、序列化这些只有真跑起来才会暴露的问题：

```bash
# 服务端先跑着
cd client && node scripts/smoke.mjs

# 打到一半就断开，用来验证停服重启后能不能恢复这局
node scripts/smoke.mjs --half
```
