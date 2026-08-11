/**
 * 运行时字段元数据（GET /api/meta/fields/{resource} 的 mock 定义）——
 * 依据契约各列表字段白名单 + 通用规范 2.10 操作符默认集。
 * 同时声明「固定参数映射」（契约各接口固定参数），供前端筛选区生成参数。
 */
import type { FieldMeta, FilterOp, SortSpec } from '../api/types'

const LABEL_TYPES = [
  { value: 'NONE', labelKey: 'enum.labelType.none' },
  { value: 'SKU', labelKey: 'enum.labelType.sku' },
  { value: 'UNIQUE', labelKey: 'enum.labelType.unique' },
]

const UOMS = [
  { value: 'CT', labelKey: 'enum.uom.CT' },
  { value: 'PC', labelKey: 'enum.uom.PC' },
  { value: 'BOX', labelKey: 'enum.uom.BOX' },
  { value: 'KG', labelKey: 'enum.uom.KG' },
  { value: 'G', labelKey: 'enum.uom.G' },
  { value: 'L', labelKey: 'enum.uom.L' },
  { value: 'M', labelKey: 'enum.uom.M' },
]

const STATUSES = [
  { value: 'ENABLED', labelKey: 'enum.status.enabled' },
  { value: 'DISABLED', labelKey: 'enum.status.disabled' },
]

export const MATERIAL_FIELDS: FieldMeta[] = [
  { field: 'code', labelKey: 'materials.code', type: 'string', operators: ['eq', 'contains', 'startsWith', 'in'] },
  { field: 'name', labelKey: 'materials.name', type: 'string', operators: ['eq', 'contains', 'startsWith', 'in'] },
  { field: 'searchCode', labelKey: 'materials.searchCode', type: 'string', operators: ['eq', 'contains'] },
  { field: 'batchControlled', labelKey: 'materials.batchControlled', type: 'bool', operators: ['eq'] },
  { field: 'labelType', labelKey: 'materials.labelType', type: 'enum', operators: ['eq', 'neq', 'in'], options: LABEL_TYPES },
  { field: 'defaultUom', labelKey: 'materials.defaultUom', type: 'enum', operators: ['eq', 'in'], options: UOMS },
  { field: 'defaultQtyPerLabel', labelKey: 'materials.defaultQtyPerLabel', type: 'decimal', operators: ['gt', 'gte', 'lt', 'lte', 'between'] },
  { field: 'status', labelKey: 'common.status', type: 'enum', operators: ['eq', 'neq', 'in'], options: STATUSES },
  { field: 'createdAt', labelKey: 'common.createdAt', type: 'datetime', operators: ['gt', 'gte', 'lt', 'lte', 'between'] },
  { field: 'updatedAt', labelKey: 'common.updatedAt', type: 'datetime', operators: ['gt', 'gte', 'lt', 'lte', 'between'] },
]

export const WAREHOUSE_FIELDS: FieldMeta[] = [
  { field: 'code', labelKey: 'warehouses.code', type: 'string', operators: ['eq', 'contains', 'startsWith'] },
  { field: 'name', labelKey: 'warehouses.name', type: 'string', operators: ['eq', 'contains'] },
  { field: 'searchCode', labelKey: 'warehouses.searchCode', type: 'string', operators: ['eq', 'contains'] },
  { field: 'status', labelKey: 'common.status', type: 'enum', operators: ['eq', 'neq', 'in'], options: STATUSES },
  { field: 'mgmtMode', labelKey: 'warehouses.mgmtMode', type: 'enum', operators: ['eq', 'in'], options: [
    { value: 'MANUAL', labelKey: 'enum.mgmtMode.manual' },
    { value: 'AGV', labelKey: 'enum.mgmtMode.agv' },
  ] },
  { field: 'createdAt', labelKey: 'common.createdAt', type: 'datetime', operators: ['gt', 'gte', 'lt', 'lte', 'between'] },
]

export const LOCATION_FIELDS: FieldMeta[] = [
  { field: 'code', labelKey: 'locations.code', type: 'string', operators: ['eq', 'contains', 'startsWith'] },
  { field: 'searchCode', labelKey: 'locations.searchCode', type: 'string', operators: ['eq', 'contains'] },
  { field: 'type', labelKey: 'locations.type', type: 'enum', operators: ['eq', 'neq', 'in'], options: [
    { value: 'STAGING', labelKey: 'enum.locationType.staging' },
    { value: 'DEFAULT', labelKey: 'enum.locationType.default' },
  ] },
  { field: 'status', labelKey: 'common.status', type: 'enum', operators: ['eq', 'neq', 'in'], options: STATUSES },
  { field: 'reachability', labelKey: 'locations.reachability', type: 'enum', operators: ['eq', 'in'], options: [
    { value: 'MANUAL_ONLY', labelKey: 'enum.reachability.manualOnly' },
    { value: 'AGV', labelKey: 'enum.reachability.agv' },
    { value: 'UNIVERSAL', labelKey: 'enum.reachability.universal' },
  ] },
  { field: 'createdAt', labelKey: 'common.createdAt', type: 'datetime', operators: ['gt', 'gte', 'lt', 'lte', 'between'] },
]

export const SOURCE_FIELDS: FieldMeta[] = [
  { field: 'type', labelKey: 'sources.type', type: 'enum', operators: ['eq', 'neq', 'in'], options: [
    { value: 'SUPPLIER', labelKey: 'enum.sourceType.supplier' },
    { value: 'WORKSHOP', labelKey: 'enum.sourceType.workshop' },
  ] },
  { field: 'code', labelKey: 'sources.code', type: 'string', operators: ['eq', 'contains', 'startsWith'] },
  { field: 'name', labelKey: 'sources.name', type: 'string', operators: ['eq', 'contains'] },
  { field: 'searchCode', labelKey: 'sources.searchCode', type: 'string', operators: ['eq', 'contains'] },
  { field: 'status', labelKey: 'common.status', type: 'enum', operators: ['eq', 'neq', 'in'], options: STATUSES },
  { field: 'createdAt', labelKey: 'common.createdAt', type: 'datetime', operators: ['gt', 'gte', 'lt', 'lte', 'between'] },
]

export const BATCH_FIELDS: FieldMeta[] = [
  { field: 'batchNo', labelKey: 'batches.batchNo', type: 'string', operators: ['eq', 'contains', 'startsWith'] },
  { field: 'materialId', labelKey: 'batches.material', type: 'ref', operators: ['eq', 'in'], refResource: 'materials' },
  { field: 'materialCode', labelKey: 'batches.materialCode', type: 'string', operators: ['eq', 'contains'] },
  { field: 'sourceBatchNo', labelKey: 'batches.sourceBatchNo', type: 'string', operators: ['eq', 'contains'] },
  { field: 'sourceType', labelKey: 'batches.sourceType', type: 'enum', operators: ['eq', 'in'], options: [
    { value: 'SUPPLIER', labelKey: 'enum.sourceType.supplier' },
    { value: 'WORKSHOP', labelKey: 'enum.sourceType.workshop' },
  ] },
  { field: 'sourceCode', labelKey: 'batches.sourceCode', type: 'string', operators: ['eq', 'contains'] },
  { field: 'productionDate', labelKey: 'batches.productionDate', type: 'date', operators: ['gt', 'gte', 'lt', 'lte', 'between'] },
  { field: 'expiryDate', labelKey: 'batches.expiryDate', type: 'date', operators: ['gt', 'gte', 'lt', 'lte', 'between'] },
  { field: 'status', labelKey: 'common.status', type: 'enum', operators: ['eq', 'neq', 'in'], options: [
    { value: 'ACTIVE', labelKey: 'enum.batchStatus.active' },
    { value: 'CLOSED', labelKey: 'enum.batchStatus.closed' },
  ] },
  { field: 'createdAt', labelKey: 'common.createdAt', type: 'datetime', operators: ['gt', 'gte', 'lt', 'lte', 'between'] },
]

export const RESOURCE_META: Record<string, FieldMeta[]> = {
  materials: MATERIAL_FIELDS,
  warehouses: WAREHOUSE_FIELDS,
  locations: LOCATION_FIELDS,
  sources: SOURCE_FIELDS,
  batches: BATCH_FIELDS,
}

/** 固定参数映射（契约各接口 search body 行声明）：参数名 → 默认操作符（前端据此生成快捷参数） */
export const RESOURCE_FIXED_PARAMS: Record<string, Array<{ param: string; op: FilterOp }>> = {
  materials: [
    { param: 'code', op: 'contains' },
    { param: 'name', op: 'contains' },
    { param: 'labelType', op: 'eq' },
    { param: 'status', op: 'eq' },
  ],
  warehouses: [
    { param: 'status', op: 'eq' },
  ],
  locations: [
    { param: 'type', op: 'eq' },
    { param: 'status', op: 'eq' },
  ],
  sources: [
    { param: 'type', op: 'eq' },
    { param: 'status', op: 'eq' },
  ],
  batches: [
    { param: 'materialId', op: 'eq' },
    { param: 'materialCode', op: 'contains' },
    { param: 'status', op: 'eq' },
  ],
}

/** 默认排序（通用规范 2.10 v2.1：主数据默认业务码 asc；时间性列表默认 createdAt DESC 最新在前） */
export const RESOURCE_DEFAULT_SORT: Record<string, SortSpec[]> = {
  materials: [{ field: 'code', dir: 'asc' }],
  warehouses: [{ field: 'code', dir: 'asc' }],
  locations: [{ field: 'code', dir: 'asc' }],
  sources: [{ field: 'code', dir: 'asc' }],
  batches: [{ field: 'createdAt', dir: 'desc' }],
}

/** keyword 模糊匹配字段集（契约：匹配 code/name/searchCode 等） */
export const RESOURCE_KEYWORD_FIELDS: Record<string, string[]> = {
  materials: ['code', 'name', 'searchCode'],
  warehouses: ['code', 'name', 'searchCode'],
  locations: ['code', 'searchCode'],
  sources: ['code', 'name', 'searchCode'],
  batches: ['batchNo', 'sourceBatchNo'],
}
