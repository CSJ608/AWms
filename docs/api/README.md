# API 契约导航与通用规范（已定稿 v1.2）

> 状态：**已定稿 v1.2（2026-08-10，用户确认）**。前后端唯一依据；变更走评审（先改契约再改实现）。

## 一、导航（docs/api/）

| 文件 | 内容 | 状态 |
|---|---|---|
| README.md（本文件） | 导航 + 通用规范（含编号规范 2.9） | ✅ 定稿 v1.2 |
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

### 2.9 编号规范（编号服务，2026-08-10 确认）

**规则注册属性**（每种编号注册一条规则）：

| 属性 | 说明 | 示例 |
|---|---|---|
| type | 编号类型（注册键） | INBOUND_ORDER / RECEIPT / BATCH / TXN_GROUP / UNIQUE_CODE / IMPORT_TASK |
| scopeKey | 作用域键 | GLOBAL / MATERIAL / 动态参数（如单据类型 ty） |
| prefix | 前缀（可空、可动态） | "PO" / "RCP" / 空 |
| dateFormat | 日期格式（可空） | yyyyMMdd / yyMMdd / 空 |
| seqLength | 序号长度（补零） | 4 / 3 |
| resetPeriod | 重置周期 | DAILY / MONTHLY / NEVER |
| onExhaustion | 序号耗尽行为 | THROW（抛 NUMBER_EXHAUSTED）/ WRAP（默认禁用） |

**本期注册表**：

| type | scopeKey | prefix | dateFormat | seqLen | 示例 |
|---|---|---|---|---|---|
| INBOUND_ORDER | 按 ty 动态 | PO/PR/OT | yyyyMMdd | 4 | PO-20260810-0001 |
| RECEIPT | GLOBAL | RCP | yyyyMMdd | 4 | RCP-20260810-0001 |
| BATCH | MATERIAL | 无 | yyMMdd | 3 | 260810001 |
| TXN_GROUP | GLOBAL | TXN | yyyyMMdd | 4 | TXN-20260810-0001 |
| UNIQUE_CODE | GLOBAL | BOX | yyyyMMdd | 4 | BOX-20260810-0001 |
| IMPORT_TASK | GLOBAL | IMP | yyyyMMdd | 4 | IMP-20260810-0001 |

**实现**：序号表 Sequence(type, scopeKey, bizDate, lastNo)，唯一(type, scopeKey, bizDate)，原子自增（UPDATE...RETURNING，行锁）；规则在代码注册。
**生成时机**：与业务同一事务；回滚跳号允许（保证唯一与单调）。
**耗尽**：序号超过 seqLength 上限 → NUMBER_EXHAUSTED（0x000007）。
**唯一性**：对外编号在"作用域内"唯一；**批次号按物料+天唯一（不同物料可同号，永远与物料一起展示/扫码，无歧义）**；UUID 仅内部主键；前端不生成编号。

## 三、修订记录

| 日期 | 变更 |
|---|---|
| 2026-08-10 | v0.1 初始草案 |
| 2026-08-10 | v0.2 错误模型修订 + 后端 i18n 独立 + hex |
| 2026-08-10 | v0.3 HTTP 状态码理由/数量示例/静默动作 |
| 2026-08-10 | v1.0 定稿：code=判断钥匙 |
| 2026-08-10 | v1.1：编号规范 2.9 |
| 2026-08-10 | v1.2：编号规则注册属性（scopeKey/prefix/dateFormat/seqLen/reset/耗尽）；批次号 YYMMDD+3 位按物料+天 |
