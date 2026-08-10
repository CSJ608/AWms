/**
 * 菜单/权限过滤（F-03）：作业员（wang01）登录 → 侧边栏只显示有 menu 权限的入口；
 * 无 action 权限 → 新建/导入导出按钮隐藏；路由权限（route.master）→ 直达 URL 可进模块。
 */
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { renderApp } from '@/test/utils'

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
    renderApp('/login')
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
    window.history.pushState({}, '', '/web/master/materials')
    window.dispatchEvent(new PopStateEvent('popstate'))
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
})
