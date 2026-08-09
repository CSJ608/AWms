# API 契约导航与通用规范（草案 v0.1，C-04）

> 状态：**草案（2026-08-10，C-04，待评审）**。锁定后为前后端唯一依据。
> 依据：数据模型 v0.1、概念设计 v1.0、框架设计 v0.2、ADR-004。

## 一、导航（docs/api/）

| 文件 | 内容 | 状态 |
|---|---|---|
| README.md（本文件） | 导航 + **通用规范** | 🟡 草案 |
| 枚举与错误码.md | 全部枚举与错误码 | 🟡 草案 |
| 认证权限.md | 登录/用户/角色/权限/菜单 | 待起草 |
| 物料.md / 仓库库位.md / 批次.md / 来源.md | 主数据 | 待起草 |
| 入库单.md / 收货.md | 入库链（预建单/收货/质检/上架） | 待起草 |
| 库存.md | 库存/库位库存/流水/可用 | 待起草 |
| 工作台.md | 首页聚合 | 待起草 |
| 标签解析.md | AWMS1 解析契约 | 待起草 |
| 附件.md / 打印.md / 导入导出.md | 支撑能力 | 待起草 |
| 数据模型-草案.md | 概念级数据模型（C-02 依据） | 🟡 草案 |

## 二、通用规范

### 2.1 响应格式（统一 envelope）

成功（2xx）：

```json
{ "code": "OK", "message": "ok", "data": { } }
```

业务错误（4xx / 5xx）：

```json
{ "code": "MATERIAL_CODE_DUPLICATED", "message": "物料编码已存在", "data": null }
```

- HTTP 语义：200 成功；400 参数/校验；401 未鉴权；403 无权限；404 不存在；409 冲突（重复/状态非法）；422 业务校验失败；500 内部错误。
- `code` 为稳定业务码（大写蛇形）；`message` 随 Accept-Language 本地化（**前端只用 code 映射 i18n，message 作兜底显示**）。
- 错误响应中 `data` 可带错误详情（如导入失败明细、字段校验错误列表）。

### 2.2 分页

- 所有列表接口统一返回：`{ "items": [...], "total": 0, "page": 1, "pageSize": 20 }`。
- 请求参数：`page`（≥1）、`pageSize`（1..200；**`pageSize=0` 表示全量**，如仓库/库位小表）。
- 筛选参数：空值不发送；各模块契约声明支持哪些筛选字段。
- 排序：默认按模块契约定义（通常创建时间倒序）；排序参数后续统一。

### 2.3 时间与数量

- 时间：ISO 8601 UTC（`yyyy-MM-dd'T'HH:mm:ss'Z'`），前端本地化展示。
- 日期：`yyyy-MM-dd`。
- **数量：JSON 一律用字符串**（decimal(18,4)，避免浮点精度问题），前端 formatQuantity 展示（去尾零）。
- ID：UUID 字符串。

### 2.4 鉴权

- 登录：`POST /api/auth/login` 返回 `token` + 用户信息 + 权限/菜单。
- 请求头：`Authorization: Bearer <token>`；`Accept-Language: zh|en`。
- 401 → 前端清除会话跳登录；403 → 提示无权限。
- 独立账号（ADR-001/框架设计 v0.2）。

### 2.5 i18n

- 后端：`message` 按 Accept-Language 本地化（zh/en）。
- 前端：错误码 → i18n key（`ERROR.<CODE>`），见《枚举与错误码》。

### 2.6 幂等（防重复提交）

- **写操作支持幂等键**：请求头 `Idempotency-Key: <uuid>`。
- 后端记录 key+结果；重复 key 返回首次结果（不重复执行）。
- 适用：收货提交、质检、上架等关键写操作（MWms 联调教训：重复点击提交两笔）。

### 2.7 错误处理约定

- 前端**只判 code**，不匹配 message。
- 未知 code → `UNKNOWN_ERROR`；网络失败 → `NETWORK_ERROR`（前端本地）。

## 三、待确认（C-04 评审）

1. 统一 envelope `{code,message,data}` + HTTP 语义 —— 确认？
2. 列表统一分页 `{items,total,page,pageSize}`，`pageSize=0` 全量 —— 确认？
3. 数量字段 JSON 用字符串（decimal 精度）—— 确认？
4. 写操作幂等键（Idempotency-Key）—— 确认？
5. 错误码 `DOMAIN_REASON` 大写蛇形、前端只判 code —— 确认？

## 四、修订记录

| 日期 | 变更 |
|---|---|
| 2026-08-10 | v0.1 初始草案 |
