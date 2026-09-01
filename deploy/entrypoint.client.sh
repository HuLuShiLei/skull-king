#!/bin/sh
# nginx 官方镜像会按顺序执行 /docker-entrypoint.d 下的脚本，这里趁启动前
# 把后端地址写进 index.html 的占位符，实现「一个镜像多环境」。
set -e

TARGET=/usr/share/nginx/html/index.html
PLACEHOLDER='__SKULLKING_API_BASE__'
VALUE="${SKULLKING_API_BASE:-}"

if [ ! -f "$TARGET" ]; then
  echo "[skullking] 找不到 $TARGET，跳过配置注入"
  exit 0
fi

# 占位符已经被换掉说明容器是重启而非首次启动，直接跳过。
if ! grep -q "$PLACEHOLDER" "$TARGET"; then
  echo "[skullking] 配置已注入过，跳过"
  exit 0
fi

# 用 | 作分隔符，免得和地址里的斜杠打架。
sed -i "s|$PLACEHOLDER|$VALUE|g" "$TARGET"

if [ -z "$VALUE" ]; then
  echo "[skullking] 后端地址：同源（由反代按 /api 与 /hub 分流）"
else
  echo "[skullking] 后端地址：$VALUE"
fi
