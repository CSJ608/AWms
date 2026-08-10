# 第3批重做（任务拆分版 v2）— 任务拆解计划（TASKS.md）

> 出具人：后端 AI（Codex）｜日期：2026-08-10
> 依据：docs/reviews/2026-08-10-后端开工指令-第3批-重做（任务拆分版 v2）、docs/reviews/2026-08-10-后端重做复验意见-第3批.md、docs/guides/后端工程规范.md（v1.3）、docs/api/ 契约（通用规范 v2.1 / 认证权限 v0.2 / 导入导出 v0.2 / 物料 v0.4 / 仓库库位 v0.3 / 来源 v0.3 / 批次 v0.6）、docs/decisions/ADR-006
> 流程铁律：一个任务一个提交（任务内可小步多次提交）；每任务自检 `dotnet build` 0 error 0 warning + 相关测试绿 + 验收清单逐条勾选；核心用例不得 Skip；docs/ 只读。

## 任务总览

| 任务 | 标题 | 目标（一句话） | 依赖 | 预计提交信息 |
|---|---|---|---|---|
| T0 | 任务拆解 + 工程骨架 | 建好可 review 的解决方案骨架与任务拆解计划 | - | `docs(T0): 任务拆解计划` + `feat(T0): 工程骨架` |
| T1 | 数据模型 + 种子 | 实体/索引/枚举齐备，初始管理员可登录，初始迁移可应用 | T0 | `feat(T1): 数据模型+种子+初始管理员` |
| T2 | 认证权限 | login/refresh/logout/me/users/roles/permissions + 操作级权限过滤 | T1 | `feat(T2): 认证权限全套端点+RequirePermission` |
| T3 | 编号服务 | PG 原子自增（EF 插值 SQL），并发/耗尽测试在真实 PG 实跑 | T1 | `feat(T3): 编号服务 PG原子自增` |
| T4 | 查询平台 | filter DSL 接入全部 Search 服务，13 操作符真实现，白名单外 400 | T1 | `feat(T4): filter DSL 接入全 Search 服务` |
| T5 | 主数据 API | 物料/仓库/库位/来源/批次 CRUD+searchCode+嵌套 batches/search+排序兜底 | T3,T4 | `feat(T5): 主数据 API` |
| T6 | 导入 | precheck 不落库+失败明细 inline≤200；execute 同事务重校验+真实入库；E2E 不 Skip | T3,T5 | `feat(T6): 导入两阶段+E2E` |
| T7 | 导出 | filter/sort/pageSize 生效；PROCESSING→DONE；后台任务独立 DbContext 作用域 | T3,T4 | `feat(T7): 异步导出` |
| T8 | 幂等 | Idempotency-Key 接线到写端点，同 key 返回首次结果，24h TTL | T1 | `feat(T8): 幂等接线写端点` |
| T9 | 集成测试与全量回归 | WebApplicationFactory + Testcontainers PG；迁移可应用/并发取号/导入E2E/认证全链路；覆盖率≥70% | T2~T8 | `test(T9): 集成测试+覆盖率` |
| T10 | 完工汇报 | 按模板如实填写并 push 分支 | T9 | `docs(T10): 第3批重做完工汇报` |

## T0 任务拆解 + 工程骨架

- **目标**：输出本任务拆解计划并建立可构建、可 review 的解决方案骨架。
- **验收标准**：
  - [ ] backend/交付/TASKS.md 存在且覆盖 T0~T10 全部验收点；
  - [ ] backend/交付/问题清单.md 建立（初始为空表）；
  - [ ] AWms.slnx 引用四层 + 两个测试项目；
  - [ ] global.json 锁定 .NET 10 SDK；Directory.Build.props 统一 TFM/Nullable/TreatWarningsAsErrors；
  - [ ] Directory.Packages.props 集中管理全部包版本（CPM）；
  - [ ] NuGet.Config 指向 nuget.org；
  - [ ] .gitignore 覆盖 bin/obj/.vs/*.user/.env；
  - [ ] `dotnet restore` 成功（网络可达则恢复，不可达记入问题清单）；
- **涉及文件**：backend/AWms.slnx、backend/global.json、backend/Directory.Build.props、backend/Directory.Packages.props、backend/NuGet.Config、backend/.gitignore（根）、backend/交付/TASKS.md、backend/交付/问题清单.md
- **依赖**：无
- **预计提交信息**：`docs(T0): 任务拆解计划`；`feat(T0): 工程骨架（slnx/CPM/global.json/Directory.Build.props）`

## T1 数据模型 + 种子

- **目标**：建齐第3批全部实体/枚举/索引（规范 §3.3），固定 GUID 种子（角色/权限/菜单/角色权限/初始管理员），生成可应用的初始迁移。
- **验收标准**：
  - [ ] 实体：Material/Warehouse/Location/Source/Batch/User/Role/Permission/MenuDefinition/RolePermission/UserRole/Sequence/ImportTask/ImportTaskDetail/IdempotencyRecord；
  - [ ] 枚举与契约一致（MaterialStatus/LabelType/UOM/LocationType/WarehouseMgmtMode/LocationReachability/SourceType/UserStatus/PermissionCategory/Surface/ImportTaskStatus 含 PROCESSING/ImportTaskDirection/BatchStatus 等）；
  - [ ] 索引齐备：唯一(code)、唯一(warehouseId,code)、唯一(materialId,batchNo)、唯一(type,scopeKey,bizDate)、外键列索引、createdAt 索引（时间性列表）；
  - [ ] 种子全部固定 GUID 常量，禁止 Guid.NewGuid()；
  - [ ] 初始管理员：环境变量注入（AWMS_ADMIN_USERNAME/AWMS_ADMIN_PASSWORD）+ 首启重置 + 有测试；
  - [ ] 初始迁移生成且可 review；`dotnet ef migrations` 无 EnsureCreated/EnsureDeleted；
  - [ ] 迁移可应用有验证（T9 集成测试或本任务内等价验证）；
- **涉及文件**：AWms.Domain/Entities/*、AWms.Domain/Enums/*、AWms.Domain/Interfaces/*、AWms.Infrastructure/Data/AWmsDbContext.cs、AWms.Infrastructure/Migrations/*、AWms.Tests/Services/AdminSeedTests.cs 等
- **依赖**：T0
- **预计提交信息**：`feat(T1): 数据模型+种子+初始迁移`；`test(T1): 初始管理员初始化测试`

## T2 认证权限

- **目标**：按认证权限 v0.2 实现 login/refresh(过期换新)/logout/me/users/roles/permissions 全套端点与操作级权限过滤。
- **验收标准**：
  - [ ] LoginResponse 契约形状：{ token, expiresAt, user{id,username,name,status,roles[]}, permissions[], menus:{web[],pda[]} }；
  - [ ] refresh：过期 token 可换新（JwtBearer 对 /api/auth/refresh 放行过期签名），测试用真实过期 token；
  - [ ] logout 204；me 返回 LoginResponse 同构（不重新签发 token）；
  - [ ] users CRUD + 分配角色 + reset-password + USERNAME_DUPLICATED 409；roles CRUD + 分配权限 + 删除引用保护（ROLE_IN_USE 409）；permissions 只读列表；
  - [ ] RequirePermission 落地到写端点/导入/导出等操作端点（不只是定义）；
  - [ ] 密码 Argon2id 哈希；USER_DISABLED/LOGIN_FAILED 语义；
  - [ ] 密钥不进 git：JWT SecretKey/连接串走环境变量/user-secrets，appsettings.json 只留占位；
  - [ ] 单测：登录成功/密码错/停用/refresh 过期换新/LoginResponse 形状/权限过滤；
- **涉及文件**：AWms.Api/Controllers/AuthController.cs、UsersController.cs、RolesController.cs、PermissionsController.cs、AWms.Api/Middleware/RequirePermissionAttribute.cs、AWms.Infrastructure/Services/AuthService.cs、TokenService.cs、Argon2PasswordHasher.cs、AWms.Domain/Dtos/Auth/*、AWms.Tests/Services/AuthServiceTests.cs
- **依赖**：T1
- **预计提交信息**：`feat(T2): 认证权限全套端点+RequirePermission`；`test(T2): 认证权限测试`

## T3 编号服务

- **目标**：Sequence 表 + PG 原子自增（UPDATE...RETURNING）+ 规则注册 + IFormatter，真实 PG 上并发/耗尽测试。
- **验收标准**：
  - [ ] 用 EF `ExecuteSqlInterpolatedAsync`/`FromSqlInterpolated`（参数化），禁止 `{0}`+无名参数；
  - [ ] UPDATE...RETURNING 原子自增；跨天 INSERT ON CONFLICT 原子处理；禁 Guid.NewGuid()（用 Guid.CreateVersion7() 或 EF 生成）；
  - [ ] 并发取号测试（50~100 并行无重复无异常）在 Testcontainers/真实 PG 实跑，不 Skip；
  - [ ] NUMBER_EXHAUSTED 测试（耗尽抛错）在 PG 实跑；
  - [ ] 格式测试：BATCH=260810001、TXN_GROUP=15 位、IMPORT_TASK=IMP-20260810-0001；
- **涉及文件**：AWms.Domain/Entities/Sequence.cs、NumberRule.cs、AWms.Domain/Interfaces/INumberingService.cs、AWms.Infrastructure/Services/NumberingService.cs、AWms.Tests/Services/NumberingServiceTests.cs、AWms.IntegrationTests/NumberingServiceTests.cs（PG 并发/耗尽）
- **依赖**：T1
- **预计提交信息**：`feat(T3): 编号服务 PG原子自增`；`test(T3): 并发/耗尽/格式测试`

## T4 查询平台

- **目标**：QueryParser/QueryService 全操作符真实现，并接入全部 5 个 Search 服务（materials/warehouses/locations/sources/batches）+ 嵌套 batches/search。
- **验收标准**：
  - [ ] 13 操作符 eq/neq/contains/startsWith/in/notIn/gt/gte/lt/lte/between/isNull/isNotNull 全部真实现；
  - [ ] contains/startsWith 正确处理 JsonElement→string；isNull 对值类型不 500；between 日期上界含当天（AddDays(1)）；嵌套 and/or 合并；
  - [ ] 白名单外字段/操作符/排序 → 400 VALIDATION_ERROR（真实 API 生效，非死代码）；
  - [ ] 排序唯一性兜底：用户字段 asc/desc 后追加 id DESC；主数据默认 code asc + id；时间性列表默认 createdAt DESC + id DESC；
  - [ ] 5 个 Search 服务全部消费 request.Filter/sort；
  - [ ] 测试：白名单 400、in/notIn/between/contains/startsWith/isNull、数值/日期/枚举筛选、注入攻击、翻页稳定性；
- **涉及文件**：AWms.Domain/Dtos/Common/CommonDtos.cs（FilterRequest/SortRequest）、AWms.Infrastructure/Services/QueryService.cs、MasterDataService.cs（接线）、AWms.Api/Controllers/*（Search 端点）、AWms.Tests/Services/QueryServiceTests.cs
- **依赖**：T1
- **预计提交信息**：`feat(T4): filter DSL 接入全 Search 服务`；`test(T4): filter 操作符测试`

## T5 主数据 API

- **目标**：物料/仓库/库位/来源/批次 API 全部按契约实现。
- **验收标准**：
  - [ ] POST /api/{resource}/search（5 资源）+ GET 快捷搜索（keyword, pageSize≤10）+ GET 详情；
  - [ ] POST/PUT/DELETE 按契约；重复码 409（MATERIAL/WAREHOUSE/LOCATION/SOURCE_CODE_DUPLICATED）；
  - [ ] 删除引用保护（事务内）：物料-批次、仓库-库位、来源/仓库被引用；
  - [ ] POST /api/materials/{materialId}/batches/search（404 MATERIAL_NOT_FOUND）；
  - [ ] keyword 匹配 code/name/searchCode；searchCode 校验 1..32；
  - [ ] 排序 dir 校验 asc/desc（非法 400）；Enum.Parse 无效值 → 400 不 500；Get 快捷搜索不用 int.MaxValue 全量扫描；
  - [ ] 测试：CRUD/searchCode/keyword/重复码 409/引用保护/嵌套 batches；
- **涉及文件**：AWms.Api/Controllers/MaterialsController.cs、WarehousesController.cs、SourcesController.cs、BatchesController.cs、AWms.Infrastructure/Services/MasterDataService.cs、AWms.Tests/Services/MasterDataServiceTests.cs
- **依赖**：T3、T4
- **预计提交信息**：`feat(T5): 主数据 API`；`test(T5): 主数据测试`

## T6 导入

- **目标**：物料导入两阶段（precheck 不落库 → execute 同事务重校验真实入库），失败明细 inline≤200。
- **验收标准**：
  - [ ] precheck 不写业务数据（ImportTask/明细可留痕，Material 不入库）；
  - [ ] 失败明细 FailureDetail[] inline ≤200，超出给 failReportUrl；ImportTaskResponse 含 failures 字段；
  - [ ] 文件级/字段级/行级业务校验（编码唯一：文件内+库中）；
  - [ ] execute：canExecute=true 才允许；同事务重校验唯一性；真实入库（Material 创建）；DONE/FAILED；
  - [ ] 导入 E2E 测试（multipart 上传→precheck→execute→库中可查）不 Skip；
- **涉及文件**：AWms.Api/Controllers/ImportExportController.cs、AWms.Infrastructure/Services/ImportExportService.cs、AWms.Domain/Dtos/ImportExport/*、AWms.Tests/Services/ImportExportServiceTests.cs、AWms.IntegrationTests/ImportE2ETests.cs
- **依赖**：T3、T5
- **预计提交信息**：`feat(T6): 导入两阶段`；`test(T6): 导入 E2E`

## T7 导出

- **目标**：异步导出任务 PROCESSING→DONE，filter/sort/pageSize 全部生效，后台任务独立 DbContext 作用域。
- **验收标准**：
  - [ ] ExportRequest 的 filter/sort/pageSize 透传并落地到查询；
  - [ ] 枚举补 PROCESSING；创建任务返回 PROCESSING（201），后台执行后 DONE；
  - [ ] 后台任务用 IServiceScopeFactory 独立 scope，不捕获请求作用域 DbContext；
  - [ ] 文件与导入模板同结构；任务留痕（operatorId/operatorName/taskNo 走编号服务）；
  - [ ] 测试：filter/sort/pageSize 生效、状态机 PROCESSING→DONE、失败 EXPORT_FAILED；
- **涉及文件**：AWms.Api/Controllers/ImportExportController.cs、AWms.Infrastructure/Services/ImportExportService.cs、AWms.Domain/Enums/ImportTaskStatus.cs、AWms.Tests/Services/ImportExportServiceTests.cs
- **依赖**：T3、T4
- **预计提交信息**：`feat(T7): 异步导出`；`test(T7): 导出测试`

## T8 幂等

- **目标**：Idempotency-Key 接线到写端点（materials/warehouses/locations/sources/users/roles/import execute 等），同 key 返回首次结果。
- **验收标准**：
  - [ ] 中间件/控制器读取 Idempotency-Key；TryGet/Store 接线；
  - [ ] 同 key 重复写请求返回首次结果（含错误响应）；24h TTL 过期后可重放；
  - [ ] 与业务同事务（IdempotencyRecord 与业务同事务）；
  - [ ] 测试：重复写、并发同 key、TTL 过期重放；
- **涉及文件**：AWms.Infrastructure/Services/IdempotencyService.cs、AWms.Api/Middleware/IdempotencyMiddleware.cs（或控制器内）、AWms.Domain/Entities/IdempotencyRecord.cs、AWms.Tests/Services/IdempotencyServiceTests.cs
- **依赖**：T1
- **预计提交信息**：`feat(T8): 幂等接线写端点`；`test(T8): 幂等测试`

## T9 集成测试与全量回归

- **目标**：WebApplicationFactory + Testcontainers PostgreSQL 真实迁移，覆盖必测清单，产出覆盖率报告。
- **验收标准**：
  - [ ] 集成测试项目真有测试（非空壳）：迁移可应用、认证全链路、并发取号、导入 E2E、导出、幂等、重复码 409、白名单外 400；
  - [ ] 启动即应用全部迁移（fixture 容器）；
  - [ ] 核心用例 0 Skip；
  - [ ] `dotnet test` 全绿（单元+集成）；
  - [ ] 覆盖率报告：核心服务行覆盖 ≥70%（coverlet），报告归档 backend/交付/coverage/；
- **涉及文件**：AWms.IntegrationTests/**、backend/交付/coverage/**
- **依赖**：T2~T8
- **预计提交信息**：`test(T9): 集成测试全套`；`docs(T9): 覆盖率报告`

## T10 完工汇报

- **目标**：按模板如实填写完工汇报并 push 分支。
- **验收标准**：
  - [ ] backend/交付/第3批-重做完工汇报.md：六项模板 + 任务对照表（T0~T10→commit/验收/测试）+ 复验清单逐条闭环 + 问题清单（待裁决单列）+ 未验证/遗留如实列出；
  - [ ] 问题清单完整（含待裁决项）；
  - [ ] 提交并 push feat/backend-3-rework-2；
- **涉及文件**：backend/交付/第3批-重做完工汇报.md、backend/交付/问题清单.md
- **依赖**：T9
- **预计提交信息**：`docs(T10): 第3批重做完工汇报（任务化提交版）`

## 备注
- 执行中如任务边界需调整：先更新本文件并另行提交，再继续。
- 每个任务完成后在“任务总览”勾选状态；最终以 git log 为准。
