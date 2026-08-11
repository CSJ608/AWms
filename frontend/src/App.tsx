/**
 * 应用根 —— 路由树（web/pda 双路由树 + 登录 + 404）、认证守卫、全局 Toast。
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { Toaster } from 'sonner'
import { AuthProvider, useAuth } from '@/platform/auth/auth-context'
import { RequireAuth, RequirePermission, WEB_ROUTES, menuTarget } from '@/platform/route-registry'
import { AppLayout } from '@/modules/web/layout/AppLayout'
import { LoginPage } from '@/modules/web/login/LoginPage'
import { ModulePlaceholderPage } from '@/modules/web/ModulePlaceholderPage'
import { PdaHomePage } from '@/modules/pda/PdaHomePage'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
      staleTime: 15_000,
    },
  },
})

/** 登录后默认落地：第一个可导航的菜单路径（模块菜单 → 该模块首页；无则回退主数据物料） */
function WebIndexRedirect() {
  const { session } = useAuth()
  const menus = session?.menus.web ?? []
  const first = [...menus]
    .sort((a, b) => a.sort - b.sort)
    .find((m) => menuTarget(m).startsWith('/web/'))
  return <Navigate to={first ? menuTarget(first) : '/web/master/materials'} replace />
}


/** 根路径：未登录 → /login；已登录 → 工作台占位页（工作台本期未实现，不再回跳主数据/404） */
function RootRoute() {
  const { status } = useAuth()
  if (status === 'loading') return null
  if (status !== 'authed') return <Navigate to='/login' replace />
  return <ModulePlaceholderPage titleKey='nav.workspace' />
}

function NotFoundPage() {
  const { t } = useTranslation()
  return (
    <div className="flex h-screen flex-col items-center justify-center gap-1">
      <p className="text-2xl font-semibold">404</p>
      <p className="text-sm text-muted-foreground">{t('common.notFoundDesc')}</p>
    </div>
  )
}

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/" element={<RootRoute />} />
      <Route path="/inbound" element={<RequireAuth><ModulePlaceholderPage titleKey="nav.inbound" /></RequireAuth>} />
      <Route path="/system" element={<RequireAuth><ModulePlaceholderPage titleKey="nav.system" /></RequireAuth>} />
      <Route path="/master-data" element={<Navigate to="/web/master/materials" replace />} />

      {/* Web 后台（RequireAuth 布局） */}
      <Route
        path="/web"
        element={
          <RequireAuth>
            <AppLayout />
          </RequireAuth>
        }
      >
        <Route index element={<WebIndexRedirect />} />
        {WEB_ROUTES.map((r) => (
          <Route
            key={r.path}
            path={r.path}
            element={<RequirePermission moduleCode={r.moduleCode}>{r.element}</RequirePermission>}
          />
        ))}
      </Route>

      {/* PDA 作业端（双路由树预留） */}
      <Route
        path="/pda"
        element={
          <RequireAuth>
            <PdaHomePage />
          </RequireAuth>
        }
      />

      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <Toaster richColors position="top-center" />
        <BrowserRouter>
          <AppRoutes />
        </BrowserRouter>
      </AuthProvider>
    </QueryClientProvider>
  )
}
