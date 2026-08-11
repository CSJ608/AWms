# AWms 后端 API 镜像（多阶段构建）
# 构建上下文 = backend/（见 deploy/docker-compose.test.yml）
# 本机 Docker Hub 不可达 → 基础镜像默认走 daocloud；其它环境可用 --build-arg 覆盖
ARG SDK_IMAGE=mcr.m.daocloud.io/dotnet/sdk:10.0
ARG RUNTIME_IMAGE=mcr.m.daocloud.io/dotnet/aspnet:10.0

FROM ${SDK_IMAGE} AS build
WORKDIR /src
COPY AWms.slnx ./
COPY Directory.Build.props Directory.Packages.props NuGet.Config global.json ./
COPY AWms.Domain/ AWms.Domain/
COPY AWms.Infrastructure/ AWms.Infrastructure/
COPY AWms.Api/ AWms.Api/
RUN dotnet restore AWms.Api/AWms.Api.csproj
RUN dotnet publish AWms.Api/AWms.Api.csproj -c Release -o /app/publish --no-restore

FROM ${RUNTIME_IMAGE} AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AWms.Api.dll"]