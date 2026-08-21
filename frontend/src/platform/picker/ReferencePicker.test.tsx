/**
 * 通用 ReferencePicker（F-04，通用规范 2.10 ref 交互模式）：
 * 快捷搜索（keyword 防抖 + 候选下拉）→ 选中返回 id；完整选择弹窗（标准列表分页）。
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it } from 'vitest'
import { ReferencePicker } from './ReferencePicker'
import { makeAdminSession, seedSession } from '@/test/utils'
import { MOCK_IDS } from '@/mocks/seed'

function Harness() {
  const [value, setValue] = useState<string | null>(null)
  return (
    <div>
      <ReferencePicker resource="materials" value={value} onChange={setValue} />
      <span data-testid="picked">{value ?? 'none'}</span>
    </div>
  )
}

const renderPicker = () => {
  seedSession(makeAdminSession())
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <Harness />
    </QueryClientProvider>,
  )
}

describe('ReferencePicker', () => {
  it('快捷搜索：keyword 命中 searchCode，选择后返回 id', async () => {
    renderPicker()
    fireEvent.click(screen.getByTestId('ref-picker-materials'))

    const user = userEvent.setup()
    await user.type(screen.getByTestId('ref-quick-search'), 'LM') // 螺母 M6 的助记码
    await screen.findByText('MAT-001 螺母 M6')

    fireEvent.click(screen.getByText('MAT-001 螺母 M6'))
    expect(screen.getByTestId('picked').textContent).toBe(MOCK_IDS.material1)
    // 选中后按钮回显展示文案
    expect(screen.getByTestId('ref-picker-materials')).toHaveTextContent('MAT-001 螺母 M6')
  })

  it('完整选择弹窗：列表加载 → 行选择返回 id', async () => {
    renderPicker()
    fireEvent.click(screen.getByTestId('ref-picker-materials'))
    fireEvent.click(screen.getByTestId('ref-open-full'))

    const rows = await screen.findAllByTestId('ref-pick-row')
    expect(rows.length).toBeGreaterThan(1)
    fireEvent.click(rows[1])

    expect(screen.getByTestId('picked').textContent).not.toBe('none')
  })

  it('清空：选中后可点 X 清除', async () => {
    renderPicker()
    fireEvent.click(screen.getByTestId('ref-picker-materials'))
    const user = userEvent.setup()
    await user.type(screen.getByTestId('ref-quick-search'), 'LM')
    await screen.findByText('MAT-001 螺母 M6')
    fireEvent.click(screen.getByText('MAT-001 螺母 M6'))

    expect(screen.getByTestId('picked').textContent).toBe(MOCK_IDS.material1)
    fireEvent.click(screen.getByLabelText('重置'))
    expect(screen.getByTestId('picked').textContent).toBe('none')
  })
})
