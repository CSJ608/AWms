/**
 * 测试渲染工具 —— QueryClientProvider + AuthProvider + MemoryRouter + Toaster。
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactElement, ReactNode } from 'react'
import { MemoryRouter } from 'react-router-dom'
import { Toaster } from 'sonner'
import { AuthProvider } from '@/platform/auth/auth-context'
import { sessionStore } from '@/platform/auth/session-store'
import type { StoredSession } from '@/platform/auth/session-store'
import { seedUsers } from '@/mocks/seed'
import { menusFor, permissionsOf } from '@/mocks/seed'
import { AppRoutes } from '@/App'

export function makeAdminSession(): StoredSession {
  const user = seedUsers.find((u) => u.username === 'admin')!
  return {
    token: 'mock-token-admin',
    expiresAt: '2099-01-01T00:00:00Z',
    user,
    permissions: permissionsOf('admin'),
    menus: menusFor('admin'),
  }
}

export function makeOperatorSession(): StoredSession {
  const user = seedUsers.find((u) => u.username === 'wang01')!
  return {
    token: 'mock-token-wang01',
    expiresAt: '2099-01-01T00:00:00Z',
    user,
    permissions: permissionsOf('wang01'),
    menus: menusFor('wang01'),
  }
}

export function makeSupervisorSession(): StoredSession {
  const user = seedUsers.find((u) => u.username === 'zhang03')!
  return {
    token: 'mock-token-zhang03',
    expiresAt: '2099-01-01T00:00:00Z',
    user,
    permissions: permissionsOf('zhang03'),
    menus: menusFor('zhang03'),
  }
}

/** 预置登录会话（AuthProvider 挂载时会经 /auth/me 恢复） */
export function seedSession(session: StoredSession = makeAdminSession()): void {
  sessionStore.save(session)
}

export function renderApp(initialEntry = '/login', ui?: ReactElement): ReturnType<typeof render> {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  const wrap = (children: ReactNode) => (
    <QueryClientProvider client={qc}>
      <AuthProvider>
        <MemoryRouter initialEntries={[initialEntry]}>
          {children}
        </MemoryRouter>
        <Toaster richColors position="top-center" />
      </AuthProvider>
    </QueryClientProvider>
  )
  return render(ui ? wrap(ui) : wrap(<AppRoutes />))
}

/** 已登录态渲染（管理员） */
export function renderAuthed(initialEntry = '/web/master/materials'): ReturnType<typeof render> {
  seedSession(makeAdminSession())
  return renderApp(initialEntry)
}
