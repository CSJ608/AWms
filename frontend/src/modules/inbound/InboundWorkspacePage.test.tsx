import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderApp, seedSession, makeAdminSession, makeSupervisorSession } from '@/test/utils'
import { server } from '@/mocks/server'

describe('入库管理 Web 工作区', () => {
  it('/inbound/* 深链接必须经过 route.inbound 守卫', async () => {
    const session = makeAdminSession()
    seedSession({ ...session, permissions: session.permissions.filter((p) => p !== 'route.inbound') })
    server.use(http.get('/api/auth/me', () => HttpResponse.json({
      code: 'OK',
      message: 'ok',
      data: { ...session, permissions: session.permissions.filter((p) => p !== 'route.inbound') },
    })))

    renderApp('/inbound/orders')
    expect(await screen.findByText('无权限')).toBeInTheDocument()
    expect(screen.queryByTestId('inbound-workspace')).not.toBeInTheDocument()
  })

  it('工作标签对新建和详情去重，并随业务路由同步', async () => {
    seedSession(makeSupervisorSession())
    renderApp('/inbound/orders')
    const user = userEvent.setup()

    expect(await screen.findByTestId('work-tabs')).toBeInTheDocument()
    await screen.findByText('PO-20260819-0001')

    await user.click(screen.getByTestId('new-inbound-order'))
    expect(screen.getAllByText('新建入库单').length).toBeGreaterThanOrEqual(2)
    const workTabsAfterNew = within(screen.getByTestId('work-tabs')).getAllByRole('button')
    expect(workTabsAfterNew.filter((button) => button.textContent?.includes('新建入库单'))).toHaveLength(1)

    await user.click(within(screen.getByTestId('work-tabs')).getByRole('button', { name: '入库管理' }))
    await screen.findByText('PO-20260819-0001')
    await user.click(listButton('PO-20260819-0001'))
    await screen.findByText('打印单据码')
    await user.click(within(screen.getByTestId('work-tabs')).getByRole('button', { name: '入库管理' }))
    expect(screen.getAllByText('PO-20260819-0001').length).toBeGreaterThan(0)
    await user.click(listButton('PO-20260819-0001'))

    const detailTabs = within(screen.getByTestId('work-tabs')).getAllByRole('button')
      .filter((button) => button.textContent?.includes('PO-20260819-0001'))
    expect(detailTabs).toHaveLength(1)

    await user.click(within(screen.getByTestId('work-tabs')).getByRole('button', { name: '入库管理' }))
    await user.click(screen.getByRole('button', { name: '收货记录' }))
    expect(await screen.findByText('RCP-20260819-0001')).toBeInTheDocument()
    const baseTabs = within(screen.getByTestId('work-tabs')).getAllByRole('button')
      .filter((button) => button.textContent?.includes('入库管理'))
    expect(baseTabs).toHaveLength(1)
  })

  it('业务标签切换保活筛选状态，关闭当前标签回到最近使用标签', async () => {
    seedSession(makeSupervisorSession())
    renderApp('/inbound/orders')
    const user = userEvent.setup()
    const orderNo = await screen.findByLabelText('单号')
    await user.type(orderNo, 'KEEP-ME')
    await user.click(screen.getByRole('button', { name: '收货记录' }))
    await user.click(screen.getByRole('button', { name: '入库单' }))
    expect(screen.getByLabelText('单号')).toHaveValue('KEEP-ME')

    await user.clear(screen.getByLabelText('单号'))
    await user.click(screen.getByTestId('new-inbound-order'))
    await user.click(within(screen.getByTestId('work-tabs')).getByRole('button', { name: '入库管理' }))
    await user.click(await screen.findByRole('button', { name: 'PO-20260819-0001' }))
    await screen.findByText('打印单据码')
    await user.click(within(screen.getByTestId('work-tabs')).getByRole('button', { name: '新建入库单' }))
    await user.click(within(screen.getByTestId('work-tabs')).getByRole('button', { name: '关闭新建入库单' }))
    expect(await screen.findByText('打印单据码')).toBeInTheDocument()
  })

  it('新建页取消复用未提交确认流程', async () => {
    seedSession(makeSupervisorSession())
    renderApp('/inbound/orders/new')
    const user = userEvent.setup()
    const quantity = await screen.findByDisplayValue('1.0000')
    await user.clear(quantity)
    await user.type(quantity, '2')
    await user.click(screen.getByRole('button', { name: '取消' }))
    expect(await screen.findByText('内容尚未提交')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: '继续编辑' })).toBeInTheDocument()
  })
})

function listButton(name: string): HTMLElement {
  const tabs = screen.getByTestId('work-tabs')
  const button = screen.getAllByRole('button', { name }).find((item) => !tabs.contains(item))
  if (!button) throw new Error(`missing list button: ${name}`)
  return button
}
