# AWms — 全新 WMS 仓库管理系统

一句话愿景：**为仓库现场作业人员打造一套"打开就知道下一步干什么、做错会拦住你、做完可追溯"的 WMS 操作台。**

- 全新设计，不迁移老系统，无历史兼容负担。
- 前后端由多个 AI 工具协作开发，人负责方向判断与评审门禁。

## 当前状态

- 第 1 批设计批、第 2 批契约批、第 3 批基础批均已验收归档。
- 已实现：登录、权限、菜单、主数据、查询平台、导入导出、CI/CD 与测试环境。
- 下一条产品工作流：第 4 批收货链（PDA 收货 → 质检 → 上架，含拍照与打印）。
- 当前没有进行中的功能批次；开工前应先在 `docs/iterations/active/` 建立第 4 批计划并完成界面/契约复核。

## 仓库结构

| 目录 | 内容 |
|---|---|
| [docs/](docs/) | 文档唯一区：标准文档固定位置；迭代/过程文档归档隔离（规范见 [docs/README.md](docs/README.md)） |
| [docs/methodology/](docs/methodology/) | 规章制度（全项目最重要的约定，先读） |
| [docs/product/](docs/product/) | 产品层：愿景、角色、工作流（长期标准） |
| [docs/design/](docs/design/) | 界面规格：页面线框、状态矩阵、视觉规范（长期标准） |
| [docs/concepts/](docs/concepts/) | 概念设计（长期标准） |
| [docs/decisions/](docs/decisions/) | 决策记录 ADR（长期标准，只增不改） |
| [docs/api/](docs/api/) | 接口契约（长期标准，前后端唯一依据） |
| [docs/guides/](docs/guides/) | 操作手册（长期标准） |
| [docs/iterations/](docs/iterations/) | 迭代文档：active 当前批次 / archive 已归档 |
| [docs/reviews/](docs/reviews/) | 评审记录（过程快照，按日期归档） |
| [docs/references/](docs/references/) | 参考输入（来自旧项目，不参与标准） |
| [backend/](backend/) | .NET 10 / ASP.NET Core 后端、领域模型、迁移与测试 |
| [frontend/](frontend/) | React 19 Web/PDA 前端、MSW mock 与组件测试 |

## 开始阅读

1. [AGENTS.md](AGENTS.md) —— AI 的地图（每个会话自动加载）
2. [docs/README.md](docs/README.md) —— 文档分类与归档规范
3. [规章制度](docs/methodology/规章制度.md) —— 全项目最重要的约定
4. [愿景 v1.0](docs/product/vision.md) —— 已定稿的产品方向与边界
5. [路线图](docs/product/roadmap.md) —— 已完成里程碑与下一批方向
