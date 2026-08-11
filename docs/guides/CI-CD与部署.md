# CI/CD 与部署手册（AWms）

> 长期规格（guides/）。记录本地开发、前后端联调、CI/CD 的**实际运行方式**与本机环境约束。
> 上层约定：docs/methodology/规章制度.md §8.9（分支与 CI/CD 规范）、docs/README.md（归档机制）。

## 〇、本机环境约束（重要，先读）

| 项 | 状态（2026-08-11 实测） | 对策 |
|---|---|---|
| GitHub / git / gh CLI | ✅ 正常（账号 CSJ608，repo 权限） | 推送、PR、workflow 触发走这里 |
| NuGet（api.nuget.org） | ✅ 可达 | dotnet restore 正常 |
| npm（registry.npmjs.org） | ✅ 可达 | pnpm install 正常（CI 上）；**runner 无 node**，前端 dist 仍由 GitHub CI 构建 → artifact → deploy-test 下载 |
| Docker Hub（registry-1.docker.io） | ❌ 不可达 | 镜像一律走 daocloud：`docker.m.daocloud.io` / `mcr.m.daocloud.io` |
| 本机 Docker Desktop | ✅ 运行中 | self-hosted runner 用 docker compose 部署 |
| runner | `C:\actions-runner-awms`（标签 `self-hosted, awms-test`） | 部署测试环境 |
| 数据库 | 本机 5432 为用户私有 PG；AWms 测试库在容器 @5434 | 端口避开 MWms（5433/5080/8081） |

## 一、本地开发环境（dev）

### 1.1 后端（.NET 10 + PostgreSQL）

```powershell
# 起 PostgreSQL（如无本地实例；镜像走 daocloud）
docker run -d --name awms-pg -p 5433:5432 -e POSTGRES_USER=awms -e POSTGRES_DB=awms -e POSTGRES_PASSWORD=<口令> docker.m.daocloud.io/library/postgres:16-alpine

# 环境变量注入（密钥/口令不进 git）
$env:ConnectionStrings__Default = "Host=localhost;Port=5433;Database=awms;Username=awms;Password=<口令>"
$env:AWMS_JWT_SECRET = "<≥32字符>"   # 或 Jwt__SecretKey
$env:Admin__Username = "admin"
$env:Admin__Password = "<初始管理员密码>"

dotnet run --project backend/AWms.Api --no-launch-profile
# 启动即 MigrateAsync + 初始管理员；默认 http://localhost:5000（可 ASPNETCORE_URLS 覆盖）
```

### 1.2 前端（React + Vite）

```powershell
cd frontend
pnpm install --frozen-lockfile
$env:VITE_USE_MOCK = "false"   # 联调切真实后端；默认 mock
pnpm dev                       # http://localhost:5173，/api 由 vite proxy 转发到 localhost:5000
```

质量门禁（提交前三连）：`pnpm build` + `pnpm lint` + `pnpm test`。

## 二、CI/CD 管道（GitHub Actions，AWms）

- 文件：`.github/workflows/ci.yml`；触发：push `main` / `feat/**` / 任何 PR。
- **backend（GitHub-hosted）**：`dotnet restore/build/test`（Release）；集成测试用 **Testcontainers PostgreSQL**（CI 有 Docker，零额外配置）；NuGet 审计随 restore 运行（NU1903 已修复，OpenApi 2.11.0）。
- **frontend（GitHub-hosted）**：`pnpm install --frozen-lockfile` + `pnpm lint` + `pnpm build` + `pnpm test`（Node 22 / pnpm 11.10.0）；上传 `frontend/dist` artifact（`frontend-dist`）。
- **deploy-test（self-hosted，标签 awms-test）**：仅 `main` push 且前后端全绿时执行；步骤：生成 `deploy/.env`（不存在则复制 `.env.example`）→ 下载 artifact 到 `frontend/dist` → `docker compose -f deploy/docker-compose.test.yml up -d --build` → `docker compose ps`。
  - **注意**：deploy-test 用 `shell: powershell`（本机无 pwsh），run 脚本体必须纯 ASCII。

## 三、Self-Hosted Runner（AWms）

- 目录：`C:\actions-runner-awms`；名称 `awms-runner`；标签 `self-hosted, Windows, X64, awms-test`。
- 配置：复制 `C:\actions-runner\actions-runner-win-x64-2.336.0.zip` 解压到新目录，用仓库 registration-token 执行 `config.cmd --url https://github.com/CSJ608/AWms --token <token> --name awms-runner --labels awms-test --work _work --unattended --replace`。
- 启动/重启（后台、隐藏窗口）：
  ```powershell
  $res = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{
    CommandLine = 'cmd.exe /c "cd /d C:\actions-runner-awms && run.cmd"'; CurrentDirectory = 'C:\actions-runner-awms'
  }
  ```
- 自启：HKCU Run 键 `awms-actions-runner` → `cmd /c cd /d C:\actions-runner-awms && run.cmd`。
- 状态确认：`gh api repos/CSJ608/AWms/actions/runners --jq '.runners[] | {name, status}'`

## 四、测试环境（test，三容器栈）

| 服务 | 端口 | 说明 |
|---|---|---|
| web | **8082** | nginx + SPA + `/api` 反代（浏览器直接访问 http://localhost:8082） |
| api | 5081 | .NET API（启动即自动迁移 + 初始管理员；连 awms_test 库） |
| db | 5434 | postgres:16-alpine（healthy） |

- 编排：`deploy/docker-compose.test.yml`；配置：`deploy/.env`（git-ignored，默认口令 change-me，正式前改）。
- 验证命令：
  ```powershell
  curl http://localhost:8082/                     # 前端登录页（200）
  curl http://localhost:5081/api/auth/login -Method POST -ContentType "application/json" -Body '{"username":"admin","password":"<ADMIN_PASSWORD>"}'   # {"code":"OK",...}
  docker compose -f deploy/docker-compose.test.yml ps
  ```

## 五、部署纪律

- **test 环境**：CI 绿 → 自动部署（含自动迁移与初始管理员）。
- **prod 环境**：**保留人工确认**，不做全自动部署（后续批次）。
- 数据库迁移版本化（EF Core Migrations），api 启动自动应用；绝不让 AI 在非空表上乱改结构。
- 密钥/口令一律环境变量或 `deploy/.env`（git-ignored），不入库。

## 六、环境一览

| 环境 | 用途 | 部署方式 | 端口 |
|---|---|---|---|
| dev | 本地开发 | 手动 dotnet run / pnpm dev | 后端 5000，前端 5173 |
| test | 模拟生产 + 联调 | CI 绿 → self-hosted 自动部署 | web 8082 / api 5081 / db 5434 |
| prod | 正式使用 | **人工确认**后部署 | 待定 |

## 七、变更记录

| 日期 | 变更 |
|---|---|
| 2026-08-11 | 初始版本：环境约束 / 本地开发 / CI/CD 管道 / runner / 测试环境 / 部署纪律 |