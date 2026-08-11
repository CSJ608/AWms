/**
 * 根路由/404 路由守门（修复 test 环境访问 / 显示 404）。
 */
import { describe, expect, it } from 'vitest'
import { screen } from '@testing-library/react'
import { renderApp } from '@/test/utils'

describe('App 根路由', () => {
  it('未登录访问 / 重定向到登录页', async () => {
    renderApp('/')
    expect(await screen.findByText('登录 AWms')).toBeInTheDocument()
  })

  it('未知路径显示应用内 404 页', () => {
    renderApp('/no-such-route')
    expect(screen.getByText('404')).toBeInTheDocument()
    expect(screen.getByText('您访问的页面不存在或已被移除')).toBeInTheDocument()
  })
})