/**
 * PDA 作业端占位 —— 双路由树预留（评审 A-5）；作业入口按登录返回 menus.pda 注册表渲染，
 * 实际作业流程第 4 批实现。
 */
import { ScanLine } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useAuth } from '@/platform/auth/auth-context'
import { menuIcon } from '@/platform/menu-icons'

export function PdaHomePage() {
  const { t } = useTranslation()
  const { session } = useAuth()
  const entries = session?.menus.pda ?? []

  return (
    <div className="flex min-h-screen flex-col bg-muted/40">
      <header className="flex h-14 items-center justify-center gap-2 bg-primary text-primary-foreground">
        <ScanLine className="size-5" data-icon />
        <span className="font-semibold">{t('common.appName')} · PDA</span>
      </header>
      <main className="flex-1 space-y-4 p-4">
        <p className="text-center text-sm text-muted-foreground">{t('pda.placeholder')}</p>
        <div className="mx-auto grid max-w-md grid-cols-1 gap-3">
          {entries.map((entry) => {
            const Icon = menuIcon(entry.code)
            return (
              <button
                key={entry.code}
                type="button"
                disabled
                className="flex items-center gap-3 rounded-xl bg-card p-4 text-left shadow-sm disabled:opacity-60"
              >
                <span className="flex size-11 items-center justify-center rounded-lg bg-primary/10 text-primary">
                  <Icon className="size-5" data-icon />
                </span>
                <span className="text-base font-medium">{t(entry.titleKey)}</span>
              </button>
            )
          })}
        </div>
      </main>
    </div>
  )
}
