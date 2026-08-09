# AGENTS.md — AWms 的 AI 地图

本文件是给所有 AI 工具的**地图**，每次会话自动加载。先读它，再决定看哪些文档。

## 项目是什么

全新设计的 WMS（仓库管理系统），非迁移老系统。产品层（愿景/角色/工作流）驱动工程层（界面/契约/实现）。
前端与后端由 AI 协作开发；人负责方向判断与评审门禁。

## 必读文档（按顺序）

1. [docs/README.md](docs/README.md) —— 文档分类与归档规范
2. [docs/methodology/规章制度.md](docs/methodology/规章制度.md) —— **全项目最重要的约定**：五阶段模型、文档纪律、迭代节奏、评审门禁、AI 分工
3. [docs/product/vision.md](docs/product/vision.md) —— 愿景（若已定稿，先读它再做事）

## 会话开始做什么

1. 读交接快照 [docs/methodology/_handoff.md](docs/methodology/_handoff.md)（若有）：定位上次进度、下一步
2. 读当前批次 [docs/iterations/active/](docs/iterations/active/)：看本批计划与验收标准
3. 涉及产品层读 [docs/product/](docs/product/)；涉及界面读 [docs/design/](docs/design/)；涉及概念读 [docs/concepts/](docs/concepts/)；涉及接口读 [docs/api/](docs/api/)

## 关键约定（摘要，详见规章制度）

- **标准文档固定位置，迭代/过程文档归档隔离**：产品、概念、契约、界面规格写进对应标准目录；"做到哪了"看 git 与交接快照，不写进标准文档
- **愿景先行，产品层驱动工程层**：页面由"角色的工作流"派生，不由 API/模块清单派生
- **先设计后实现**：界面规格定稿并评审通过，前端才允许写代码
- **契约先行**：前后端唯一依据是 docs/api/ 契约
- **每批 = 一条可演示的工作流**，批内先设计后实现，批末演示验收
- **"已完成"以 git 和测试为准**，不以文档为准
- **提交约定**：Conventional Commits + 中文描述
- 参考旧项目 MWms 的资料在 [docs/references/](docs/references/)，只作参考，不作标准
