/**
 * 引用资源配置 —— 通用 ReferencePicker 的目标资源（通用规范 2.10 ref 交互模式）。
 * 每个资源：轻量 keyword 搜索 + 完整列表接口 + 展示文案 + 单选回显策略。
 */
import {
  apiGetBatch, apiListBatches, apiListMaterials, apiListSources, apiListWarehouses,
  apiQuickSearchBatches, apiQuickSearchMaterials, apiQuickSearchSources, apiQuickSearchWarehouses,
} from '../../api'
import type { BatchItem, ListQuery, MaterialItem, PageResult, SourceItem, WarehouseItem } from '../../api/types'

export interface RefResource<T> {
  resource: string
  /** 轻量搜索：keyword + pageSize=10（快捷搜索） */
  quickSearch: (keyword: string) => Promise<T[]>
  /** 完整列表（弹窗：分页/筛选/排序复用字段元数据与 filter DSL） */
  listQuery: (q: ListQuery) => Promise<PageResult<T>>
  /** 候选/已选展示文案 */
  display: (item: T) => string
  /** 单选回显：按 id 取展示（契约无 by-id 端点时用列表扫描） */
  lookupById: (id: string) => Promise<T | null>
}

function listScan<T extends { id: string }>(listFn: (q: ListQuery) => Promise<PageResult<T>>) {
  return async (id: string): Promise<T | null> => {
    const res = await listFn({ page: 1, pageSize: 100 })
    return res.items.find((i) => i.id === id) ?? null
  }
}

export const REF_RESOURCES: Record<string, RefResource<unknown>> = {
  materials: {
    resource: 'materials',
    quickSearch: async (keyword) => (await apiQuickSearchMaterials(keyword)).items,
    listQuery: (q) => apiListMaterials(q),
    display: (m) => `${(m as MaterialItem).code} ${(m as MaterialItem).name}`,
    lookupById: listScan(apiListMaterials),
  },
  warehouses: {
    resource: 'warehouses',
    quickSearch: async (keyword) => (await apiQuickSearchWarehouses(keyword)).items,
    listQuery: (q) => apiListWarehouses(q),
    display: (w) => `${(w as WarehouseItem).code} ${(w as WarehouseItem).name}`,
    lookupById: listScan(apiListWarehouses),
  },
  sources: {
    resource: 'sources',
    quickSearch: async (keyword) => (await apiQuickSearchSources(keyword)).items,
    listQuery: (q) => apiListSources(q),
    display: (s) => `${(s as SourceItem).code} ${(s as SourceItem).name}`,
    lookupById: listScan(apiListSources),
  },
  batches: {
    resource: 'batches',
    quickSearch: async (keyword) => (await apiQuickSearchBatches(keyword)).items,
    listQuery: (q) => apiListBatches(q),
    display: (b) => `${(b as BatchItem).batchNo} ${(b as BatchItem).materialCode}`,
    lookupById: async (id) => apiGetBatch(id),
  },
}
