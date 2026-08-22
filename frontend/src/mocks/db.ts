/**
 * MSW mock 内存库 —— 业务规则与契约一致（mock 必须连业务规则一起模拟）：
 * 唯一性（*_CODE_DUPLICATED）、引用保护（*_IN_USE）、keyword 匹配、filter DSL + sort 白名单、分页。
 */
import type {
  ApiError, FilterDsl, FilterGroup, PageResult, SortSpec,
} from '../api/types'
import { ApiErrorImpl } from '../api/client'
import {
  MOCK_IDS, seedAttachments, seedBatches, seedInboundOrders, seedLocations, seedMaterials, seedPrintJobs,
  seedQualityChecks, seedReceipts, seedSources, seedUsers, seedWarehouses,
} from './seed'

export interface MockDb {
  materials: typeof seedMaterials
  warehouses: typeof seedWarehouses
  locations: typeof seedLocations
  sources: typeof seedSources
  batches: typeof seedBatches
  inboundOrders: typeof seedInboundOrders
  receipts: typeof seedReceipts
  qualityChecks: typeof seedQualityChecks
  attachments: typeof seedAttachments
  printJobs: typeof seedPrintJobs
  putawayVersions: Record<string, number>
}

export function createDb(): MockDb {
  return {
    materials: structuredClone(seedMaterials),
    warehouses: structuredClone(seedWarehouses),
    locations: structuredClone(seedLocations),
    sources: structuredClone(seedSources),
    batches: structuredClone(seedBatches),
    inboundOrders: structuredClone(seedInboundOrders),
    receipts: structuredClone(seedReceipts),
    qualityChecks: structuredClone(seedQualityChecks),
    attachments: structuredClone(seedAttachments),
    printJobs: structuredClone(seedPrintJobs),
    putawayVersions: { [MOCK_IDS.receiptLine2]: 3 },
  }
}

export const db = createDb()

export const err = (code: string, message: string, status = 400): ApiError =>
  new ApiErrorImpl(code, message, status)

// ── 通用查询（keyword + 固定参数 + filter DSL + sort + 分页）──

type Row = Record<string, unknown>

/** 值比较（filter DSL 语义，通用规范 2.10） */
function evalCondition(row: Row, cond: { field: string; op: string; value?: unknown }): boolean {
  const v = row[cond.field]
  switch (cond.op) {
    case 'eq': return v === cond.value
    case 'neq': return v !== cond.value
    case 'contains': return typeof v === 'string' && typeof cond.value === 'string' && v.toLowerCase().includes(cond.value.toLowerCase())
    case 'startsWith': return typeof v === 'string' && typeof cond.value === 'string' && v.toLowerCase().startsWith(cond.value.toLowerCase())
    case 'in': return Array.isArray(cond.value) && cond.value.includes(v)
    case 'notIn': return Array.isArray(cond.value) && !cond.value.includes(v)
    case 'gt': return v != null && (v as number) > (cond.value as number)
    case 'gte': return v != null && (v as number) >= (cond.value as number)
    case 'lt': return v != null && (v as number) < (cond.value as number)
    case 'lte': return v != null && (v as number) <= (cond.value as number)
    case 'between': {
      const [lo, hi] = cond.value as [string | number, string | number]
      // between 对纯日期字段按“当日 00:00 ~ 次日 00:00”处理（含当日全天）
      if (typeof v === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(String(lo))) {
        const vDay = v.slice(0, 10)
        return vDay >= String(lo) && vDay <= String(hi)
      }
      return v != null && v >= lo && v <= hi
    }
    case 'isNull': return v === null || v === undefined
    case 'isNotNull': return v !== null && v !== undefined
    default: return false
  }
}

function evalGroup(row: Row, group: FilterGroup): boolean {
  return group.op === 'and'
    ? group.conditions.every((c) => ('conditions' in c ? evalGroup(row, c) : evalCondition(row, c)))
    : group.conditions.some((c) => ('conditions' in c ? evalGroup(row, c) : evalCondition(row, c)))
}

export interface QueryOptions {
  keyword?: string
  /** 固定参数（契约声明的 eq/contains 快捷参数） */
  fixed?: Record<string, string | boolean | number | undefined>
  /** 固定参数中包含模糊匹配的字段（用于 keyword 匹配 code/name/searchCode） */
  fuzzyFields?: string[]
  filter?: FilterDsl
  sort?: SortSpec[]
  page?: number
  pageSize?: number
}

/** 通用列表查询：筛选 → 排序 → 分页（契约：筛选空值不发送、pageSize=0 全量） */
export function queryList<T>(rows: T[], opts: QueryOptions): PageResult<T> {
  const source = rows as Row[]
  let list = source

  if (opts.keyword) {
    const kw = opts.keyword.toLowerCase()
    const fuzzy = opts.fuzzyFields ?? []
    list = list.filter((row) => fuzzy.some((f) => {
      const v = row[f]
      return typeof v === 'string' && v.toLowerCase().includes(kw)
    }))
  }

  if (opts.fixed) {
    for (const [k, v] of Object.entries(opts.fixed)) {
      if (v === undefined || v === null || v === '') continue
      list = list.filter((row) => {
        const rv = row[k]
        if (typeof v === 'string' && typeof rv === 'string') return rv.toLowerCase().includes(v.toLowerCase())
        return rv === v
      })
    }
  }

  const filter = opts.filter
  if (filter) list = list.filter((row) => evalGroup(row, filter))

  if (opts.sort && opts.sort.length > 0) {
    const { field, dir } = opts.sort[0] // 本期单列排序（通用规范 2.10 固化）
    list = [...list].sort((a, b) => {
      const av = a[field]
      const bv = b[field]
      if (av == null && bv == null) return 0
      if (av == null) return 1
      if (bv == null) return -1
      const cmp = typeof av === 'string' && typeof bv === 'string'
        ? av.localeCompare(bv, 'zh-CN')
        : av < bv ? -1 : av > bv ? 1 : 0
      return dir === 'desc' ? -cmp : cmp
    })
  }

  const total = list.length
  if (!opts.pageSize || opts.pageSize === 0) {
    // pageSize=0 全量
    return { items: list as T[], total, page: opts.page ?? 1, pageSize: 0 }
  }
  const page = opts.page ?? 1
  const pageSize = opts.pageSize
  const start = (page - 1) * pageSize
  return { items: list.slice(start, start + pageSize) as T[], total, page, pageSize }
}

// ── 通用字段操作 ───────────────────────────────────────

export function getByCode<T>(rows: T[], code: string): T | undefined {
  return (rows as Row[]).find((r) => r.code === code) as T | undefined
}

export function getById<T>(rows: T[], id: string): T | undefined {
  return (rows as Row[]).find((r) => r.id === id) as T | undefined
}

export function newId(_prefix?: string): string {
  return crypto.randomUUID()
}

export const nowIso = () => new Date().toISOString()

// ── 认证 ──────────────────────────────────────────────

export function findUser(username: string) {
  return seedUsers.find((u) => u.username === username)
}
