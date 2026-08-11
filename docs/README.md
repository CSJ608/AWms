# 文档导航与管理规范

文档是 AI 的长期记忆载体。核心不是"写得规范"，而是**让 AI 能快速、准确读取和定位**。
本文件固定：标准文档放哪里、迭代/过程文档如何归档、写入纪律。

## 一、三类文档（生命周期不同）

| 类别 | 目录 | 内容 | 变更方式 |
|---|---|---|---|
| **长期标准**（固定位置，永不归档） | [product/](product/) | 愿景、角色卡、工作流 | 定稿后极少变；变更走评审 |
| | [design/](design/) | 界面规格：线框、状态矩阵、视觉规范 | 定稿后极少变；改界面先改规格 |
| | [concepts/](concepts/) | 概念设计（领域模型） | 动概念走 ADR |
| | [decisions/](decisions/) | 决策记录 ADR | 只增不改 |
| | [api/](api/) | 接口契约（前后端唯一依据） | 先改契约再改代码 |
| | [guides/](guides/) | 操作手册（部署/测试/联调） | 随环境演进 |
| | [methodology/](methodology/) | 规章制度 | 讨论定稿后极少变 |
| **迭代文档**（一批一份，验收后归档） | [iterations/active/](iterations/active/) | 当前批次：迭代计划 + 评审输入 + 详设 + 界面规格草稿 | 定稿后不改（除验收记录/回顾） |
| | [iterations/archive/](iterations/archive/) | 已归档批次（第X批-名称/ 子目录） | **只读追溯，不再修改** |
| **迭代文档**（验收后归档） | [reviews/](reviews/) | 本批评审记录：开工指令/评审意见/复验/抽查/修复指令 | 批次验收后整套移入 archive（见下） |
| | methodology/_handoff.md | 会话交接快照 | **不入 git** |

## 二、归档机制（强制）

### 静态 vs 动态

- **静态（长期标准，永不归档，只做文件内修订记录）**：product/、design/（当前定稿规格）、concepts/、decisions/、api/（契约）、guides/、methodology/规章制度、references/。
- **动态（批次内高频修改，批次验收后归档）**：iterations/active/、reviews/、review/<批次> 分支上的三方意见文档。

### 归档动作（审核者在新批次开工前执行一次）

1. 触发：批次《验收记录》填完且用户验收通过 → 该批动态文档整套移入 iterations/archive/<第X批-名称>/。
2. 范围：iterations/active/ 中该批全部文件；eviews/ 中该批全部文件（放入 rchive/<第X批-名称>/reviews/）；eview/<批次> 分支三方意见合并回 main 后的相关文件。
3. 动作：git mv（非复制）+ 文件头部加 > 状态：已归档（YYYY-MM-DD）——仅供追溯，不作为当前实现依据。
4. 纪律：**动态区 = 进行中缓冲区**——iterations/active/ 与 eviews/ 只允许存在当前批次/未决事项；新批次开始时上一批自动归档，避免过程文档无限累积。
5. _handoff.md 为滚动交接快照（不入 git），每次批次末就地更新，不归档。
6. **永不归档**：product/、design/（当前规格）、concepts/、decisions/、api/、guides/、methodology/、references/。
## 三、写入纪律（要点）

- **规格进标准文档，状态进工具**：契约/验收标准/架构/界面规格写进标准目录；"做到哪了"看 git log 和交接快照
- **"已完成"以 git 和测试为准**：文档里的状态会过期，过期即撒谎
- **先契约后代码**：接口改动先更新 api/ 契约，再改实现
- **先设计后实现**：页面改动先更新 design/ 界面规格，再改前端代码
- **新概念必给一句介绍**：避免读者/AI 遇到未定义术语
- **提交约定**：Conventional Commits + 中文描述；批次提交 `feat: 实现第X批 - <概要>`
