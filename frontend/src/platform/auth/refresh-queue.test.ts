/**
 * 401 单飞刷新 + 排队重放（评审 B-31：以 401 响应触发，不依赖本地倒计时）。
 * 通过 MSW + client 端到端验证：并发 401 共享一次 refresh，全部重放成功。
 */
import { describe, expect, it, vi } from 'vitest'
import { apiListMaterials } from '../../api'
import { invalidateMockTokens, mockState } from '../../mocks/handlers'
import { onSessionExpired } from './session-events'
import { sessionStore } from './session-store'
import { makeAdminSession } from '../../test/utils'

describe('401 单飞刷新 + 排队重放', () => {
  it('并发 401：只发一次 refresh，两个请求都重放成功', async () => {
    sessionStore.save(makeAdminSession())
    invalidateMockTokens(true) // 服务端使全部 token 失效 → 受保护接口 401

    const [r1, r2] = await Promise.all([
      apiListMaterials({ page: 1, pageSize: 5 }),
      apiListMaterials({ page: 1, pageSize: 5 }),
    ])

    expect(r1.items.length).toBeGreaterThan(0)
    expect(r2.items.length).toBeGreaterThan(0)
    expect(mockState.refreshCount).toBe(1)
  })

  it('顺序 401 重放：刷新后 token 更新到本地会话', async () => {
    sessionStore.save(makeAdminSession())
    invalidateMockTokens(true)

    await apiListMaterials({ page: 1, pageSize: 5 })

    // 刷新签发新 token（mock-token-admin#<轮次>，对齐真实后端每次 refresh 换发）
    expect(sessionStore.getToken()).toBe('mock-token-admin#1')
    expect(mockState.refreshCount).toBe(1)
    // 刷新后 token 已恢复有效，后续请求不再触发刷新
    await apiListMaterials({ page: 1, pageSize: 5 })
    expect(mockState.refreshCount).toBe(1)
  })

  it('刷新失败（token 非法）→ 抛 UNAUTHORIZED + 广播会话过期事件', async () => {
    const expired = vi.fn()
    const off = onSessionExpired(expired)
    sessionStore.save({ ...makeAdminSession(), token: 'totally-invalid-token' })

    await expect(apiListMaterials({ page: 1, pageSize: 5 })).rejects.toMatchObject({ code: 'UNAUTHORIZED' })
    expect(expired).toHaveBeenCalledTimes(1)
    expect(sessionStore.getToken()).toBeNull()
    off()
  })
})
