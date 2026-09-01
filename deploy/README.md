# 部署说明

前后端各是一个容器：

| 容器 | 内容 | 内部端口 | 需要持久化 |
| --- | --- | --- | --- |
| `skullking-server` | ASP.NET Core，只提供 API 和 SignalR Hub | 8080 | 是，`/data` 放 SQLite |
| `skullking-client` | nginx，托管前端静态产物 | 80 | 否 |

## 一、先理解路由模式

推荐**同源模式**：前端和后端挂同一个域名，Traefik 按路径分流。

```
https://sk.example.com/api/*   ─┐
https://sk.example.com/hub/*   ─┴─→ skullking-server:8080
https://sk.example.com/*       ───→ skullking-client:80
```

好处是浏览器眼里全是同源请求：不用配 CORS，WebSocket 不涉及跨站，也不会被浏览器的第三方策略挡住。`deploy/stack.traefik.yml` 里的 `priority` 就是为这个服务的——后端那条规则优先级 100，前端那条 1，否则前端的 `Host()` 规则会把 `/api` 一起吃掉。

如果你确实想分成两个域名（比如 `sk.example.com` + `sk-api.example.com`），需要多做两件事：

1. 前端容器设 `SKULLKING_API_BASE=https://sk-api.example.com`
2. 后端容器设 `Cors__AllowedOrigins=https://sk.example.com`

后端地址是**容器启动时**注入 `index.html` 的，不是构建时写死的，所以同一个前端镜像可以连不同环境的后端。

## 二、镜像从哪来

你现在没有 CI/CD、镜像也没推仓库，但代码已经在 GitHub 上，所以有两条路，选一条即可。

### 路线 A：GitHub Actions 自动构建推 GHCR（推荐）

仓库里已经放好了 `.github/workflows/docker.yml`，推代码就会自动构建两个镜像推到 GHCR。不需要你申请任何密钥——`GITHUB_TOKEN` 是 Actions 自带的。

产出的镜像是：

```
ghcr.io/stoneshilei/skullking-server:latest
ghcr.io/stoneshilei/skullking-client:latest
```

1. 把这次的改动推上去：

```bash
git add -A
git commit -m "拆分前后端容器，补上 Traefik 部署与镜像构建流水线"
git push
```

2. 打开 [Actions 页](https://github.com/StoneShiLei/skull-king/actions) 确认「构建并推送镜像」跑绿了。首次约 3~5 分钟。

   如果 job 只跑了两三秒就红了、点进去一个步骤都没有，多半不是工作流的问题。
   看 annotation 里的原话，常见的是 `your account is locked due to a billing issue`——
   公开仓库的 Actions 虽然免费，但账户被账单锁定会全局阻断 runner 分配，
   去 <https://github.com/settings/billing> 处理完再 Re-run 即可。
   在这期间可以先走下面的路线 B 部署，不受影响。

3. 把镜像设为公开，这样 Portainer 拉取时不用配凭据：
   GitHub 个人主页 → Packages → 分别打开 `skullking-server` 和 `skullking-client`
   → Package settings → Change visibility → Public。

   不想公开也行，那就在 Portainer 里加一个 Registry：
   Registries → Add registry → Custom，地址 `ghcr.io`，用户名填 GitHub 用户名，
   密码填一个勾了 `read:packages` 的 Personal Access Token（classic）。

4. 之后每次改代码 `git push`，Actions 出新的 `latest`，在 Portainer 里点 stack 的
   **Update the stack** 并勾上 **Re-pull image** 就完成升级。

想要可回滚的版本号，就打标签：

```bash
git tag v1.0.0 && git push --tags
```

然后把 stack 环境变量 `IMAGE_TAG` 从 `latest` 改成 `v1.0.0`。

### 路线 B：让 Portainer 直接从 GitHub 拉代码现场构建

完全不碰镜像仓库。代价是构建在你的服务器上跑，首次几分钟，并占几百 MB 构建缓存。

Portainer → Stacks → Add stack → 选 **Repository**：

- Repository URL：`https://github.com/StoneShiLei/skull-king`（私有仓库要额外填 PAT）
- Repository reference：`refs/heads/main`
- Compose path：`deploy/stack.traefik.build.yml`

升级时点 **Pull and redeploy**，它会重新拉代码并重建。

两条路线的 compose 文件除了「拉镜像 / 现场构建」这一处，其余完全一致，随时可以互换。

## 三、在 Portainer 上部署

前提：Traefik 已经跑起来，并且有一个外部 Docker 网络（一般叫 `traefik`）。

1. DNS 把 `sk.example.com` 解析到服务器。

2. Portainer → Stacks → Add stack，名字填 `skullking`。

3. 用路线 A 的话选 **Web editor**，把 `deploy/stack.traefik.yml` 的内容粘进去；
   用路线 B 的话按上一节选 Repository。

4. 在下方 **Environment variables** 里逐条添加（参照 `deploy/stack.env.example`）：

| 变量 | 说明 |
| --- | --- |
| `SKULLKING_DOMAIN` | 对外域名，如 `sk.example.com` |
| `IMAGE_OWNER` | GitHub 用户名（全小写），路线 B 不需要 |
| `IMAGE_TAG` | 留空即 `latest` |
| `TRAEFIK_NETWORK` | Traefik 所在的外部网络名，默认 `traefik` |
| `TRAEFIK_ENTRYPOINT` | Traefik 的 HTTPS entrypoint 名，常见是 `websecure` 或 `https` |
| `TRAEFIK_CERTRESOLVER` | Traefik 里配的证书解析器名，常见是 `letsencrypt` 或 `le` |
| `TZ` | 时区，默认 `Asia/Shanghai` |
| `SKULLKING_API_BASE` | 同源模式留空 |

   `TRAEFIK_ENTRYPOINT` 和 `TRAEFIK_CERTRESOLVER` 一定要和你现有 Traefik 的静态配置对上，
   名字对不上的表现是：容器起来了但访问 404，或者证书一直签不下来。

5. Deploy the stack。

6. 打开 `https://sk.example.com`，填个昵称就能进。左栏「新建群聊」开房，把 6 位群号或
   `https://sk.example.com/j/群号` 发给同事。

## 四、验证部署是否正常

```bash
# 后端活着：返回 {"status":"ok"}
curl -fsS https://sk.example.com/api/healthz

# 路径分流对了：应该返回 401（没带 token），而不是 200 的 HTML
curl -i https://sk.example.com/api/rooms

# 前端路由回退对了：应该返回 200 和 HTML
curl -i https://sk.example.com/j/ABCDEF
```

第一条如果返回 HTML，说明 `/api` 没有被分流到后端，去检查两个 router 的
`priority` 和 `rule` 有没有被改动，以及 `TRAEFIK_ENTRYPOINT` 填得对不对。

WebSocket 是否通只能在浏览器里看：F12 → Network → WS，应该有一条
`/hub/game` 的连接处于 101/open 状态。如果它在反复重连，通常是 Traefik 那边
把 WebSocket 升级头过滤了，检查有没有给这个 router 挂上会改写请求头的中间件。

## 五、数据与备份

对局历史在 `skullking-data` 卷里的 SQLite 文件。备份就是把文件拷出来：

```bash
docker run --rm -v skullking_skullking-data:/data -v $(pwd):/backup alpine \
  sh -c "cp /data/skullking.db* /backup/"
```

卷名前缀是 Portainer 里的 stack 名，实际名字用 `docker volume ls` 确认。

如果你把 `/data` 换成宿主目录挂载（bind mount），记得先把目录属主改成容器里的运行用户
（镜像用的是非 root 的 `$APP_UID`，通常是 1654），否则容器起来会报无法写数据库。
用命名卷就没这个问题，Docker 会自动继承镜像里的属主。

进行中的对局状态在后端内存里，同时每一步命令都落了盘，所以容器重启会自动重放恢复，
并额外留 90 秒重连窗口，期间不会触发超时托管。

## 六、几个已知限制

- **后端只能跑一个副本**。房间状态在内存里，SignalR 也没接 backplane。要横向扩容
  得先引入 Redis backplane 并把房间状态外置，目前没做。
- **后端容器别直接暴露到公网**。它信任 `X-Forwarded-*` 头且不校验来源，这是为了在
  反代后面能拿到真实 IP。只让 Traefik 能访问它就好，stack 里也没有 `ports:` 映射。
- **匿名 token 存在浏览器 localStorage 里**，换浏览器等于换人。这是刻意的：
  不想为了一个摸鱼小游戏做账号体系。
