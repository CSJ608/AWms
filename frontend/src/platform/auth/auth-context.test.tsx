/**
 * 会话恢复链路守门（验收 F-01）：恢复时 /auth/me 可能 401 → 自动 refresh → 重放成功，
 * 会话必须用 me.token（重放后请求头中的新 token）构建并保存，不得回写恢复前捕获的
 * 旧 cached.token（否则后续请求再次 401/refresh，实测 1 次过期恢复触发 3 次 refresh）。
 */
import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { apiListMaterials } from '../../api'
import { invalidateMockTokens, mockState } from '../../mocks/handlers'
import { sessionStore } from './session-store'
import { makeAdminSession, renderApp, seedSession } from '../../test/utils'

describe('会话恢复（/auth/me 401 → refresh → 重放）', () => {
  it('恢复后 sessionStore token == me.token（新 token），且后续请求不再二次 refresh', async () => {
    // 预置一个已失效会话（token 本身可解析，但服务端整体失效 → /auth/me 401）
    seedSession(makeAdminSession())
    invalidateMockTokens(true)

    renderApp('/')

    // 恢复链路完成（authed → 工作台占位页渲染）后：
    // 会话中保存的必须是重放后 /auth/me 回显的新 token（mock-token-admin#1），
    // 而不是恢复前捕获的旧 token（mock-token-admin）——旧实现回写 cached.token 即失败。
    await waitFor(() => {
      expect(sessionStore.getToken()).toBe('mock-token-admin#1')
    })
    expect(mockState.refreshCount).toBe(1)
    // 恢复后会话保持
    expect(await screen.findByText('模块开发中，敬请期待')).toBeInTheDocument()

    // 后续请求直接成功，不再触发二次 refresh
    const r = await apiListMaterials({ page: 1, pageSize: 5 })
    expect(r.items.length).toBeGreaterThan(0)
    expect(mockState.refreshCount).toBe(1)
  })

  it('恢复失败（refresh 401）→ 会话清理回登录页', async () => {
    // 坏 token：refresh 也无法解析 → 401 → 清会话
    seedSession({ ...makeAdminSession(), token: 'totally-invalid-token' })

    renderApp('/')

    expect(await screen.findByText('登录 AWms')).toBeInTheDocument()
    expect(sessionStore.getToken()).toBeNull()
  })
})
