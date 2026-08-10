/**
 * 会话存储 —— token/user/permissions/menus 的 localStorage 持久化（纯模块，无 React）。
 */
import type { LoginResponse } from '../../api/types'
import { getLocalStorage } from '../../lib/storage'

const STORAGE_KEY = 'awms.session'

export interface StoredSession {
  token: string
  expiresAt: string
  user: LoginResponse['user']
  permissions: string[]
  menus: LoginResponse['menus']
}

export const sessionStore = {
  load(): StoredSession | null {
    try {
      const raw = getLocalStorage()?.getItem(STORAGE_KEY)
      if (!raw) return null
      const parsed = JSON.parse(raw) as StoredSession
      if (!parsed.token || !parsed.user) return null
      return parsed
    } catch {
      return null
    }
  },

  save(session: StoredSession): void {
    getLocalStorage()?.setItem(STORAGE_KEY, JSON.stringify(session))
  },

  clear(): void {
    getLocalStorage()?.removeItem(STORAGE_KEY)
  },

  getToken(): string | null {
    return sessionStore.load()?.token ?? null
  },

  setToken(token: string, expiresAt: string): void {
    const session = sessionStore.load()
    if (!session) return
    sessionStore.save({ ...session, token, expiresAt })
  },
}
