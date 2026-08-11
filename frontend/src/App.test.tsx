/**
 * 根路由/404 路由守门（修复 test 环境访问 / 显示 404）。
 */
import { describe, expect, it } from 'vitest'
import { screen } from '@testing-library/react'
import { renderApp, renderAuthed } from '@/test/utils'

describe('App 根路由', () => {
  it('未登录访问 / 重定向到登录页', async () => {
    renderApp('/')
    expect(await screen.findByText('登录 AWms')).toBeInTheDocument()
  })


  it('已登录访问 / 显示工作台占位页（不再回跳/404）', async () => {
    renderAuthed('/')
    expect(await screen.findByText('工作台')).toBeInTheDocument()
    expect(screen.getByText('模块开发中，敬请期待')).toBeInTheDocument()
  })

  it('已登录访问 /inbound 与 /system 显示开发中占位页', async () => {
    renderAuthed('/inbound')
    expect(await screen.findByText('入库')).toBeInTheDocument()
    expect(screen.getByText('模块开发中，敬请期待')).toBeInTheDocument()
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