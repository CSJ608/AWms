/** 平台能力单测：数量格式化（decimal 18,4 去尾零）/ 时间 / 幂等键 / 权限 / 枚举标签 */
import { describe, expect, it } from 'vitest'
import { formatDateTime, formatQuantity } from './format'
import { hasAllPermissions, hasAnyPermission, hasPermission, routePermission } from './permission'
import { enumLabelKey, statusVariant } from './labels'

describe('formatQuantity（契约 2.3：decimal 字符串去尾零）', () => {
  it('去尾零', () => {
    expect(formatQuantity('10.0000')).toBe('10')
    expect(formatQuantity('0.5000')).toBe('0.5')
    expect(formatQuantity('1.2300')).toBe('1.23')
    expect(formatQuantity('100')).toBe('100')
  })
  it('null/undefined/空串 → "-"；非法值原样透传', () => {
    expect(formatQuantity(null)).toBe('-')
    expect(formatQuantity(undefined)).toBe('-')
    expect(formatQuantity('')).toBe('-')
    expect(formatQuantity('abc')).toBe('abc')
  })
})

describe('formatDateTime', () => {
  it('ISO UTC 转本地格式；非法值原样', () => {
    expect(formatDateTime('2026-08-10T08:00:00Z')).toMatch(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$/)
    expect(formatDateTime(null)).toBe('-')
  })
})

describe('permission', () => {
  const perms = ['route.master', 'action.material.create', 'menu.materials']
  it('hasPermission / all / any', () => {
    expect(hasPermission(perms, 'route.master')).toBe(true)
    expect(hasPermission(perms, 'action.material.delete')).toBe(false)
    expect(hasPermission(undefined, 'route.master')).toBe(false)
    expect(hasAllPermissions(perms, ['route.master', 'menu.materials'])).toBe(true)
    expect(hasAllPermissions(perms, ['route.master', 'xxx'])).toBe(false)
    expect(hasAnyPermission(perms, ['xxx', 'action.material.create'])).toBe(true)
    expect(hasAnyPermission([], ['xxx'])).toBe(false)
  })
  it('routePermission 生成 route.<moduleCode>', () => {
    expect(routePermission('master')).toBe('route.master')
  })
})

describe('labels（枚举 → i18n key，未知值透传）', () => {
  it('已知枚举返回 key', () => {
    expect(enumLabelKey('labelType', 'SKU')).toBe('enums.labelType.sku')
    expect(enumLabelKey('status', 'ENABLED')).toBe('enums.status.enabled')
    expect(enumLabelKey('uom', 'KG')).toBe('enums.uom.KG')
  })
  it('未知值/空值返回 null（调用方透传原码）', () => {
    expect(enumLabelKey('labelType', 'FUTURE_TYPE')).toBeNull()
    expect(enumLabelKey('status', null)).toBeNull()
    expect(enumLabelKey('unknownEnum', 'X')).toBeNull()
  })
  it('statusVariant：ENABLED/ACTIVE → success，其余 neutral', () => {
    expect(statusVariant('ENABLED')).toBe('success')
    expect(statusVariant('ACTIVE')).toBe('success')
    expect(statusVariant('DISABLED')).toBe('neutral')
    expect(statusVariant('CLOSED')).toBe('neutral')
    expect(statusVariant(null)).toBe('neutral')
  })
})
