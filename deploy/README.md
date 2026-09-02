# 部署

前后端各一个容器：

| 服务 | 内容 | 容器端口 | 持久化 |
| --- | --- | --- | --- |
| `skullking-server` | ASP.NET Core：HTTP API 与 SignalR Hub | 8080 | `/data`（SQLite） |
| `skullking-client` | nginx 静态站点 | 80 | 无 |

## Compose 文件

| 场景 | 文件 |
| --- | --- |
| Traefik 与本服务同机，拉取 GHCR | [`stack.traefik.yml`](stack.traefik.yml) |
| Traefik 与本服务同机，本机构建 | [`stack.traefik.build.yml`](stack.traefik.build.yml) |
| Traefik 在其他主机，拉取 GHCR | [`stack.remote-traefik.yml`](stack.remote-traefik.yml) |
| Traefik 在其他主机，本机构建 | [`stack.remote-traefik.build.yml`](stack.remote-traefik.build.yml) |
| 本机验证、不使用反代 | 仓库根目录 [`compose.yaml`](../compose.yaml) |

除「镜像来源」与「标签发现 / 发布端口」外，上述 stack 的运行时配置一致，可按环境替换文件。

## 路由

推荐同源：同一域名由反代按路径分流。

```
https://sk.example.com/api/*   ─┐
https://sk.example.com/hub/*   ─┴─→ skullking-server:8080
https://sk.example.com/*       ───→ skullking-client:80
```

同源时浏览器对 API 与 WebSocket 均为 same-origin，无需 CORS。`stack.traefik.yml` 中后端路由 `priority` 为 100、前端为 1，避免仅含 `Host()` 的规则吞掉 `/api` 与 `/hub`。

若前后端使用不同域名（例如 `sk.example.com` 与 `sk-api.example.com`）：

1. 前端容器设置 `SKULLKING_API_BASE=https://sk-api.example.com`
2. 后端容器设置 `Cors__AllowedOrigins=https://sk.example.com`

`SKULLKING_API_BASE` 在容器启动时写入 `index.html`，不在镜像构建时固化，因此同一前端镜像可用于多个后端地址。

## 镜像

推送到默认分支或打 `v*` 标签时，[`.github/workflows/docker.yml`](../.github/workflows/docker.yml) 构建并推送：

```
ghcr.io/<owner>/skullking-server:<tag>
ghcr.io/<owner>/skullking-client:<tag>
```

`<owner>` 为仓库所有者的小写形式。`latest` 对应默认分支；git tag 会额外生成同名镜像标签。

GitHub Packages 默认私有。公开仓库仍需在包设置中改为 Public，或在部署主机配置 GHCR 凭据（用户名为 GitHub 账号，密码为具有 `read:packages` 的 PAT）。

升级：拉取新镜像后滚动更新。Portainer 中为 Update stack 并勾选重新拉取镜像。固定版本时将 `IMAGE_TAG` 设为对应 git tag。

无法使用 GHCR 时，改用 `*.build.yml` 在宿主机构建，或：

```bash
git clone https://github.com/<owner>/skull-king.git /opt/skull-king
cd /opt/skull-king/deploy
cp stack.env.example .env
docker compose -f stack.remote-traefik.build.yml -p skullking up -d --build
```

Portainer 通过 Repository 构建时，若出现 `mkdir /.docker: permission denied`，属于 [Portainer 内嵌 Compose / Buildx 的问题](https://github.com/portainer/portainer/issues/13143)，可改为在 Docker 主机上执行上述命令。

## 环境变量

参照 [`stack.env.example`](stack.env.example)。按所选 stack 填写对应分组。

| 变量 | 适用 | 说明 |
| --- | --- | --- |
| `IMAGE_OWNER` | 拉取 GHCR | GitHub 用户或组织名，必须小写 |
| `IMAGE_TAG` | 拉取 GHCR | 默认 `latest` |
| `SKULLKING_DOMAIN` | Traefik 同机 | 对外域名，DNS 指向 Traefik 所在主机 |
| `SKULLKING_API_BASE` | 前端 | 同源留空；分域名时填后端对外 URL |
| `Cors__AllowedOrigins` | 后端 | 仅分域名时需要 |
| `TZ` | 两个容器 | 默认 `Asia/Shanghai` |
| `TRAEFIK_NETWORK` | Traefik 同机 | 外部 Docker 网络名，默认 `traefik` |
| `TRAEFIK_ENTRYPOINT` | Traefik 同机 | HTTPS entrypoint，须与 Traefik 静态配置一致 |
| `TRAEFIK_CERTRESOLVER` | Traefik 同机 | 证书解析器名称，须与静态配置一致 |
| `BIND_ADDR` | Traefik 跨主机 | 绑定本机内网 IP。`0.0.0.0` 会在全部网卡（含公网）上监听 |
| `SERVER_PORT` | Traefik 跨主机 | 后端宿主机端口，默认 `8080` |
| `WEB_PORT` | Traefik 跨主机 | 前端宿主机端口，默认 `8081` |

`TRAEFIK_ENTRYPOINT` 或 `TRAEFIK_CERTRESOLVER` 与现网不一致时，常见现象为 HTTP 404 或证书无法签发。

## Traefik 同机

前提：Traefik 已运行，且存在可供本 stack 加入的外部网络（通常名为 `traefik`）。

1. 将域名解析到该主机。
2. 使用 `stack.traefik.yml`（或 Portainer Web editor / Repository 指向该文件）。
3. 配置环境变量：至少 `SKULLKING_DOMAIN`、`IMAGE_OWNER`，以及与现网一致的 Traefik 网络、entrypoint、证书解析器。
4. 部署后访问 `https://<域名>`。

## Traefik 位于其他主机

Docker provider 只能通过本机 socket 发现容器，跨主机无法读取 `traefik.*` 标签。此时使用 `stack.remote-traefik.yml`，将端口发布到跑容器的主机，再在 Traefik 主机上用 file provider 指向这些上游。

```
浏览器 ── HTTPS ──→ Traefik 主机
                         │ 内网 HTTP
                    应用主机 :8080（API/Hub）、:8081（静态站）
```

1. 部署 `stack.remote-traefik.yml`，设置 `BIND_ADDR` 为**应用主机内网 IP**，以及 `IMAGE_OWNER`、端口等。不要使用 `0.0.0.0`。若该主机网卡同时暴露公网，应在防火墙中仅允许 Traefik 主机访问这两个端口。
2. 将 [`traefik-dynamic.example.yml`](traefik-dynamic.example.yml) 放到 Traefik 的 file provider 目录，替换域名、上游 IP、端口与 entryPoint。静态配置需启用 file provider，例如：

```yaml
providers:
  file:
    directory: /etc/traefik/conf
    watch: true
```

   若已配置 `tls.stores.default` 默认证书，router 使用 `tls: {}` 即可；否则改为 `tls.certResolver` 并填写解析器名称。

3. 不要为这两条 router 配置仅允许内网的 `ipAllowList`，否则外部受邀用户无法进入。

两台主机之间为明文 HTTP。若中间网络不可信，应使用隧道或改为 HTTPS 上游。

## 验证

```bash
# 后端存活，JSON {"status":"ok"}
curl -fsS https://sk.example.com/api/healthz

# 应为 401（未带 token），而不是 200 HTML
curl -i https://sk.example.com/api/rooms

# SPA 回退：200 与 HTML
curl -i https://sk.example.com/j/ABCDEF
```

`/api/healthz` 若返回前端 HTML，说明路径未转到后端，检查 router 的 `priority`、`rule` 与 `TRAEFIK_ENTRYPOINT`。

WebSocket：浏览器开发者工具 Network → WS，`/hub/game` 应为 101。反复重连通常是反代丢弃了 `Connection` / `Upgrade`。

容器尚未启动时，可用 `--resolve` 探测 Traefik：**502 表示路由已命中、上游未就绪；404 表示规则未匹配**。

```bash
curl -sk -o /dev/null -w '%{http_code}\n' \
  --resolve sk.example.com:443:127.0.0.1 https://sk.example.com/api/healthz
```

若启用了 Traefik API，可查询 router 状态：

```bash
curl -s http://127.0.0.1:8080/api/http/routers | grep -A5 skullking
```

## 数据与备份

对局归档位于名为 `skullking-data` 的卷（实际名称带 stack 前缀，以 `docker volume ls` 为准）：

```bash
docker run --rm -v skullking_skullking-data:/data -v "$(pwd)":/backup alpine \
  sh -c "cp /data/skullking.db* /backup/"
```

若改为 bind mount，目录属主须为镜像中的非 root 用户（`$APP_UID`，一般为 1654），否则无法写入数据库。命名卷会继承镜像中的属主。

进行中的对局在内存中；每步命令同时落盘。容器重启后重放恢复，并在约 90 秒内不启动托管。

## 限制

- 后端不可水平扩展：房间状态在进程内存中，SignalR 未配置 backplane。
- 不要将后端直接暴露到公网。进程信任 `X-Forwarded-*` 且不校验来源，以便反代传递真实 IP。同机 Traefik 的 stack 不发布 `ports`。
- 身份 token 保存在浏览器 `localStorage`，更换浏览器即新身份。当前无账号体系。
