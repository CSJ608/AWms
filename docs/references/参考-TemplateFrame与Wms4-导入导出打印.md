# 参考：TemplateFrame 与 Wms4（导入/导出/打印时使用）

> 参考输入，不参与标准。**按我们自己的架构做，不机械照搬。**

## 何时参考

未来做**导入、导出、打印**相关设计/实现时。

## 参考来源

1. **TemplateFrame（引擎）**：C:\work\OpenCode\TemplateFrame
   - 模板⇄数据契约引擎：契约（TemplateContract）→ 版式（Builder）→ 填充（Fill）→ 回读（Parse）。
   - 支持 Word / Excel（src/TemplateFrame.Word、src/TemplateFrame.Excel）。
   - 开发单据模板先查 samples/TemplateFrame.Demo（送货单示例），再按 docs/DESIGN.md 与模板技能落地。
2. **Multiway.Logistics.Wms4（应用举例）**：C:\build\s\Multiway.Logistics.Wms4
   - 是 TemplateFrame 的应用示例，可查其中导入/导出/打印相关模块的用法。

## 使用原则

- 借鉴其"契约驱动模板、数据与版式分离"的思路，不照抄其代码结构。
- 我们自己的架构优先：导入导出走"平台级能力"（标准模板 + 两阶段校验 + 失败明细，见《调研-收货与PDA设计》第 5 节），打印走"模板化 + 业务模板服务"。
- 接入方式、依赖、目录结构均按 AWms 的概念设计与契约决定。
