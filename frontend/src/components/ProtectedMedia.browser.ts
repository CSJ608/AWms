import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiFetchProtectedFile } from '@/api'
import { sessionStore } from '@/platform/auth/session-store'

describe('真实浏览器受保护媒体', () => {
  afterEach(() => {
    sessionStore.clear()
    document.body.replaceChildren()
  })

  it('Chrome 携带 Bearer 获取图片，并使用可加载的 blob URL 后释放', async () => {
    sessionStore.save({
      token: 'browser-test-token',
      expiresAt: '2099-01-01T00:00:00Z',
      user: { id: crypto.randomUUID(), username: 'browser', name: 'Browser', status: 'ACTIVE', roles: [], createdAt: '2026-08-21T00:00:00Z' },
      permissions: [],
      menus: { web: [], pda: [] },
    })
    const revoke = vi.spyOn(URL, 'revokeObjectURL')

    const response = await apiFetchProtectedFile('/api/browser-test/protected.png')
    const objectUrl = URL.createObjectURL(await response.blob())
    const image = new Image()
    const loaded = new Promise<void>((resolve, reject) => {
      image.addEventListener('load', () => resolve(), { once: true })
      image.addEventListener('error', () => reject(new Error('受保护图片加载失败')), { once: true })
    })
    image.src = objectUrl
    document.body.appendChild(image)
    await loaded

    expect(response.status).toBe(200)
    expect(objectUrl.startsWith('blob:')).toBe(true)
    expect(image.naturalWidth).toBe(1)
    URL.revokeObjectURL(objectUrl)
    expect(revoke).toHaveBeenCalledWith(objectUrl)
  })
})
