/**
 * 库位管理页（嵌套：/web/master/warehouses/:warehouseId/locations）——
 * 顶部显示所属仓库 + 返回；列表/新建/编辑/删除走通用能力。
 */
import { useQuery } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { apiDeleteLocation, apiListLocations, apiListWarehouses } from '@/api'
import type { LocationItem } from '@/api/types'
import { Button } from '@/components/ui/button'
import { MasterListPage } from '@/platform/master/MasterListPage'
import { dateTimeColumn, enumColumn, statusColumn, textColumn } from '@/platform/table/columns'
import { LocationFormDialog } from './LocationFormDialog'

export function LocationsPage() {
  const { t } = useTranslation()
  const { warehouseId = '' } = useParams<{ warehouseId: string }>()

  const whQuery = useQuery({
    queryKey: ['warehouses', { page: 1, pageSize: 100 }],
    queryFn: () => apiListWarehouses({ page: 1, pageSize: 100 }),
  })
  const warehouse = whQuery.data?.items.find((w) => w.id === warehouseId)

  const columns = useMemo(() => [
    textColumn<LocationItem>('code', t('locations.code'), true, 'font-medium tabular-nums'),
    textColumn<LocationItem>('searchCode', t('locations.searchCode')),
    enumColumn<LocationItem>('type', t('locations.type'), 'locationType'),
    enumColumn<LocationItem>('reachability', t('locations.reachability'), 'reachability'),
    statusColumn<LocationItem>('status', t('locations.status')),
    dateTimeColumn<LocationItem>('createdAt', t('common.createdAt')),
  ], [t])

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2 text-sm">
        <Button variant="ghost" size="sm" className="h-7 gap-1 px-2 text-muted-foreground" asChild>
          <Link to="/web/master/warehouses">
            <ArrowLeft className="size-3.5" data-icon />
            {t('locations.backToWarehouses')}
          </Link>
        </Button>
        {warehouse && (
          <span className="text-muted-foreground">
            {t('locations.warehouse')}: <span className="font-medium text-foreground tabular-nums">{warehouse.code}</span> {warehouse.name}
          </span>
        )}
      </div>
      <MasterListPage<LocationItem>
        resource="locations"
        titleKey="locations.title"
        columns={columns}
        listFn={(q) => apiListLocations(warehouseId, q)}
        commonSearchFields={['code', 'type', 'status']}
        keyword
        defaultSort={[{ field: 'code', dir: 'asc' }]}
        createPermission="action.location.create"
        updatePermission="action.location.edit"
        deletePermission="action.location.delete"
        deleteFn={(row) => apiDeleteLocation(row.id)}
        renderForm={({ open, editing, onOpenChange, onSaved }) => (
          <LocationFormDialog
            warehouseId={warehouseId}
            open={open}
            editing={editing}
            onOpenChange={onOpenChange}
            onSaved={onSaved}
          />
        )}
      />
    </div>
  )
}
