/**
 * 来源管理页（供应商/车间）—— 通用列表复用（来源契约 v0.2）。
 */
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { apiDeleteSource, apiListSources } from '@/api'
import type { SourceItem } from '@/api/types'
import { MasterListPage } from '@/platform/master/MasterListPage'
import { dateTimeColumn, enumColumn, statusColumn, textColumn } from '@/platform/table/columns'
import { SourceFormDialog } from './SourceFormDialog'

export function SourcesPage() {
  const { t } = useTranslation()

  const columns = useMemo(() => [
    enumColumn<SourceItem>('type', t('sources.type'), 'sourceType'),
    textColumn<SourceItem>('code', t('sources.code'), true, 'font-medium tabular-nums'),
    textColumn<SourceItem>('name', t('sources.name'), true),
    textColumn<SourceItem>('searchCode', t('sources.searchCode')),
    statusColumn<SourceItem>('status', t('sources.status')),
    dateTimeColumn<SourceItem>('createdAt', t('common.createdAt')),
  ], [t])

  return (
    <MasterListPage<SourceItem>
      resource="sources"
      titleKey="sources.title"
      columns={columns}
      listFn={apiListSources}
      commonSearchFields={['type', 'code', 'name']}
      keyword
      defaultSort={[{ field: 'code', dir: 'asc' }]}
      createPermission="action.source.create"
      updatePermission="action.source.edit"
      deletePermission="action.source.delete"
      deleteFn={(row) => apiDeleteSource(row.id)}
      renderForm={({ open, editing, onOpenChange, onSaved }) => (
        <SourceFormDialog open={open} editing={editing} onOpenChange={onOpenChange} onSaved={onSaved} />
      )}
    />
  )
}
