> 状态：已归档（2026-08-22）——仅供追溯，不作为当前实现依据

# 第 4 批：收货链归档索引

## 批次结果

第 4 批已交付 Web 入库单创建、详情、打印、收货记录与异常处理，以及 PDA 收货、质检、异常上报和上架闭环；真实 Web/API/PostgreSQL/附件存储联调通过，用户验收通过后经 PR #13 合并 `main`。

## 归档内容

| 目录或文件 | 内容 |
|---|---|
| [迭代计划](迭代计划-第4批-收货链.md) | 批次目标、契约裁决、任务范围、验收场景与风险控制 |
| [reviews/](reviews/) | 开工评审、视觉与工作标签评审、契约锁定、前后端完工审核 |
| [交付/backend/](交付/backend/) | 后端完工汇报 |
| [交付/frontend/](交付/frontend/) | 前端完工汇报 |
| [验收/](验收/) | 真实 UI 联调报告、用户验收与发布记录 |

## 关键证据

- Integration HEAD：`91308f5aa47be8ee1ae93ad4c0427bfffcd88388`
- 修复复测提交：`ddfcf80668e486ee302c1a32e3ec40fd05a8e25c`
- Integration -> Main PR：https://github.com/CSJ608/AWms/pull/13
- Main 合并提交：`81d534f072e35a03083fc9d2fe25503fba956517`
- Main CI/CD 与测试环境部署：https://github.com/CSJ608/AWms/actions/runs/32569822422

## 说明

- `active/` 与 `docs/reviews/` 已清空，仅保留 `.gitkeep`。
- 当前实现依据以 `docs/api/`、`docs/design/`、`docs/product/` 和代码为准。
- 生产/失效日期未落库、PC 全局布局、全局标签页、多语言与开发规范列入后续迭代输入，不阻塞本批归档。