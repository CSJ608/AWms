/**
 * 仓库管理页 —— 通用列表复用 + 行内“库位”入口（基础数据规格：仓库/库位/来源/批次沿用物料模板）。
 */
import { Layers } from 'lucide-react'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { apiDeleteWarehouse, apiListWarehouses } from '@/api'
import type { WarehouseItem } from '@/api/types'
import { Button } from '@/components/ui/button'
import { MasterListPage } from '@/platform/master/MasterListPage'
import { dateTimeColumn, enumColumn, statusColumn, textColumn } from '@/platform/table/columns'
import { WarehouseFormDialog } from './WarehouseFormDialog'

export function WarehousesPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()

  const columns = useMemo(() => [
    textColumn<WarehouseItem>('code', t('warehouses.code'), true, 'font-medium tabular-nums'),
    textColumn<WarehouseItem>('name', t('warehouses.name'), true),
    enumColumn<WarehouseItem>('mgmtMode', t('warehouses.mgmtMode'), 'mgmtMode'),
    statusColumn<WarehouseItem>('status', t('warehouses.status')),
    dateTimeColumn<WarehouseItem>('createdAt', t('common.createdAt')),
  ], [t])

  return (
    <MasterListPage<WarehouseItem>
      resource="warehouses"
      titleKey="warehouses.title"
      columns={columns}
      listFn={apiListWarehouses}
      commonSearchFields={['code', 'name', 'status']}
      keyword
      defaultSort={[{ field: 'code', dir: 'asc' }]}
      createPermission="action.warehouse.create"
      updatePermission="action.warehouse.edit"
      deletePermission="action.warehouse.delete"
      deleteFn={(row) => apiDeleteWarehouse(row.id)}
      rowExtraActions={(row) => (
        <Button
          variant="ghost"
          size="sm"
          className="h-7 px-2"
          onClick={() => navigate(`/web/master/warehouses/${row.id}/locations`)}
          data-testid="btn-locations"
        >
          <Layers className="size-3.5" data-icon />
          {t('warehouses.locations')}
        </Button>
      )}
      renderForm={({ open, editing, onOpenChange, onSaved }) => (
        <WarehouseFormDialog open={open} editing={editing} onOpenChange={onOpenChange} onSaved={onSaved} />
      )}
    />
  )
}
