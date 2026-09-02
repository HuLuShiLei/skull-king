# 骷髅王在线版

服务端权威的 [Skull King](https://www.grandpabeck.com/skull-king) 在线对战。后端为 ASP.NET Core + SignalR + SQLite，前端为 Vue 3。界面伪装成企业即时通讯客户端：出牌对应发消息，叫牌对应填写承接量，记分显示在群成员列表中。

## 功能

- 大厅与房间：公开列表、群号、带口令的邀请链接、旁观
- 对局中途改限时、掉线宽限、超时托管、进程重启后重放恢复
- 完整消息流与历史回放
- 老板键与失焦自动伪装；浅色 / 深色 / 跟随系统主题

## 本地开发

需要 [.NET 10 SDK](https://dotnet.microsoft.com/download) 与 Node 22。

```bash
# 后端 http://localhost:5080
dotnet run --project src/SkullKing.Server

# 前端 http://localhost:5173（/api 与 /hub 已代理到 5080）
cd client && npm install && npm run dev
```

将前端构建进后端进程（单进程即可访问全链路）：

```bash
npm --prefix client run build
dotnet run --project src/SkullKing.Server -c Release
```

SQLite 在首次启动时自动迁移，默认文件为工作目录下的 `skullking.db`，可通过 `ConnectionStrings__Default` 更改路径。

## 部署

向 `main` 推送后，[GitHub Actions](.github/workflows/docker.yml) 会构建并推送镜像：

| 镜像 | 说明 |
| --- | --- |
| `ghcr.io/<owner>/skullking-server` | API 与 SignalR Hub |
| `ghcr.io/<owner>/skullking-client` | nginx 静态站点 |

标签：`latest`（默认分支）、git tag（如 `v1.0.0`）、短 SHA。`<owner>` 为仓库所属 GitHub 用户或组织名（小写）。

首次拉取前需在 GitHub Packages 将两个包设为 Public，或在宿主机配置带 `read:packages` 的凭据。

### 本机或单机 Compose

仓库根目录的 [`compose.yaml`](compose.yaml) 在本地构建并映射端口，用于验证，不经过反向代理：

```bash
docker compose up -d --build
# 前端 http://localhost:8081 ，后端 http://localhost:8080
```

生产环境建议同源反代（同一域名下 `/api`、`/hub` 转发到后端，其余给前端），使用已构建的 GHCR 镜像。Traefik 与本服务同机时：

```bash
cd deploy
cp stack.env.example .env   # 填写域名与 IMAGE_OWNER
docker compose -f stack.traefik.yml --env-file .env up -d
```

Portainer、Traefik 跨主机、现场构建等说明见 [deploy/README.md](deploy/README.md)。

### 环境变量

| 变量 | 作用对象 | 说明 |
| --- | --- | --- |
| `IMAGE_OWNER` | Compose | GHCR 命名空间，须为小写 GitHub 用户名或组织名 |
| `IMAGE_TAG` | Compose | 镜像标签，默认 `latest` |
| `SKULLKING_DOMAIN` | Traefik 同机 | 对外域名 |
| `SKULLKING_API_BASE` | 前端容器 | 后端对外地址。同源反代时留空 |
| `Cors__AllowedOrigins` | 后端 | 跨域时的前端 Origin，多个用逗号分隔。同源部署不需要 |
| `ConnectionStrings__Default` | 后端 | SQLite 连接串，容器内默认 `Data Source=/data/skullking.db` |
| `TZ` | 两个容器 | 时区，默认 `Asia/Shanghai` |
| `TRAEFIK_NETWORK` | Traefik 同机 | Traefik 所在 Docker 网络，默认 `traefik` |
| `TRAEFIK_ENTRYPOINT` | Traefik 同机 | HTTPS entrypoint 名称 |
| `TRAEFIK_CERTRESOLVER` | Traefik 同机 | 证书解析器名称 |
| `BIND_ADDR` | Traefik 跨主机 | 发布端口绑定的地址，应为本机内网 IP，勿用 `0.0.0.0` |
| `SERVER_PORT` / `WEB_PORT` | Traefik 跨主机 | 宿主机上后端 / 前端端口，默认 `8080` / `8081` |

完整清单与示例见 [`deploy/stack.env.example`](deploy/stack.env.example)。

## 使用

1. 打开站点，填写显示名称（无需注册）。
2. 左侧「新建群聊」创建房间，获得 6 位群号。
3. 将群号或邀请链接 `https://<host>/j/<群号>` 发给其他成员。
4. 非房主点击「确认参加」，房主点击「发起议程」。最少 2 人、最多 8 人（默认上限 6），超出人数进入旁观。
5. 每轮先选择本周承接量，全员提交后揭示；再从底部快捷条出牌。手牌上方显示本人的承接量、已完成数与差额。

对局结束后可在同一房间再开一轮。大厅中可打开历史回放。

老板键默认为 `Esc`，可在设置中修改；亦可启用失焦后自动切换伪装界面。

## 结构

```
src/SkullKing.Domain/          规则引擎（纯函数，无外部依赖）
src/SkullKing.Contracts/      DTO 与 Hub 契约
src/SkullKing.Application/    大厅、房间、对局编排、超时托管、回放
src/SkullKing.Infrastructure/  EF Core + SQLite
src/SkullKing.Server/        Minimal API + SignalR
client/                       Vue 3 + TypeScript + Vite
deploy/                       Dockerfile、Compose、nginx、部署说明
tests/                        xUnit
```

### 设计要点

**服务端权威。** 客户端只提交意图；可出牌列表以服务端下发的 `playableCardIds` 为准。手牌仅单播给本人；叫牌数字在全员叫完前仅本人可见。

**存命令、不存运行时状态。** 规则引擎由随机种子与命令日志决定。进程重启按日志重放；历史回放使用同一套重放。

**事件与快照分离。** Hub 广播增量事件用于表现，随后的房间快照为权威状态。二者进入同一客户端队列，避免快照抢在出牌动画之前落地。

**掉线保留座位。** 对局中掉线只标记离线。重连凭 token 恢复座位并补发快照。托管在本步限时结束（未设限时则掉线满 2 分钟）后才执行；重连时若仍轮到该玩家，会重新给予完整思考时间。服务重启后另有 90 秒抑制托管窗口。

## 规则实现

70 张核心牌：四花色各 1–14（黑色 JollyRoger 为王牌）、逃跑 5、海盗 5、Tigress 1（出牌时选择作为海盗或逃跑）、美人鱼 2、骷髅王 1。

第 N 轮每人 N 张牌。最大轮数为 `min(房间设置, 70 / 人数)`。

吃墩优先级：美人鱼克骷髅王 > 骷髅王克海盗 > 海盗克美人鱼与数字牌 > 黑色王牌 > 跟牌花色最大点数 > 全部逃跑时最先出牌者得墩。

计分：叫 0 成功得 `轮次 × 10`，失败扣相同分数；叫 N 成功得 `N × 20` 加奖励分，失败按墩差每墩扣 10 且无奖励。奖励分仅在叫牌命中时计入：用 14 得墩 +10（黑 14 为 +20），骷髅王每吃一张海盗 +30，海盗吃骷髅王 +30，美人鱼吃骷髅王 +50。

`Loot`、`Kraken`、`WhiteWhale` 已预留牌面定义与房间开关，当前未加入牌组。

## 测试

```bash
dotnet test
npm --prefix client run build
```

规则层覆盖克制关系、Tigress 双形态、全逃跑墩、跟牌花色、叫 0 与奖励分；编排层覆盖手牌可见性、叫牌保密、超时托管、掉线重连、重启重放与回放一致性。

端到端脚本会启动三名匿名玩家经 Hub 打完一局并核对回放（服务端需先运行）：

```bash
node client/scripts/smoke.mjs
node client/scripts/smoke.mjs --half   # 中途断开，用于验证重启恢复
```
