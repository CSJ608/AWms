# 文档导航与管理规范

文档是 AI 的长期记忆载体。核心不是“写得规范”，而是让参与者能快速、准确地定位当前事实。

## 一、文档分类

| 类别 | 目录 | 内容 | 变更方式 |
|---|---|---|---|
| 长期标准 | [product/](product/) | 愿景、角色、工作流、路线图、后置项 | 定稿后少改；变更走评审 |
| | [design/](design/) | 界面规格、状态矩阵、视觉与标签规范 | 改界面先改规格 |
| | [concepts/](concepts/) | 领域概念设计 | 重大变更走 ADR |
| | [decisions/](decisions/) | ADR | 只增补，不覆盖原始决策 |
| | [api/](api/) | 前后端接口契约 | 先改契约再改代码 |
| | [guides/](guides/) | 工程、测试、部署和联调手册 | 随环境演进 |
| | [methodology/](methodology/) | 项目规章制度 | 讨论定稿后少改 |
| 当前过程 | [iterations/active/](iterations/active/) | 当前批次计划、任务、问题和验收记录 | 批次内维护，验收后归档 |
| | [reviews/](reviews/) | 当前批次评审、复验和联调记录 | 批次内维护，验收后归档 |
| 历史追溯 | [iterations/archive/](iterations/archive/) | 已验收批次的计划、评审、交付和验收证据 | 只读，不再作为当前实现依据 |
| 会话状态 | methodology/_handoff.md | 当前状态与下一步快照 | 不入 Git，滚动覆盖 |
| 参考输入 | [references/](references/) | 旧项目资料和外部调研 | 仅供参考，不作为标准 |

## 二、归档机制

### 静态与动态

- 静态标准：product/、design/、concepts/、decisions/、api/、guides/、methodology/、references/。固定位置，不随批次归档。
- 动态过程：iterations/active/、reviews/、review/<批次> 分支上的过程材料，以及实现工作区产生的任务清单、问题清单和完工汇报。

### 批次归档动作

1. 触发条件：批次验收记录完成且用户验收通过。
2. 将 iterations/active/ 和 reviews/ 中的本批文件移动到 `iterations/archive/<第X批-名称>/`。
3. 实现工作区产生的任务清单、问题清单和完工汇报归入批次目录的 `交付/backend/` 或 `交付/frontend/`。
4. 归档文件注明：`状态：已归档（YYYY-MM-DD）——仅供追溯，不作为当前实现依据`。
5. active/ 与 reviews/ 只允许保留当前批次或未决事项；没有进行中批次时仅保留 `.gitkeep`。
6. `_handoff.md` 就地更新，不归档、不入 Git。

## 三、写入纪律

- 规格进标准文档，过程状态进 active/reviews 和交接快照。
- “已完成”以 Git、测试和可复核证据为准，不以过时状态文字为准。
- 接口改动先更新 api/ 契约，再改实现。
- 页面改动先更新 design/ 界面规格，再改前端代码。
- 新概念必须给出一句定义，避免未定义术语进入实现。
- 已归档文档不再修改；标准文档发生修订时保留修订记录。
- 提交遵循 Conventional Commits + 中文描述；批次提交使用 `feat: 实现第X批 - <概要>`。
