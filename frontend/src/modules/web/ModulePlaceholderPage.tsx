/**
 * 模块开发中占位页 —— 未实现模块（工作台/入库/系统）菜单入口的落点，
 * 替代“静默绕圈回主数据”与 404（用户反馈路由问题，2026-08-11 修复）。
 */
import { useTranslation } from 'react-i18next'

export function ModulePlaceholderPage({ titleKey }: { titleKey: string }) {
  const { t } = useTranslation()
  return (
    <div className="flex h-screen flex-col items-center justify-center gap-2 text-center">
      <p className="text-2xl font-semibold">{t(titleKey)}</p>
      <p className="text-sm text-muted-foreground">{t('common.developing')}</p>
    </div>
  )
}