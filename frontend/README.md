# AWms 前端

Web 后台 + PDA 双路由树同库前端。当前已完成基础平台和主数据页面；PDA 作业流程待第 4 批实现。

## 技术栈

React 19 · Vite 8 · TypeScript 6 strict · Tailwind v4 · shadcn/ui（radix-nova）·
TanStack Query/Table 8 · react-hook-form + zod 4 · i18next · Vitest + RTL · MSW · pnpm

## 常用命令

```bash
pnpm dev    # 开发（默认 MSW mock，VITE_USE_MOCK=false 切真实后端）
pnpm build  # tsc -b 严格类型检查 + 生产构建（含 PWA SW，不缓存 API）
pnpm lint   # oxlint 0 error
pnpm test   # 运行 Vitest 全套测试（MSW node server，同一套契约 handlers）
```

## 目录

- `src/api/` —— 契约 DTO（types.ts，唯一事实来源）+ 类型化端点 + fetch 客户端（envelope/Bearer/401 刷新/幂等键）
- `src/mocks/` —— MSW：seed 种子数据、db 内存库（业务规则：唯一性/引用保护/filter DSL/分页）、handlers（严格按契约）
- `src/platform/` —— 平台能力：SearchField/DataTable/ReferencePicker/导入导出/权限/路由注册表/401 单飞刷新
- `src/modules/web/` —— Web 页面（登录 + 主数据：物料/仓库/库位/来源/批次）
- `src/modules/pda/` —— PDA 占位（第 4 批）
- `src/i18n/` —— 中英文案（语义 key，zh/en 结构一致）

## 联调说明

- mock 开关：`VITE_USE_MOCK=false`（见 `.env.example`）；生产构建不启用 MSW，天然连真实后端
- 契约依据：`docs/api/`（docs/ 只读）；发现契约问题走问题清单 → 协调者裁决
