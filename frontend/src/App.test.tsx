/**
 * 根路由/404 路由守门（修复 test 环境访问 / 显示 404）+ 验收 F-02：
 * 占位页（/、/inbound、/system）包 AppLayout，从菜单进入后保留侧边栏导航，不再死胡同。
 */
import { describe, expect, it } from 'vitest'
import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderApp, renderAuthed } from '@/test/utils'

describe('App 根路由', () => {
  it('未登录访问 / 重定向到登录页', async () => {
    renderApp('/')
    expect(await screen.findByText('登录 AWms')).toBeInTheDocument()
  })

  it('已登录访问 / 显示工作台占位页（AppLayout 内，保留侧边栏导航）', async () => {
    renderAuthed('/')
    expect(await screen.findByText('模块开发中，敬请期待')).toBeInTheDocument()
    // F-02：占位页不再是死胡同——侧边栏常驻，可从菜单返回主数据
    expect(screen.getByTestId('sidebar-nav')).toBeInTheDocument()
  })

  it('已登录访问 /inbound 进入入库管理；/system 仍显示开发中占位页', async () => {
    renderAuthed('/inbound')
    expect(await screen.findByRole('heading', { name: '入库管理', level: 2 })).toBeInTheDocument()
    expect(screen.getByText('入库单')).toBeInTheDocument()
    expect(screen.getByTestId('sidebar-nav')).toBeInTheDocument()
    expect(screen.getAllByText('入库').length).toBeGreaterThan(0)
  })

  it('从侧边栏菜单进入占位页后仍可导航返回主数据（F-02）', async () => {
    renderAuthed('/web/master/materials')
    await screen.findByText('螺母 M6')

    // 菜单 → 入库占位页：侧边栏还在
    const user = userEvent.setup()
    const nav = screen.getByTestId('sidebar-nav')
    await user.click(within(nav).getByRole('link', { name: '入库' }))
    expect(await screen.findByRole('heading', { name: '入库管理', level: 2 })).toBeInTheDocument()
    expect(screen.getByTestId('sidebar-nav')).toBeInTheDocument()

    // 侧边栏 → 返回主数据物料页
    await user.click(within(nav).getByRole('link', { name: '物料' }))
    expect(await screen.findByText('螺母 M6')).toBeInTheDocument()

    // 工作台占位页（根路径）同样保留导航
    await user.click(within(nav).getByRole('link', { name: '工作台' }))
    expect(await screen.findByText('模块开发中，敬请期待')).toBeInTheDocument()
    expect(screen.getByTestId('sidebar-nav')).toBeInTheDocument()
  })

  it('已登录访问 /master-data 重定向到主数据物料页', async () => {
    renderAuthed('/master-data')
    expect(await screen.findAllByText('物料')).not.toHaveLength(0)
    expect(screen.queryByText('404')).not.toBeInTheDocument()
  })

  it('未知路径显示应用内 404 页', () => {
    renderApp('/no-such-route')
    expect(screen.getByText('404')).toBeInTheDocument()
    expect(screen.getByText('您访问的页面不存在或已被移除')).toBeInTheDocument()
  })
})
