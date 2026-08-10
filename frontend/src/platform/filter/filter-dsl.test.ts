/**
 * 平台能力单测：filter DSL builder（通用规范 2.10 两层并存 + 空值不发送）。
 */
import { describe, expect, it } from 'vitest'
import { MATERIAL_FIELDS } from '../../mocks/meta'
import { buildListQuery, isEmptyCondition, toggleSort } from './filter-dsl'
import type { SearchValues } from './filter-dsl'

const fields = MATERIAL_FIELDS

describe('buildListQuery', () => {
  it('固定参数：code/name contains、labelType/status eq 发固定参数（不重复进 DSL）', () => {
    const values: SearchValues = {
      code: { op: 'contains', value: 'MAT' },
      name: { op: 'contains', value: '螺母' },
      labelType: { op: 'eq', value: 'SKU' },
      status: { op: 'eq', value: 'ENABLED' },
    }
    const q = buildListQuery({ resource: 'materials', values, fields, page: 1, pageSize: 20 })
    expect(q.code).toBe('MAT')
    expect(q.name).toBe('螺母')
    expect(q.labelType).toBe('SKU')
    expect(q.status).toBe('ENABLED')
    expect(q.filter).toBeUndefined()
    expect(q.page).toBe(1)
    expect(q.pageSize).toBe(20)
  })

  it('非固定参数字段与非常规操作符 → filter DSL', () => {
    const values: SearchValues = {
      searchCode: { op: 'contains', value: 'LM' },
      batchControlled: { op: 'eq', value: true },
      labelType: { op: 'neq', value: 'NONE' },
      defaultQtyPerLabel: { op: 'gte', value: '10' },
    }
    const q = buildListQuery({ resource: 'materials', values, fields })
    expect(q.filter).toEqual({
      op: 'and',
      conditions: [
        { field: 'searchCode', op: 'contains', value: 'LM' },
        { field: 'batchControlled', op: 'eq', value: true },
        { field: 'labelType', op: 'neq', value: 'NONE' },
        { field: 'defaultQtyPerLabel', op: 'gte', value: '10' },
      ],
    })
  })

  it('keyword 透传；between 双值数组保留', () => {
    const values: SearchValues = {
      createdAt: { op: 'between', value: ['2026-08-01', '2026-08-10'] },
    }
    const q = buildListQuery({ resource: 'materials', values, fields, keyword: 'LM' })
    expect(q.keyword).toBe('LM')
    expect(q.filter).toEqual({
      op: 'and',
      conditions: [{ field: 'createdAt', op: 'between', value: ['2026-08-01', '2026-08-10'] }],
    })
  })

  it('空值不发送：空字符串条件被忽略，无条件的查询不带 filter 参数', () => {
    const values: SearchValues = {
      code: { op: 'contains', value: '' },
      name: { op: 'contains', value: '   ' },
    }
    const q = buildListQuery({ resource: 'materials', values, fields })
    expect(q.code).toBeUndefined()
    expect(q.filter).toBeUndefined()
    expect(Object.keys(q).filter((k) => k !== 'page' && k !== 'pageSize')).toHaveLength(0)
  })

  it('isNull/isNotNull 视为非空条件，value 不参与序列化', () => {
    const values: SearchValues = {
      searchCode: { op: 'isNull', value: null },
    }
    const q = buildListQuery({ resource: 'materials', values, fields })
    expect(q.filter).toEqual({ op: 'and', conditions: [{ field: 'searchCode', op: 'isNull' }] })
  })

  it('sort 透传；切换排序 asc → desc → 取消', () => {
    const q = buildListQuery({ resource: 'materials', values: {}, fields, sort: [{ field: 'code', dir: 'desc' }] })
    expect(q.sort).toEqual([{ field: 'code', dir: 'desc' }])
    expect(toggleSort(q.sort, 'code')).toEqual([])
    expect(toggleSort(undefined, 'code')).toEqual([{ field: 'code', dir: 'asc' }])
    expect(toggleSort([{ field: 'code', dir: 'asc' }], 'code')).toEqual([{ field: 'code', dir: 'desc' }])
  })

  it('操作符与固定参数声明不符 → 走 DSL（如 status 用 in）', () => {
    const values: SearchValues = {
      status: { op: 'in', value: ['ENABLED', 'DISABLED'] },
    }
    const q = buildListQuery({ resource: 'materials', values, fields })
    expect(q.status).toBeUndefined()
    expect(q.filter).toEqual({
      op: 'and',
      conditions: [{ field: 'status', op: 'in', value: ['ENABLED', 'DISABLED'] }],
    })
  })
})

describe('isEmptyCondition', () => {
  it('undefined / 空字符串 / 全空数组 / null 值为空', () => {
    expect(isEmptyCondition(undefined)).toBe(true)
    expect(isEmptyCondition({ op: 'eq', value: '' })).toBe(true)
    expect(isEmptyCondition({ op: 'between', value: ['', ''] })).toBe(true)
    expect(isEmptyCondition({ op: 'eq', value: null })).toBe(true)
  })
  it('isNull/isNotNull 永不为空', () => {
    expect(isEmptyCondition({ op: 'isNull', value: null })).toBe(false)
    expect(isEmptyCondition({ op: 'isNotNull', value: null })).toBe(false)
  })
})
