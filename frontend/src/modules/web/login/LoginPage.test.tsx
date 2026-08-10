/**
 * 登录页交互测试（F-02）：成功登录进入 Web 后台；错误分支展示后端 message；
 * 必填校验 inline；停用账号拦截。
 */
import { fireEvent, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { makeAdminSession, renderApp, seedSession } from '@/test/utils'

async function login(username: string, password: string) {
  const user = userEvent.setup()
  await user.type(screen.getByLabelText(/用户名/), username)
  await user.type(screen.getByLabelText(/密码/), password)
  fireEvent.click(screen.getByTestId('login-submit'))
}

describe('登录页', () => {
  it('登录成功 → 进入 Web 后台，侧边栏按 menus.web 渲染', async () => {
    renderApp('/login')
    await login('admin', 'admin123')

    await screen.findByTestId('sidebar-nav')
    expect(screen.getByText('物料')).toBeInTheDocument()
    expect(screen.getByText('仓库')).toBeInTheDocument()
    expect(screen.getByText('来源')).toBeInTheDocument()
    expect(screen.getByText('批次')).toBeInTheDocument()
  })

  it('密码错误 → 展示后端 message（LOGIN_FAILED），停留登录页', async () => {
    renderApp('/login')
    await login('admin', 'wrong-pass')

    await screen.findByText('用户名或密码错误')
    expect(screen.getByTestId('login-form')).toBeInTheDocument()
    expect(screen.queryByTestId('sidebar-nav')).not.toBeInTheDocument()
  })

  it('停用账号 → USER_DISABLED message', async () => {
    renderApp('/login')
    await login('li02', '123456')

    await screen.findByText('账号已停用')
  })

  it('必填校验 inline 提示', async () => {
    renderApp('/login')
    fireEvent.click(screen.getByTestId('login-submit'))

    await waitFor(() => {
      expect(screen.getByText('请输入用户名')).toBeInTheDocument()
      expect(screen.getByText('请输入密码')).toBeInTheDocument()
    })
  })

  it('已登录访问 /login → 重定向 /web', async () => {
    seedSession(makeAdminSession())
    renderApp('/login')
    await screen.findByTestId('sidebar-nav')
  })
})
