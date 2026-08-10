# ADR-006 主键采用 UUID v7（时间有序）

> 状态：**草案（2026-08-10，用户提出，待确认转正）**。
> 关联：ADR-004（技术栈）、docs/guides/后端工程规范.md §3、通用规范 2.3（ID UUID）。

## 背景

- 项目约定主键为 UUID（通用规范 2.3）。纯随机 UUID（v4，`Guid.NewGuid()`/`gen_random_uuid()`）作为主键时，插入位置随机，B-tree 索引产生大量页分裂与碎片，缓存局部性差；大表下写入与查询性能明显劣于有序主键（Supabase/PostgreSQL 官方实践均建议避免随机 UUID 主键）。
- 时间有序 UUID v7（RFC 9562）：48 位毫秒时间戳 + 版本/变体位 + 74 位随机位；插入近似顺序，索引友好，同时保留 UUID 的全局唯一、不可枚举业务号之外的特性。

## 现状（2026-08-10 核实）

- 后端为 .NET 10 + `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3`。Npgsql EF 提供程序 **9.0 起默认对 Guid 主键客户端生成 UUID v7**（替代 v4），10.x 延续该行为。
- 实体 `Id` 属性无初始化器（默认 `Guid.Empty`），即走 EF 值生成 → **当前实现实际上已经是 v7**（只要不手动 `Guid.NewGuid()`）。
- PostgreSQL 18 起内置 `uuidv7()` / `uuid_extract_timestamp()`，可做数据库侧默认值；PG<18 需扩展（pg_uuidv7）或客户端生成。

## 决策

1. 主键继续用 `Guid`（PG 类型 `uuid`），**值生成交给 EF/Npgsql 默认（UUID v7）**；代码中禁止对主键手动 `Guid.NewGuid()`（会退回 v4），需要显式生成时用 `Guid.CreateVersion7()`（.NET 9+ 内置）。
2. 默认采用**客户端生成 v7**（Npgsql 默认行为），不依赖数据库版本/扩展；若部署 PostgreSQL ≥18，允许个别表用 `HasDefaultValueSql("uuidv7()")` 作为数据库侧兜底，二者同版兼容。
3. 业务可见编号（物料编码/批次号/单据号等）仍由编号服务生成（通用规范 2.9），UUID 仅作内部主键，不暴露给用户 → 不引入 v7 时间戳可枚举性风险。
4. 集成测试（Testcontainers PostgreSQL）不依赖数据库生成 UUID，任意 PG 16/17/18 均可。

## 影响

- 正面：索引局部性好，减少页分裂/碎片；迁移零改动（列类型不变，仍为 uuid）；客户端生成对测试友好。
- 注意：v7 顺序性是"近似有序"，同一毫秒内大量插入时随机位保证唯一但顺序非严格单调；对 B-tree 索引已足够。
- 注意：EF InMemory 测试提供程序仍生成 v4（单元测试无索引问题，不影响）。

## 备选（不选）

- 自增 bigint：暴露业务量、分布式/导入冲突风险，违背"不暴露内部 id"的既有约定。
- ULID：.NET 9 已有 `Guid.CreateVersion7()`，无需第三方库。