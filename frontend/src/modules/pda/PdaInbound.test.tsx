import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { fireEvent, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderApp, seedSession, makeOperatorSession, makeSupervisorSession } from '@/test/utils'
import { server } from '@/mocks/server'

const label = (payload: Record<string, unknown>) => `AWMS1:${JSON.stringify(payload)}`

async function scan(user: ReturnType<typeof userEvent.setup>, content: string) {
  const input = await screen.findByTestId('pda-scan-input')
  await user.clear(input)
  fireEvent.change(input, { target: { value: content } })
  await user.click(screen.getByTestId('pda-scan-submit'))
}

describe('PDA 菜单与入库作业', () => {
  it('收货、质检、上架角色均可看到对应 PDA 入口', async () => {
    seedSession(makeOperatorSession())
    renderApp('/pda')

    const menu = await screen.findByTestId('pda-menu')
    expect(within(menu).getByRole('button', { name: '收货' })).toBeInTheDocument()
    expect(within(menu).getByRole('button', { name: '质检' })).toBeInTheDocument()
    expect(within(menu).getByRole('button', { name: '上架' })).toBeInTheDocument()
  })

  it('PDA 菜单同时受 menus.pda 与 action 权限过滤', async () => {
    const supervisor = makeSupervisorSession()
    const receivingOnly = {
      ...supervisor,
      permissions: ['route.inbound', 'action.receiving.create'],
      menus: {
        ...supervisor.menus,
        pda: [
          { code: 'receiving', titleKey: 'pda.receiving', moduleCode: 'inbound', sort: 10 },
          { code: 'qc', titleKey: 'pda.qc', moduleCode: 'inbound', sort: 20 },
          { code: 'putaway', titleKey: 'pda.putaway', moduleCode: 'inbound', sort: 30 },
        ],
      },
    }
    seedSession(receivingOnly)
    server.use(http.get('/api/auth/me', () => HttpResponse.json({ code: 'OK', message: 'ok', data: receivingOnly })))
    renderApp('/pda')
    const menu = await screen.findByTestId('pda-menu')
    expect(within(menu).getByRole('button', { name: '收货' })).toBeInTheDocument()
    expect(within(menu).queryByRole('button', { name: '质检' })).not.toBeInTheDocument()
    expect(within(menu).queryByRole('button', { name: '上架' })).not.toBeInTheDocument()
  })

  it('收货扫 t=D 直接使用完整上下文，唯一码按 quantity 累加并防本地重复', async () => {
    seedSession(makeOperatorSession())
    const spy = vi.spyOn(globalThis, 'fetch')
    renderApp('/pda/receiving')
    const user = userEvent.setup()

    await scan(user, label({ v: 1, t: 'D', ty: 'PO', d: 'PO-20260819-0001', wh: 'WH-01' }))
    expect(await screen.findByText(/PO-20260819-0001/)).toBeInTheDocument()
    expect(spy.mock.calls.some(([url]) => String(url).includes('/api/inbound-orders/search'))).toBe(false)

    await user.selectOptions(screen.getByLabelText('单据行'), 'iol-002')
    expect(screen.getByTestId('receiving-qty')).toHaveValue('0.0000')

    await scan(user, label({ v: 1, t: 'U', s: 'MAT-004', u: 'BOX-20260820-0001', q: '5.0000' }))
    expect(await screen.findByTestId('receiving-qty')).toHaveValue('5.0000')
    await scan(user, label({ v: 1, t: 'U', s: 'MAT-004', u: 'BOX-20260820-0002', q: '5.0000' }))
    expect(screen.getByTestId('receiving-qty')).toHaveValue('10.0000')

    await scan(user, label({ v: 1, t: 'U', s: 'MAT-004', u: 'BOX-20260820-0002', q: '5.0000' }))
    expect(await screen.findByText('已收过')).toBeInTheDocument()
    spy.mockRestore()
  })

  it('收货照片上传失败保留当前输入并提示附件错误', async () => {
    seedSession(makeOperatorSession())
    renderApp('/pda/receiving')
    const user = userEvent.setup()

    await scan(user, label({ v: 1, t: 'D', ty: 'PO', d: 'PO-20260819-0001', wh: 'WH-01' }))
    await screen.findByText(/PO-20260819-0001/)

    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement
    fireEvent.change(fileInput, { target: { files: [new File(['bad'], 'bad.txt', { type: 'text/plain' })] } })
    expect(await screen.findByText('附件类型不支持')).toBeInTheDocument()
    expect(screen.getByText(/PO-20260819-0001/)).toBeInTheDocument()
  })

  it('质检扫批次标签按多条选择、1 条直达、0 条提示处理', async () => {
    seedSession(makeOperatorSession())
    renderApp('/pda/qc')
    const user = userEvent.setup()

    await scan(user, label({ v: 1, t: 'B', s: 'MAT-001', b: '260810001', q: '10.0000' }))
    expect(await screen.findByText('选择待质检任务')).toBeInTheDocument()
    expect(screen.getAllByText(/RCP-20260819-0004/).length).toBeGreaterThan(0)

    await scan(user, label({ v: 1, t: 'B', s: 'MAT-002', b: '260809001', q: '12.0000' }))
    expect(await screen.findByTestId('quality-pass')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: '返回' }))
    await user.click(screen.getByRole('button', { name: '质检' }))
    await scan(user, label({ v: 1, t: 'B', s: 'MAT-001', b: '260810002', q: '10.0000' }))
    expect(await screen.findByText('未找到待质检任务')).toBeInTheDocument()
  })

  it('上架必须扫库位确认，非法库位由契约错误阻断', async () => {
    seedSession(makeOperatorSession())
    renderApp('/pda/putaway')
    const user = userEvent.setup()

    await user.click(await screen.findByText(/RCP-20260819-0002/))
    expect(await screen.findByText('DEF-01')).toBeInTheDocument()
    await scan(user, 'STG-01')
    await user.click(screen.getByTestId('submit-putaway'))
    expect(await screen.findByText('目标库位不合法')).toBeInTheDocument()
  })
})
