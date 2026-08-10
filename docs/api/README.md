# API 契约导航与通用规范（已定稿 v1.3）

> 状态：**已定稿 v1.6（2026-08-10，用户确认）**。前后端唯一依据；变更走评审（先改契约再改实现）。

## 一、导航（docs/api/）

| 文件 | 内容 | 状态 |
|---|---|---|
| README.md（本文件） | 导航 + 通用规范（含编号规范 2.9） | ✅ 定稿 v1.3 |
| 枚举与错误码.md | 全部枚举与错误码 | ✅ 定稿 v1.1 |
| 物料.md / 仓库库位.md / 来源.md | 主数据 | 🟡 草案 v0.1 |
| 批次.md | 主数据（系统自动建批次） | 🟡 草案 v0.3 |
| 入库单.md / 收货.md | 入库链 | 待起草 |
| 库存.md / 工作台.md | 库存与聚合 | 待起草 |
| 标签解析.md / 附件.md / 打印.md / 导入导出.md | 支撑能力 | 待起草 |
| 数据模型-草案.md | 概念级数据模型（C-02 依据） | 草案 v0.3 |

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
- 错误文案归后端（messages.zh/en.json，未知语言回退中文）；前端只维护 UI 文案；两端不共享 i18n 文件（本期）。

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

**排序（sort 白名单）**：

- 每个接口声明**可排序字段白名单**（如 materialCode / totalQty / status / updatedAt）；
- 请求：`sort=[{"field":"totalQty","dir":"desc"}]`；field 必须在白名单内、dir ∈ asc/desc，否则 400；
- 默认排序由各接口定义（如 occurredAt desc）。

**前端**：筛选 UI 由 `SearchField[]` 元数据驱动（字段/控件类型/选项源/操作符），映射为固定参数或 filter DSL。

**本期实现范围**：通用 QueryParser（字段白名单 + 操作符白名单 + 参数化）+ 运行时元数据端点（GET /api/meta/fields/{resource}）作为平台能力，主要列表（物料/库存/流水/入库单）启用；其余接口按需启用。

**字段元数据（FieldMeta，2026-08-10 确认）**：

- 每个列表接口声明可筛选字段的元数据：`field / labelKey / type / operators / options?`。
- `type`：string / number / decimal / date / datetime / bool / enum / uuid / ref（ref 带 `refResource`，如 materials/warehouses）。
- `options`：枚举字段提供 `[{ value, labelKey }]`；ref 字段由引用资源提供选项。
- 操作符按类型给默认集（string: eq/contains/startsWith/in；number/decimal: 比较+between；enum: eq/in/neq；date/datetime: 比较+between；bool: eq；uuid/ref: eq/in）。
- **前端控件由 type 推导**：string→文本框；enum→下拉；bool→开关；number/decimal→数字输入（可区间）；date/datetime→日期选择器（可区间）；ref→引用选择器。
- **来源**：① 契约文档内定义（各接口字段元数据表）；② **运行时元数据端点 `GET /api/meta/fields/{resource}`**（推荐本期实现，返回 FieldMeta[]，前端动态渲染筛选区，新增字段自动生效）。



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
