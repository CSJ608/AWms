/**
 * mock 数据层业务规则守门（后端行为契约）：
 * 唯一性（*_CODE_DUPLICATED）、引用保护（*_IN_USE）、keyword/filter DSL/sort/分页、
 * 元数据端点。走真实 client + MSW（严格按契约 DTO）。
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  apiCreateLocation, apiCreateMaterial, apiCreateWarehouse, apiDeleteMaterial, apiDeleteWarehouse,
  apiListBatches, apiListLocations, apiListMaterials, apiMetaFields, apiQuickSearchMaterials,
} from '../api'
import { seedSession } from '../test/utils'
import { MOCK_IDS } from './seed'

beforeEach(() => {
  seedSession()
})

describe('mock 数据层：主数据业务规则', () => {
  it('新建物料成功（201 返回完整 DTO）', async () => {
    const item = await apiCreateMaterial(
      { code: 'MAT-999', name: '测试物料', searchCode: 'CSWL', batchControlled: false, labelType: 'NONE', defaultUom: 'PC', status: 'ENABLED' },
      'key-1',
    )
    expect(item.code).toBe('MAT-999')
    expect(item.status).toBe('ENABLED')
    expect(item.defaultQtyPerLabel).toBeNull()
  })

  it('重复编码 → MATERIAL_CODE_DUPLICATED（409）', async () => {
    await expect(
      apiCreateMaterial({ code: 'MAT-001', name: '重复', batchControlled: false, labelType: 'NONE', defaultUom: 'PC' }, 'key-2'),
    ).rejects.toMatchObject({ code: 'MATERIAL_CODE_DUPLICATED', status: 409 })
  })

  it('删除被批次引用的物料 → MATERIAL_IN_USE（引用保护）', async () => {
    await expect(apiDeleteMaterial(MOCK_IDS.material1)).rejects.toMatchObject({ code: 'MATERIAL_IN_USE', status: 409 })
  })

  it('删除未被引用物料 → 204 且列表不再返回', async () => {
    await apiDeleteMaterial(MOCK_IDS.material3)
    const list = await apiListMaterials({ page: 1, pageSize: 100 })
    expect(list.items.find((m) => m.code === 'MAT-003')).toBeUndefined()
    expect(list.total).toBe(24)
  })

  it('keyword 匹配 code/name/searchCode 三者（引用选择器快捷搜索）', async () => {
    // LM = 螺母 M6 的 searchCode
    const bySearchCode = await apiListMaterials({ keyword: 'LM', page: 1, pageSize: 10 })
    expect(bySearchCode.items.some((m) => m.code === 'MAT-001')).toBe(true)
    const byName = await apiListMaterials({ keyword: '垫片', page: 1, pageSize: 10 })
    expect(byName.items.some((m) => m.code === 'MAT-002')).toBe(true)
    const byCode = await apiListMaterials({ keyword: 'MAT-01', page: 1, pageSize: 10 })
    expect(byCode.items.length).toBeGreaterThan(0)
  })

  it('filter DSL（字段/操作符白名单语义）+ 固定参数筛选', async () => {
    const byFixed = await apiListMaterials({ labelType: 'SKU', page: 1, pageSize: 100 })
    expect(byFixed.items.every((m) => m.labelType === 'SKU')).toBe(true)

    const byDsl = await apiListMaterials({
      filter: { op: 'and', conditions: [{ field: 'batchControlled', op: 'eq', value: true }] },
      page: 1, pageSize: 100,
    })
    expect(byDsl.items.every((m) => m.batchControlled === true)).toBe(true)
  })

  it('sort 白名单 + 分页（pageSize 默认 20；pageSize=0 全量）', async () => {
    const sorted = await apiListMaterials({ sort: [{ field: 'code', dir: 'desc' }], page: 1, pageSize: 5 })
    expect(sorted.items[0].code).toBe('MAT-025')
    expect(sorted.total).toBe(25)

    const paged = await apiListMaterials({ page: 3, pageSize: 10 })
    expect(paged.items.length).toBe(5)
    expect(paged.page).toBe(3)

    const all = await apiListMaterials({ pageSize: 0 })
    expect(all.items.length).toBe(25)
  })

  it('仓库删除保护：有库位 → WAREHOUSE_IN_USE；库位编码仓内唯一', async () => {
    await expect(apiDeleteWarehouse(MOCK_IDS.warehouse1)).rejects.toMatchObject({ code: 'WAREHOUSE_IN_USE', status: 409 })
    await expect(
      apiCreateLocation(MOCK_IDS.warehouse1, { code: 'STG-01', type: 'STAGING' }, 'key-3'),
    ).rejects.toMatchObject({ code: 'LOCATION_CODE_DUPLICATED', status: 409 })
  })

  it('批次列表（keyword 分页，ref 实体规则）+ 批次详情', async () => {
    const list = await apiListBatches({ keyword: '260810', page: 1, pageSize: 10 })
    expect(list.items.length).toBeGreaterThanOrEqual(2)
    expect(list.items[0]).toHaveProperty('batchNo')
    expect(list.items[0]).toHaveProperty('materialCode')
  })

  it('传输契约（通用规范 2.10 v1.9）：标准列表走 POST /{resource}/search，filter/sort 在 body 不进 URL', async () => {
    const filter = { op: 'and' as const, conditions: [{ field: 'batchControlled', op: 'eq' as const, value: true }] }
    const spy = vi.spyOn(globalThis, 'fetch')
    try {
      await apiListMaterials({
        keyword: 'LM',
        labelType: 'SKU',
        filter,
        sort: [{ field: 'code', dir: 'desc' }],
        page: 2,
        pageSize: 10,
      })
      const [url, init] = spy.mock.calls[0] as unknown as [string, RequestInit]
      expect(url).toBe('/api/materials/search')
      expect(init.method).toBe('POST')
      const body = JSON.parse(String(init.body)) as Record<string, unknown>
      expect(body.keyword).toBe('LM')
      expect(body.labelType).toBe('SKU')
      expect(body.filter).toEqual(filter)
      expect(body.sort).toEqual([{ field: 'code', dir: 'desc' }])
      expect(body.page).toBe(2)
      expect(body.pageSize).toBe(10)
      expect(url).not.toContain('filter')
      expect(url).not.toContain('sort')
    } finally {
      spy.mockRestore()
    }
  })

  it('传输契约：嵌套列表走 POST .../locations/search；引用选择器快捷搜索走 GET ?keyword=&pageSize=', async () => {
    const spy = vi.spyOn(globalThis, 'fetch')
    try {
      await apiListLocations(MOCK_IDS.warehouse1, { page: 1, pageSize: 20 })
      const [url1] = spy.mock.calls[0] as unknown as [string, RequestInit]
      expect(url1).toBe(`/api/warehouses/${MOCK_IDS.warehouse1}/locations/search`)

      await apiQuickSearchMaterials('LM')
      const [url2, init2] = spy.mock.calls[1] as unknown as [string, RequestInit]
      expect(url2).toBe('/api/materials?keyword=LM&pageSize=10')
      expect(init2.method ?? 'GET').toBe('GET')
      expect(init2.body).toBeUndefined()
    } finally {
      spy.mockRestore()
    }
  })

  it('批次默认排序 createdAt DESC（v2.1 最新在前，mock 服务端默认）', async () => {
    const list = await apiListBatches({ page: 1, pageSize: 100 })
    const times = list.items.map((b) => b.createdAt)
    const sorted = [...times].sort().reverse()
    expect(times).toEqual(sorted)
  })

  it('运行时字段元数据：资源存在返回 FieldMeta[]，未知资源 404', async () => {
    const meta = await apiMetaFields('materials')
    expect(meta.length).toBeGreaterThan(0)
    const code = meta.find((m) => m.field === 'code')
    expect(code?.type).toBe('string')
    expect(code?.operators).toContain('contains')
    const labelType = meta.find((m) => m.field === 'labelType')
    expect(labelType?.options?.length).toBeGreaterThan(0)

    await expect(apiMetaFields('nope')).rejects.toMatchObject({ code: 'NOT_FOUND', status: 404 })
  })

  it('新建仓库成功（mgmtMode 默认 MANUAL）', async () => {
    const wh = await apiCreateWarehouse({ code: 'WH-99', name: '测试仓' }, 'key-4')
    expect(wh.mgmtMode).toBe('MANUAL')
    expect(wh.status).toBe('ENABLED')
  })
})
