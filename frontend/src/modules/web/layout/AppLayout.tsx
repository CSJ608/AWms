/**
 * Web 布局 —— 左侧菜单（登录返回 menus.web 注册表驱动，iconKey 映射）+ 顶部上下文
 * （当前页标题 / 语言切换 / 用户菜单登出）。
 */
import { ChevronsUpDown, LogOut, Package } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { cn } from '@/lib/utils'
import { useAuth } from '@/platform/auth/auth-context'
import { changeLanguage } from '@/i18n'
import { menuIcon } from '@/platform/menu-icons'
import { menuTarget } from '@/platform/route-registry'

export function AppLayout() {
  const { t } = useTranslation()
  const { session, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  const menus = session?.menus.web ?? []

  // 按 groupKey 分组 + sort 排序（注册表驱动）
  const groups = menus.reduce<Record<string, typeof menus>>((acc, m) => {
    acc[m.groupKey] = [...(acc[m.groupKey] ?? []), m].sort((a, b) => a.sort - b.sort)
    return acc
  }, {})

  const current = menus.find((m) => {
    const target = menuTarget(m)
    return target !== '/' && location.pathname.startsWith(target)
  })

  const handleLogout = async () => {
    await logout()
    navigate('/login', { replace: true })
  }

  return (
    <div className="flex h-screen overflow-hidden bg-background">
      {/* 侧边栏 */}
      <aside className="flex w-56 shrink-0 flex-col border-r bg-sidebar">
        <div className="flex h-14 items-center gap-2 border-b px-4">
          <span className="flex size-8 items-center justify-center rounded-lg bg-primary text-primary-foreground">
            <Package className="size-4" data-icon />
          </span>
          <span className="text-sm font-semibold">{t('common.appName')}</span>
        </div>
        <nav className="flex-1 space-y-4 overflow-y-auto p-3" data-testid="sidebar-nav">
          {Object.entries(groups).map(([groupKey, items]) => (
            <div key={groupKey}>
              {groupKey && (
                <p className="mb-1.5 px-2 text-xs font-medium text-muted-foreground">{t(groupKey)}</p>
              )}
              <div className="space-y-0.5">
                {items.map((menu) => {
                  const Icon = menuIcon(menu.iconKey)
                  return (
                    <NavLink
                      key={menu.path}
                      to={menuTarget(menu)}
                      className={({ isActive }) => cn(
                        'flex items-center gap-2 rounded-md px-2 py-1.5 text-sm transition-colors',
                        isActive
                          ? 'bg-sidebar-accent font-medium text-sidebar-accent-foreground'
                          : 'text-sidebar-foreground hover:bg-sidebar-accent/60',
                      )}
                    >
                      <Icon className="size-4 shrink-0" data-icon />
                      <span className="truncate">{t(menu.titleKey)}</span>
                    </NavLink>
                  )
                })}
              </div>
            </div>
          ))}
        </nav>
      </aside>

      {/* 主区域 */}
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 shrink-0 items-center justify-between border-b bg-card px-4">
          <h1 className="text-sm font-semibold" data-testid="page-title">
            {current ? t(current.titleKey) : t('common.appName')}
          </h1>
          <div className="flex items-center gap-2">
            <LangSwitcher />
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="sm" className="gap-2 px-2" data-testid="user-menu">
                  <Avatar className="size-6">
                    <AvatarFallback className="bg-primary/10 text-xs text-primary">
                      {session?.user.name?.slice(0, 1) ?? '?'}
                    </AvatarFallback>
                  </Avatar>
                  <span className="max-w-32 truncate text-sm">{session?.user.name}</span>
                  <ChevronsUpDown className="size-3.5 text-muted-foreground" data-icon />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuLabel>
                  {session?.user.username} · {session?.user.roles.map((r) => r.name).join(' / ')}
                </DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem onClick={handleLogout} data-testid="logout-item">
                  <LogOut className="size-3.5" data-icon />
                  {t('common.logout')}
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </header>
        <main className="flex-1 overflow-auto p-4">
          <Outlet />
        </main>
      </div>
    </div>
  )
}

function LangSwitcher() {
  const { t } = useTranslation()
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="sm" className="px-2 text-sm" data-testid="lang-switcher">
          {t('common.lang', { defaultValue: '语言' })}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem onClick={() => changeLanguage('zh')} data-testid="lang-zh">中文</DropdownMenuItem>
        <DropdownMenuItem onClick={() => changeLanguage('en')} data-testid="lang-en">English</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
