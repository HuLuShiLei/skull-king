# 前端先构建。Vite 的 outDir 指向服务端的 wwwroot，所以产物直接落在
# src/SkullKing.Server/wwwroot，下一阶段照原样拷过去就能被静态文件中间件托管。
FROM node:22-alpine AS client
WORKDIR /src

COPY client/package.json client/package-lock.json ./client/
RUN cd client && npm ci

COPY client ./client
RUN cd client && npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server
WORKDIR /src

# 先只拷工程文件，让 restore 这一层能命中缓存。
COPY src/SkullKing.Domain/*.csproj ./src/SkullKing.Domain/
COPY src/SkullKing.Contracts/*.csproj ./src/SkullKing.Contracts/
COPY src/SkullKing.Application/*.csproj ./src/SkullKing.Application/
COPY src/SkullKing.Infrastructure/*.csproj ./src/SkullKing.Infrastructure/
COPY src/SkullKing.Server/*.csproj ./src/SkullKing.Server/
RUN dotnet restore src/SkullKing.Server/SkullKing.Server.csproj

COPY src ./src
COPY --from=client /src/src/SkullKing.Server/wwwroot ./src/SkullKing.Server/wwwroot

RUN dotnet publish src/SkullKing.Server/SkullKing.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# SQLite 文件放在卷里，容器重建不丢对局历史。
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Default="Data Source=/data/skullking.db"

COPY --from=server /app ./

RUN mkdir -p /data && chown -R $APP_UID /data

USER $APP_UID
VOLUME /data
EXPOSE 8080

# 存活探针用 GET /healthz，交给外面的编排系统或反代去打。
ENTRYPOINT ["dotnet", "SkullKing.Server.dll"]
