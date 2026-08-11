/**
 * 新建对话框表单清空守门（验收⑥）：物料/仓库/库位/来源 4 个 FormDialog
 * 新建（非编辑）打开时重置为默认值，不保留上次输入；编辑态正确回填、编辑→新建切换不回填错乱。
 * （实现：values useMemo 依赖 [editing, open]，打开/切换时 RHF 触发 reset——验收 F-R3/⑥）
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import type { LocationItem, MaterialItem, SourceItem, WarehouseItem } from '@/api/types'
import { seedLocations, seedMaterials, seedSources, seedWarehouses } from '@/mocks/seed'
import { LocationFormDialog } from './LocationFormDialog'
import { MaterialFormDialog } from './MaterialFormDialog'
import { SourceFormDialog } from './SourceFormDialog'
import { WarehouseFormDialog } from './WarehouseFormDialog'

const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })

/** 对话框使用 useMutation，harness 需 QueryClientProvider */
function wrap(ui: React.ReactElement) {
  return <QueryClientProvider client={qc}>{ui}</QueryClientProvider>
}

function inputValue(testId: string): string {
  return (screen.getByTestId(testId) as HTMLInputElement).value
}

describe('新建对话框打开时清空表单（验收⑥）', () => {
  it('物料：新建打开字段为空；输入后关闭再新建仍为空；编辑态正确回填', () => {
    const props = { open: false, editing: null as MaterialItem | null, onOpenChange: vi.fn(), onSaved: vi.fn() }
    const view = render(wrap(<MaterialFormDialog {...props} />))
    const openWith = (editing: MaterialItem | null, open: boolean) =>
      view.rerender(wrap(<MaterialFormDialog {...props} editing={editing} open={open} />))

    // 新建打开 → 字段为空（默认值）
    openWith(null, true)
    expect(inputValue('f-code')).toBe('')
    expect(inputValue('f-name')).toBe('')

    // 输入后关闭再新建 → 仍为空，不保留上次输入
    fireEvent.change(screen.getByTestId('f-code'), { target: { value: 'MAT-777' } })
    fireEvent.change(screen.getByTestId('f-name'), { target: { value: '测试物料' } })
    openWith(null, false)
    openWith(null, true)
    expect(inputValue('f-code')).toBe('')
    expect(inputValue('f-name')).toBe('')

    // 编辑态 → 正确回填
    const item = seedMaterials[0]
    openWith(item, true)
    expect(inputValue('f-code')).toBe(item.code)
    expect(inputValue('f-name')).toBe(item.name)

    // 编辑 → 新建切换 → 清空（不回填错乱）
    openWith(null, false)
    openWith(null, true)
    expect(inputValue('f-code')).toBe('')
    expect(inputValue('f-name')).toBe('')
  })

  it('仓库：新建打开清空；编辑态回填；编辑→新建切换清空', () => {
    const props = { open: false, editing: null as WarehouseItem | null, onOpenChange: vi.fn(), onSaved: vi.fn() }
    const view = render(wrap(<WarehouseFormDialog {...props} />))
    const openWith = (editing: WarehouseItem | null, open: boolean) =>
      view.rerender(wrap(<WarehouseFormDialog {...props} editing={editing} open={open} />))

    openWith(null, true)
    expect(inputValue('f-code')).toBe('')
    expect(inputValue('f-name')).toBe('')
    fireEvent.change(screen.getByTestId('f-code'), { target: { value: 'WH-99' } })
    fireEvent.change(screen.getByTestId('f-name'), { target: { value: '九号仓' } })
    openWith(null, false)
    openWith(null, true)
    expect(inputValue('f-code')).toBe('')
    expect(inputValue('f-name')).toBe('')

    const item = seedWarehouses[0]
    openWith(item, true)
    expect(inputValue('f-code')).toBe(item.code)
    expect(inputValue('f-name')).toBe(item.name)
    openWith(null, false)
    openWith(null, true)
    expect(inputValue('f-code')).toBe('')
  })

  it('库位：新建打开清空；编辑态回填；编辑→新建切换清空', () => {
    const props = {
      warehouseId: 'wh-01', open: false, editing: null as LocationItem | null, onOpenChange: vi.fn(), onSaved: vi.fn(),
    }
    const view = render(wrap(<LocationFormDialog {...props} />))
    const openWith = (editing: LocationItem | null, open: boolean) =>
      view.rerender(wrap(<LocationFormDialog {...props} editing={editing} open={open} />))

    openWith(null, true)
    expect(inputValue('f-code')).toBe('')
    expect(inputValue('f-searchCode')).toBe('')
    fireEvent.change(screen.getByTestId('f-code'), { target: { value: 'STG-99' } })
    openWith(null, false)
    openWith(null, true)
    expect(inputValue('f-code')).toBe('')

    const item = seedLocations[0]
    openWith(item, true)
    expect(inputValue('f-code')).toBe(item.code)
    expect(inputValue('f-searchCode')).toBe(item.searchCode ?? '')
    openWith(null, false)
    openWith(null, true)
    expect(inputValue('f-code')).toBe('')
  })

  it('来源：新建打开清空；编辑态回填；编辑→新建切换清空', () => {
    const props = { open: false, editing: null as SourceItem | null, onOpenChange: vi.fn(), onSaved: vi.fn() }
    const view = render(wrap(<SourceFormDialog {...props} />))
    const openWith = (editing: SourceItem | null, open: boolean) =>
      view.rerender(wrap(<SourceFormDialog {...props} editing={editing} open={open} />))

    openWith(null, true)
    expect(inputValue('f-code')).toBe('')
    expect(inputValue('f-name')).toBe('')
    fireEvent.change(screen.getByTestId('f-code'), { target: { value: 'SUP-999' } })
    openWith(null, false)
    openWith(null, true)
    expect(inputValue('f-code')).toBe('')

    const item = seedSources[0]
    openWith(item, true)
    expect(inputValue('f-code')).toBe(item.code)
    expect(inputValue('f-name')).toBe(item.name)
    openWith(null, false)
    openWith(null, true)
    expect(inputValue('f-code')).toBe('')
  })
})
