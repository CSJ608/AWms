/**
 * 路由注册表 —— 静态注册 + 权限过滤渲染（评审 A-10 / ADR-001 模块注册表驱动）。
 * 路由权限：route.<moduleCode>（能否进入模块）；菜单显隐由登录返回 menus 注册表驱动（框架设计 v0.2）。
 */
import { Loader2 } from 'lucide-react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router-dom'
import { useAuth } from '@/platform/auth/auth-context'
import { routePermission } from '@/platform/permission'
import { BatchesPage } from '@/modules/web/master/BatchesPage'
import { LocationsPage } from '@/modules/web/master/LocationsPage'
import { MaterialsPage } from '@/modules/web/master/MaterialsPage'
import { SourcesPage } from '@/modules/web/master/SourcesPage'
import { WarehousesPage } from '@/modules/web/master/WarehousesPage'

export interface WebRouteEntry {
  path: string
  moduleCode: string
  titleKey: string
  element: ReactNode
}

/** Web 路由注册表（新增模块 = 这里加一项 + 菜单注册表，框架不动） */
export const WEB_ROUTES: WebRouteEntry[] = [
  { path: 'master/materials', moduleCode: 'master-data', titleKey: 'nav.material', element: <MaterialsPage /> },
  { path: 'master/warehouses', moduleCode: 'master-data', titleKey: 'nav.warehouse', element: <WarehousesPage /> },
  { path: 'master/warehouses/:warehouseId/locations', moduleCode: 'master-data', titleKey: 'nav.warehouse', element: <LocationsPage /> },
  { path: 'master/sources', moduleCode: 'master-data', titleKey: 'nav.source', element: <SourcesPage /> },
  { path: 'master/batches', moduleCode: 'master-data', titleKey: 'nav.batch', element: <BatchesPage /> },
]

/**
 * 菜单项 → 前端路由（联调对齐：后端菜单 path 为模块级，如 /、/master-data、/system；
 * 前端路由为页面级 /web/...）。已含 /web 前缀（mock）原样返回；模块级 path 映射到该模块
 * 第一个已注册页面；模块页面未实现（如 dashboard/inbound/system）→ 原样返回（落到 404 页）。
 */
export function menuTarget(menu: { path: string; moduleCode: string }): string {
  if (menu.path.startsWith('/web/')) return menu.path
  const first = WEB_ROUTES.find((r) => r.moduleCode === menu.moduleCode)
  return first ? `/web/${first.path}` : menu.path
}

/** 登录守卫：会话恢复中 → 加载；未登录 → 登录页 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { status } = useAuth()
  const { t } = useTranslation()

  if (status === 'loading') {
    return (
      <div className="flex h-screen items-center justify-center gap-2 text-muted-foreground">
        <Loader2 className="size-5 animate-spin" data-icon />
        {t('common.loading')}
      </div>
    )
  }
  if (status === 'anon') return <Navigate to="/login" replace />
  return children
}

/** 模块路由权限守卫：无 route.<moduleCode> → 无权限页 */
export function RequirePermission({ moduleCode, children }: { moduleCode: string; children: ReactNode }) {
  const { hasPerm } = useAuth()
  const { t } = useTranslation()

  if (!hasPerm(routePermission(moduleCode))) {
    return (
      <div className="flex h-full min-h-64 flex-col items-center justify-center gap-1 text-center">
        <p className="text-base font-medium">{t('common.noPermission')}</p>
        <p className="text-sm text-muted-foreground">{t('common.noPermissionDesc')}</p>
      </div>
    )
  }
  return children
}
