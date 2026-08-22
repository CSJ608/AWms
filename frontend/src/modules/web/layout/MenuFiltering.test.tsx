/**
 * 菜单/权限过滤（F-03）：作业员（wang01）登录 → 侧边栏只显示有 menu 权限的入口；
 * 无 action 权限 → 新建/导入导出按钮隐藏；路由权限（route.master）→ 直达 URL 可进模块。
 * F3 接回：仓管（zhang03/SUPERVISOR）主数据按钮可见（action.warehouse/location/source.*）；
 * 无 action 权限 → MasterListPage 操作按钮隐藏（permission 语义守门）。
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { apiListWarehouses } from '@/api'
import type { WarehouseItem } from '@/api/types'
import { AuthContext } from '@/platform/auth/auth-context'
import { MasterListPage } from '@/platform/master/MasterListPage'
import { textColumn } from '@/platform/table/columns'
import { makeAdminSession, makeOperatorSession, makeSupervisorSession, renderApp, seedSession } from '@/test/utils'
import { MOCK_IDS } from '@/mocks/seed'

describe('菜单与权限过滤', () => {
  it('管理员：四个主数据菜单齐全 + 操作按钮可见', async () => {
    renderApp('/login')
    const user = userEvent.setup()
    await user.type(screen.getByLabelText(/用户名/), 'admin')
    await user.type(screen.getByLabelText(/密码/), 'admin123')
    fireEvent.click(screen.getByTestId('login-submit'))

    await screen.findByTestId('sidebar-nav')
    expect(screen.getByText('物料')).toBeInTheDocument()
    expect(screen.getByText('仓库')).toBeInTheDocument()
    expect(screen.getByText('来源')).toBeInTheDocument()
    expect(screen.getByText('批次')).toBeInTheDocument()

    // 落地到物料页：新建 + 导入导出按钮可见（action 权限）
    await screen.findByText('螺母 M6')
    expect(screen.getByTestId('btn-create')).toBeInTheDocument()
    expect(screen.getByTestId('btn-import-export')).toBeInTheDocument()
  })

  it('作业员（OPERATOR）：无 menu.master-data → 主数据菜单不显示；直达物料页 → 无权限页', async () => {
    const view = renderApp('/login')
    const user = userEvent.setup()
    await user.type(screen.getByLabelText(/用户名/), 'wang01')
    await user.type(screen.getByLabelText(/密码/), '123456')
    fireEvent.click(screen.getByTestId('login-submit'))

    // OPERATOR 默认仅有 inbound 权限（与后端默认角色一致）：主数据模块菜单全部隐藏
    await screen.findByTestId('sidebar-nav')
    expect(screen.queryByText('物料')).not.toBeInTheDocument()
    expect(screen.queryByText('仓库')).not.toBeInTheDocument()
    expect(screen.queryByText('来源')).not.toBeInTheDocument()
    expect(screen.queryByText('批次')).not.toBeInTheDocument()

    // 无 route.master-data → 直达物料页显示无权限页（路由权限守卫）
    view.unmount()
    seedSession(makeOperatorSession())
    renderApp('/web/master/materials')
    expect(await screen.findByText('无权限')).toBeInTheDocument()
  })

  it('路由权限守卫：无 route.<moduleCode> 权限 → 无权限页', async () => {
    const { AuthContext } = await import('@/platform/auth/auth-context')
    const { RequirePermission } = await import('@/platform/route-registry')
    const authValue = {
      status: 'authed' as const,
      session: null,
      login: async () => {},
      logout: async () => {},
      hasPerm: () => false,
    }
    render(
      <AuthContext.Provider value={authValue}>
        <RequirePermission moduleCode="master-data"><div>secret-content</div></RequirePermission>
      </AuthContext.Provider>,
    )
    expect(screen.getByText('无权限')).toBeInTheDocument()
    expect(screen.queryByText('secret-content')).not.toBeInTheDocument()
  })

  it('仓管（SUPERVISOR）：主数据菜单齐全；仓库页按钮可见（action.warehouse.*）', async () => {
    renderApp('/login')
    const user = userEvent.setup()
    await user.type(screen.getByLabelText(/用户名/), 'zhang03')
    await user.type(screen.getByLabelText(/密码/), '123456')
    fireEvent.click(screen.getByTestId('login-submit'))

    // SUPERVISOR（与后端种子一致：入库 + 主数据）→ 主数据菜单全部可见
    await screen.findByTestId('sidebar-nav')
    expect(screen.getByText('物料')).toBeInTheDocument()
    expect(screen.getByText('仓库')).toBeInTheDocument()
    expect(screen.getByText('来源')).toBeInTheDocument()
    expect(screen.getByText('批次')).toBeInTheDocument()

    // 落地物料页 → 点侧边栏进仓库页：新建 + 行内编辑/删除按钮可见（持 action.warehouse.*）
    await screen.findByText('螺母 M6')
    await user.click(screen.getByText('仓库'))
    await screen.findByText('一号仓')
    expect(screen.getByTestId('btn-create')).toBeInTheDocument()
    expect(screen.getAllByTestId('btn-edit').length).toBeGreaterThan(0)
    expect(screen.getAllByTestId('btn-delete').length).toBeGreaterThan(0)
  })

  it('仓管（SUPERVISOR）：库位页按钮可见（action.location.*）', async () => {
    seedSession(makeSupervisorSession())
    renderApp(`/web/master/warehouses/${MOCK_IDS.warehouse1}/locations`)
    await screen.findByText('STG-01')
    expect(screen.getByTestId('btn-create')).toBeInTheDocument()
    expect(screen.getAllByTestId('btn-edit').length).toBeGreaterThan(0)
    expect(screen.getAllByTestId('btn-delete').length).toBeGreaterThan(0)
  })

  it('仓管（SUPERVISOR）：来源页按钮可见（action.source.*）', async () => {
    seedSession(makeSupervisorSession())
    renderApp('/web/master/sources')
    await screen.findByText('华东五金')
    expect(screen.getByTestId('btn-create')).toBeInTheDocument()
    expect(screen.getAllByTestId('btn-edit').length).toBeGreaterThan(0)
    expect(screen.getAllByTestId('btn-delete').length).toBeGreaterThan(0)
  })

  it('作业员（OPERATOR）：直达仓库页 → 无权限页（无 route.master-data）', async () => {
    seedSession(makeOperatorSession())
    renderApp('/web/master/warehouses')
    expect(await screen.findByText('无权限')).toBeInTheDocument()
  })

  it('无 action 权限：MasterListPage 新建/编辑/删除按钮全部隐藏（permission 语义守门）', async () => {
    seedSession(makeAdminSession())
    const authValue = {
      status: 'authed' as const,
      session: null,
      login: async () => {},
      logout: async () => {},
      hasPerm: () => false,
    }
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={qc}>
        <AuthContext.Provider value={authValue}>
          <MasterListPage
            resource="warehouses"
            titleKey="warehouses.title"
            columns={[textColumn<WarehouseItem>('code', '编码', true)]}
            listFn={apiListWarehouses}
            createPermission="action.warehouse.create"
            updatePermission="action.warehouse.edit"
            deletePermission="action.warehouse.delete"
            deleteFn={async () => {}}
            renderForm={() => null}
          />
        </AuthContext.Provider>
      </QueryClientProvider>,
    )
    // 列表数据照常加载（种子会话 token 有效），但操作按钮全部隐藏
    await screen.findByText('WH-01')
    expect(screen.queryByTestId('btn-create')).not.toBeInTheDocument()
    expect(screen.queryByTestId('btn-edit')).not.toBeInTheDocument()
    expect(screen.queryByTestId('btn-delete')).not.toBeInTheDocument()
  })
})
