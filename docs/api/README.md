# API 契约导航与通用规范（通用规范 v2.1；第 4 批已锁定）

> 状态：通用规范 **v2.1 已定稿**；第 4 批相关契约已于 **2026-08-21 锁定**。锁定依据为 `84c5c8a` 的前后端复核均无异议，本页导航所列锁定版本是第 4 批实现的唯一契约基线。

## 一、导航（docs/api/）

| 文件 | 内容 | 状态 |
|---|---|---|
| README.md（本文件） | 导航 + 通用规范（含编号、查询和筛选规范） | ✅ 定稿 v2.1 |
| 枚举与错误码.md | 全部枚举与错误码 | 🔒 锁定 v1.9 |
| 物料.md / 仓库库位.md / 来源.md | 主数据 | 🔒 锁定 v0.5 / v0.4 / v0.4；第 3 批已实现 |
| 批次.md | 主数据（系统自动建批次） | 🔒 锁定 v0.7；第 3 批已实现查询 |
| 认证权限.md | 登录、用户、角色、权限、菜单 | 🔒 锁定 v0.4（v0.3 已实现，第 4 批实现增量） |
| 入库单.md / 收货.md | 入库链 | 🔒 锁定 v0.5 |
| 库存.md / 工作台.md | 库存与聚合 | 🔒 锁定 v0.3 / v0.1；待第 5 批实现 |
| 标签解析.md / 附件.md / 打印.md | 收货链支撑能力 | 🔒 锁定 v0.2 / v0.3 / v0.4 |
| 导入导出.md | 平台导入导出能力 | 🔒 锁定 v0.2；第 3 批已实现物料模块 |
| 数据模型-草案.md | 概念级数据模型（C-02 依据） | 🔒 锁定 v0.6 |

## 二、通用规范

### 2.1 响应格式（统一 envelope + HTTP 状态码，混合方案）
`{ "code": "OK", "message": "ok", "data": { } }` / 错误 `{ "code": "MATERIAL_CODE_DUPLICATED", "message": "...", "data": null }`
- 状态码表达成败类别，code 表达精确业务原因；HTTP 语义：200/400/401/403/404/409/422/500。

### 2.2 分页
- 列表统一 `{ items, total, page, pageSize }`；`pageSize=0` 全量；筛选空值不发送。

### 2.3 时间与数量
- 时间 ISO 8601 UTC；日期 yyyy-MM-dd；数量 JSON 字符串（decimal 18,4）；ID UUID。

### 2.4 鉴权
- Bearer token；Accept-Language；401 静默刷新重放；403 提示。

### 2.5 i18n
- 目标约定：错误文案归后端（messages.zh/en.json，未知语言回退中文）；前端只维护 UI 文案；两端不共享 i18n 文件。
- 当前过渡：服务端错误文案 i18n 登记为 P-22，本批后置。实现前服务端至少返回稳定中文 message；前端仍以 code 决定业务分支，不解析 message。

### 2.6 幂等
- 写操作 Idempotency-Key；重复返回首次结果；适用收货/质检/上架等。

### 2.7 前端错误处理
- 默认展示后端 message；code=前端判断"是否需要做特别的事"的钥匙（静默或提示由前端按 code+上下文决定）。

### 2.8 错误码形式
- 字符串 code（模块前缀）+ hex 内部编号（区间预占）；前端只消费字符串 code。

### 2.9 编号规范（编号服务，2026-08-10 确认 v1.3）

**设计原则（2026-08-10 讨论定）**：

1. **前缀是为了"人"**：业务熟手看编号即知业务类型——前缀 ≤2 位固定字符（如 PO/RCP），用于用户可见单据。
2. **不给用户看的编号可无前缀**：如事务组号，纯日期+流水号即可。
3. **规则可插拔、可调整**：前缀/日期/序号长度/重置周期/组合方式均可配置；**规则变更只影响新号，不影响历史号**。
4. **未来扩展预留**：批量生成（如 HU 码一次生成 N 个）、复合编码（自定义组合段，如 HU 码某位代表产品/型号+批次+流水）、全局唯一码的位数设计——本期不做，但编号服务保留扩展点。

**规则注册属性**（每种编号注册一条规则）：

| 属性 | 说明 | 示例 |
|---|---|---|
| type | 编号类型（注册键） | INBOUND_ORDER / RECEIPT / BATCH / TXN_GROUP / UNIQUE_CODE / IMPORT_TASK |
| scopeKey | 作用域键 | GLOBAL / MATERIAL / 动态参数（如单据类型 ty） |
| prefix | 前缀（可空、可动态） | "PO" / "RCP" / 空 |
| dateFormat | 日期格式（可空） | yyyyMMdd / yyMMdd / 空 |
| seqLength | 序号长度（补零） | 4 / 3 / 9 |
| resetPeriod | 重置周期 | DAILY / MONTHLY / NEVER |
| onExhaustion | 序号耗尽行为 | THROW（抛 NUMBER_EXHAUSTED）/ WRAP（默认禁用） |
| formatter | 组合方式（默认 前缀+日期+序号；可插拔） | 默认 / 自定义（未来 HU） |

**本期注册表**：

| type | scopeKey | prefix | dateFormat | seqLen | reset | 示例 |
|---|---|---|---|---|---|---|
| INBOUND_ORDER | 按 ty 动态 | PO/PR/OT | yyyyMMdd | 4 | DAILY | PO-20260810-0001 |
| RECEIPT | GLOBAL | RCP | yyyyMMdd | 4 | DAILY | RCP-20260810-0001 |
| BATCH | MATERIAL | 无 | yyMMdd | 3 | DAILY | 260810001 |
| TXN_GROUP | GLOBAL | **无** | yyMMdd | **9** | DAILY | **260810000000001（15 位）** |
| UNIQUE_CODE | GLOBAL | BOX | yyyyMMdd | 4 | DAILY | BOX-20260810-0001 |
| IMPORT_TASK | GLOBAL | IMP | yyyyMMdd | 4 | DAILY | IMP-20260810-0001 |

**实现与扩展**：

- 序号表 Sequence(type, scopeKey, bizDate, lastNo)，唯一(type,scopeKey,bizDate)，原子自增（UPDATE...RETURNING，行锁）。
- **批量分配**：支持一次分配 N 个连续序号（NextN），为未来 HU 批量生成预留。
- **复合规则**：formatter 可插拔——默认 前缀+日期+序号；未来 HU 等自定义组合段（如产品/型号位+批次+流水）。
- **规则变更只影响新号**；与业务同一事务；回滚跳号允许（唯一+单调，不追求连续）。
- 唯一性：对外编号在作用域内唯一；批次号按物料+天唯一；UUID 仅内部主键；前端不生成编号。

### 2.10 查询与筛选规范（2026-08-10 确认）

**两层并存**：

1. **固定参数（常用场景）**：各接口保留常用筛选（code/name 的 eq/contains、日期区间、状态等），简单直观、索引友好。
2. **通用查询 DSL（filter，高级场景）**：接口可选用，支持任意字段组合：

```json
{
  "op": "and",
  "conditions": [
    { "field": "materialCode", "op": "contains", "value": "MAT" },
    { "field": "status", "op": "eq", "value": "AVAILABLE" },
    { "field": "totalQty", "op": "gte", "value": "100" },
    { "field": "createdAt", "op": "between", "value": ["2026-08-01", "2026-08-10"] }
  ]
}
```

- **操作符白名单**：eq / neq / contains / startsWith / in / notIn / gt / gte / lt / lte / between / isNull / isNotNull。
- **字段白名单**：每个接口声明可筛选字段（field registry）；白名单外字段 → 400 VALIDATION_ERROR。
- 全部参数化（防注入）；支持嵌套 and/or。
- **固化（2026-08-10）**：in/notIn 的 value 为数组；between 对纯日期字段按“当日 00:00 ~ 次日 00:00”处理（含当日全天）；sort 本期仅支持单列（单元素数组）。

**排序（sort 白名单）**：

- 每个接口声明**可排序字段白名单**（如 materialCode / totalQty / status / updatedAt）；
- 请求：`sort=[{"field":"totalQty","dir":"desc"}]`；field 必须在白名单内、dir ∈ asc/desc，否则 400；
- 默认排序由各接口定义（如 occurredAt desc）。
- **排序唯一性兜底（v2.1，2026-08-10 用户确认）**：分页结果必须是**确定性全序**。服务端在用户排序（或默认排序）之后**自动追加唯一兜底列**，不暴露给前端（API 的 sort 仍为单元素数组）：
  - **默认排序**：时间性列表（批次、导入导出任务、流水/日志等）默认 `createdAt DESC, id DESC`（**最新在前**，禁止默认时间升序导致新数据沉底）；主数据列表（物料/仓库/库位/来源）默认业务码 `asc, id`（如 code asc）。
  - **用户自定义排序**：用户字段 `asc/desc` 后，服务端追加 `id DESC` 兜底（唯一且稳定；id 为 UUID v7 时同值记录近似后插入在前）。
  - 禁止仅按非唯一列排序（会导致翻页重复/漏行）。

**传输约定（v1.9，2026-08-10 用户裁决）**：
- **标准列表查询统一走 POST /api/{resource}/search**（body JSON：{ keyword?, 固定参数..., filter?, sort?, page?, pageSize? }）；GET 的 query string 不适合复杂 JSON filter/sort，避免再出现"GET+body"这类歧义实现。
- GET /api/{resource}?keyword=&pageSize= 仅保留为**引用选择器快捷搜索**（轻量，pageSize≤10）。
- GET /api/{resource}/{id} 详情不变。
- 嵌套列表（如 /api/warehouses/{id}/locations）同样：POST .../locations/search 为标准查询；GET .../locations?keyword= 为快捷搜索。
- 前端列表页/高级筛选一律调 POST /search；引用选择器快捷搜索可继续用 GET keyword。

**前端**：筛选 UI 由 `SearchField[]` 元数据驱动（字段/控件类型/选项源/操作符），映射为固定参数或 filter DSL。

**本期实现范围**：通用 QueryParser（字段白名单 + 操作符白名单 + 参数化）+ 运行时元数据端点（GET /api/meta/fields/{resource}）作为平台能力，主要列表（物料/库存/流水/入库单）启用；其余接口按需启用。

**字段元数据（FieldMeta，2026-08-10 确认）**：

- 每个列表接口声明可筛选字段的元数据：`field / labelKey / type / operators / options?`。
- `type`：string / number / decimal / date / datetime / bool / enum / uuid / ref（ref 带 `refResource`，如 materials/warehouses）。
- `options`：枚举字段提供 `[{ value, labelKey }]`；ref 字段由引用资源提供选项。
- 操作符按类型给默认集（string: eq/contains/startsWith/in；number/decimal: 比较+between；enum: eq/in/neq；date/datetime: 比较+between；bool: eq；uuid/ref: eq/in）。
- **前端控件由 type 推导**：string→文本框；enum→下拉；bool→开关；number/decimal→数字输入（可区间）；date/datetime→日期选择器（可区间）；ref→引用选择器。
- **来源**：① 契约文档内定义（各接口字段元数据表）；② **运行时元数据端点 `GET /api/meta/fields/{resource}`**（推荐本期实现，返回 FieldMeta[]，前端动态渲染筛选区，新增字段自动生效）。



**ref（引用）字段的交互模式（2026-08-10 确认）**：

- FieldMeta 中 `type=ref` + `refResource`（如 materials / warehouses / batches）。
- 前端提供**通用引用选择器（ReferencePicker）**，所有 ref 字段共用，不写死具体资源：
  1. **快捷搜索**：文本框输入 keyword（匹配该资源"搜索字段集"），防抖调 `GET /api/{refResource}?keyword=&pageSize=10`，下拉候选，选中返回 id；
  2. **完整选择弹窗**：打开引用资源的标准列表弹窗（分页+筛选+排序，复用该资源字段元数据与 filter DSL），搜索选择后返回。
- 引用资源需提供：轻量搜索（keyword 参数）+ 标准列表接口（已具备）。

**引用实体规则（2026-08-10 确认）**：

- 凡作为 ref 选择器目标的实体，契约**必须提供 `keyword` 搜索**；建议提供可选 `searchCode`（助记码），`keyword` 匹配 code/name/searchCode。
- 无需助记的实体用自然编码纳入 keyword：批次=batchNo、用户=username/name、入库单/收货单=orderNo/receiptNo。
- 本期带 searchCode 的实体：物料 / 仓库 / 库位 / 来源（供应商·车间）。

## 三、修订记录

| 日期 | 变更 |
|---|---|
| 2026-08-10 | v0.1 初始草案 |
| 2026-08-10 | v0.2 错误模型修订 + 后端 i18n 独立 + hex |
| 2026-08-10 | v0.3 HTTP 状态码理由/数量示例/静默动作 |
| 2026-08-10 | v1.0 定稿：code=判断钥匙 |
| 2026-08-10 | v1.1：编号规范 2.9 |
| 2026-08-10 | v1.2：规则注册属性；批次号 YYMMDD+3 位 |
| 2026-08-10 | v1.3：编号设计原则（前缀为人/内部号无前缀/规则可插拔）；事务组号 15 位；批量分配与复合规则扩展点 |
| 2026-08-10 | v1.4：新增 2.10 查询与筛选规范（固定参数 + filter DSL + sort 白名单） |
| 2026-08-10 | v1.5：2.10 增加字段元数据 FieldMeta（type/operators/options）+ 运行时元数据端点 |
| 2026-08-10 | v1.6：ref 字段交互模式（通用 ReferencePicker：快捷搜索+完整弹窗）+ keyword 约定 |
| 2026-08-10 | v1.7：引用实体规则（keyword 必提供；searchCode 建议提供；无需助记实体用自然编码） |
| 2026-08-10 | v1.8：Idempotency-Key 实现约定；filter DSL 固化（in/notIn 数组、between 日期边界、sort 单列） |
| 2026-08-10 | v1.9：列表查询传输约定——标准查询 POST /api/{resource}/search，GET keyword 仅作快捷搜索（用户裁决） |
| 2026-08-10 | v2.0：排序唯一性兜底——分页排序须确定性全序，服务端追加 id / createdAt+id 兜底；时间性列表默认 createdAt DESC |
| 2026-08-10 | v2.1：明确默认降序（时间性列表 createdAt DESC, id DESC）；用户排序后追加 id DESC 兜底；配套索引要求见后端工程规范 §3.3 |
| 2026-08-19 | 状态整理：导航同步第 2 批契约锁定及第 3 批实现结果；无契约内容变更 |
| 2026-08-19 | 第 4 批：入库单、收货、权限、打印、枚举和数据模型进入修订草案；待双向评审重新锁定 |
| 2026-08-19 | 通用规范状态澄清：2.5 保留服务端 i18n 目标，当前实现按 P-22 后置处理 |
| 2026-08-21 | 第 4 批契约锁定：前后端基于 `84c5c8a` 复核均无异议；导航同步为实现唯一基线 |
