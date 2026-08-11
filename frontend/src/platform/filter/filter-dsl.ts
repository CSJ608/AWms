/**
 * 筛选查询构建 —— SearchField 值 → 固定参数 + filter DSL + sort（通用规范 2.10）。
 * 规则：字段声明了固定参数且操作符匹配 → 发固定参数（快捷/索引友好）；
 * 其余条件 → filter DSL；空值不发送。
 */
import type { FieldMeta, FilterDsl, FilterOp, ListQuery, SortSpec } from '../../api/types'
import { RESOURCE_FIXED_PARAMS } from '../../mocks/meta'

/** 单个筛选条件（UI 态） */
export interface SearchCondition {
  op: FilterOp
  /** between 用双元素数组；isNull/isNotNull 忽略 */
  value: string | string[] | boolean | null
}

export type SearchValues = Record<string, SearchCondition>

/** 类型默认操作符（通用规范 2.10 默认集，取常用值） */
export const DEFAULT_OP: Record<FieldMeta['type'], FilterOp> = {
  string: 'contains',
  number: 'eq',
  decimal: 'eq',
  date: 'gte',
  datetime: 'gte',
  bool: 'eq',
  enum: 'eq',
  uuid: 'eq',
  ref: 'eq',
}

/** 条件是否为空（不参与查询） */
export function isEmptyCondition(c: SearchCondition | undefined): boolean {
  if (!c) return true
  if (c.op === 'isNull' || c.op === 'isNotNull') return false
  if (Array.isArray(c.value)) return c.value.length === 0 || c.value.every((v) => v === '')
  if (c.value === null || c.value === undefined) return true
  return String(c.value).trim() === ''
}

/** 构建 filter DSL 条件（含类型化 value） */
export function toDslCondition(meta: FieldMeta, c: SearchCondition): { field: string; op: FilterOp; value?: string | string[] | boolean | null } {
  if (c.op === 'isNull' || c.op === 'isNotNull') return { field: meta.field, op: c.op }
  return { field: meta.field, op: c.op, value: c.value }
}

export interface BuildQueryArgs {
  resource: string
  values: SearchValues
  fields: FieldMeta[]
  keyword?: string
  page?: number
  pageSize?: number
  sort?: SortSpec[]
}

/** SearchValues → ListQuery（固定参数 + filter DSL + sort） */
export function buildListQuery(args: BuildQueryArgs): ListQuery {
  const { resource, values, fields, keyword, page, pageSize, sort } = args
  const fixedParams = RESOURCE_FIXED_PARAMS[resource] ?? []
  const conditions: NonNullable<FilterDsl['conditions']> = []
  const fixed: Record<string, string> = {}
  const query: ListQuery = {}

  for (const meta of fields) {
    const c = values[meta.field]
    if (isEmptyCondition(c)) continue
    const fixedDef = fixedParams.find((f) => f.param === meta.field)
    const isFixedMatch = fixedDef && c!.op === fixedDef.op && !Array.isArray(c!.value) && typeof c!.value !== 'boolean'
    if (isFixedMatch) {
      fixed[meta.field] = String(c!.value)
    } else {
      conditions.push(toDslCondition(meta, c!))
    }
  }

  if (keyword) query.keyword = keyword
  if (Object.keys(fixed).length > 0) Object.assign(query, fixed)
  if (conditions.length > 0) query.filter = { op: 'and', conditions }
  if (sort && sort.length > 0) query.sort = sort
  if (page !== undefined) query.page = page
  if (pageSize !== undefined) query.pageSize = pageSize
  return query
}

/** 排序切换（表头点击：asc → desc → 取消） */
export function toggleSort(current: SortSpec[] | undefined, field: string): SortSpec[] {
  const existing = current?.find((s) => s.field === field)
  if (!existing) return [{ field, dir: 'asc' }]
  if (existing.dir === 'asc') return [{ field, dir: 'desc' }]
  return []
}
