/**
 * 批次浏览页 —— 只读（批次契约 v0.4：系统自动建批次，前端不创建/删除）。
 */
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { apiListBatches } from '@/api'
import type { BatchItem } from '@/api/types'
import { Badge } from '@/components/ui/badge'
import { MasterListPage } from '@/platform/master/MasterListPage'
import { dateColumn, dateTimeColumn, enumColumn, statusColumn, textColumn } from '@/platform/table/columns'

export function BatchesPage() {
  const { t } = useTranslation()

  const columns = useMemo(() => [
    textColumn<BatchItem>('batchNo', t('batches.batchNo'), true, 'font-medium tabular-nums'),
    textColumn<BatchItem>('materialCode', t('batches.materialCode'), true, 'tabular-nums'),
    textColumn<BatchItem>('sourceBatchNo', t('batches.sourceBatchNo')),
    enumColumn<BatchItem>('sourceType', t('batches.sourceType'), 'sourceType'),
    textColumn<BatchItem>('sourceCode', t('batches.sourceCode')),
    dateColumn<BatchItem>('productionDate', t('batches.productionDate')),
    dateColumn<BatchItem>('expiryDate', t('batches.expiryDate')),
    statusColumn<BatchItem>('status', t('batches.status')),
    dateTimeColumn<BatchItem>('createdAt', t('common.createdAt')),
  ], [t])

  return (
    <div className="space-y-3">
      <Badge variant="neutral" className="text-muted-foreground">
        {t('batches.readonly')}
      </Badge>
      <MasterListPage<BatchItem>
        resource="batches"
        titleKey="batches.title"
        columns={columns}
        listFn={apiListBatches}
        commonSearchFields={['batchNo', 'materialCode', 'status']}
        keyword
        defaultSort={[{ field: 'createdAt', dir: 'desc' }]}
        createPermission={null}
        updatePermission={null}
        deletePermission={null}
        renderForm={() => null}
      />
    </div>
  )
}
