/**
 * 认证上下文 —— 会话状态（loading/authed/anon）、登录/登出/会话恢复（/auth/me）、
 * 权限判断（三级权限点）。401 刷新失败 → onSessionExpired → 登出跳登录（通用规范 2.4）。
 */
import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { apiLogin, apiLogout, apiMe } from '@/api'
import type { LoginResponse } from '@/api/types'
import { hasPermission } from '@/platform/permission'
import { onSessionExpired } from './session-events'
import { sessionStore } from './session-store'
import type { StoredSession } from './session-store'

export type AuthStatus = 'loading' | 'authed' | 'anon'

interface AuthContextValue {
  status: AuthStatus
  session: StoredSession | null
  login: (username: string, password: string) => Promise<void>
  logout: () => Promise<void>
  hasPerm: (code: string) => boolean
}

const AuthContext = createContext<AuthContextValue | null>(null)

export { AuthContext }

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('loading')
  const [session, setSession] = useState<StoredSession | null>(null)

  // 会话恢复：本地有 token → /auth/me 校验并刷新 user/permissions/menus
  useEffect(() => {
    const cached = sessionStore.load()
    if (!cached) {
      setStatus('anon')
      return
    }
    let cancelled = false
    apiMe()
      .then((me: LoginResponse) => {
        if (cancelled) return
        // 会话恢复：token/expiresAt 以 /auth/me 返回的当前 token 为准（后端返回请求头中
        // 的 token；恢复链路可能已 401→refresh→重放，旧 cached.token 已失效，回写会导致
        // 后续请求再次 401/refresh——验收 F-01）。
        const next: StoredSession = {
          token: me.token,
          expiresAt: me.expiresAt,
          user: me.user,
          permissions: me.permissions,
          menus: me.menus,
        }
        sessionStore.save(next)
        setSession(next)
        setStatus('authed')
      })
      .catch(() => {
        if (cancelled) return
        sessionStore.clear()
        setStatus('anon')
      })
    return () => {
      cancelled = true
    }
  }, [])

  // 401 刷新失败 → 登出
  useEffect(() => onSessionExpired(() => {
    sessionStore.clear()
    setSession(null)
    setStatus('anon')
  }), [])

  const login = useCallback(async (username: string, password: string) => {
    const res = await apiLogin({ username, password })
    const next: StoredSession = {
      token: res.token,
      expiresAt: res.expiresAt,
      user: res.user,
      permissions: res.permissions,
      menus: res.menus,
    }
    sessionStore.save(next)
    setSession(next)
    setStatus('authed')
  }, [])

  const logout = useCallback(async () => {
    try {
      await apiLogout()
    } catch {
      // 登出尽力而为：本地会话必须清理
    }
    sessionStore.clear()
    setSession(null)
    setStatus('anon')
  }, [])

  const hasPerm = useCallback(
    (code: string) => hasPermission(session?.permissions, code),
    [session],
  )

  const value = useMemo(
    () => ({ status, session, login, logout, hasPerm }),
    [status, session, login, logout, hasPerm],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}
