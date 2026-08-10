/**
 * 物料管理页（基础数据-Web物料-规格 v1.1）—— 列表/新建编辑/筛选 SearchField/导入导出。
 */
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { apiDeleteMaterial, apiListMaterials } from '@/api'
import type { MaterialItem } from '@/api/types'
import { MasterListPage } from '@/platform/master/MasterListPage'
import {
  boolColumn, dateTimeColumn, enumColumn, statusColumn, textColumn,
} from '@/platform/table/columns'
import { MaterialFormDialog } from './MaterialFormDialog'

export function MaterialsPage() {
  const { t } = useTranslation()

  const columns = useMemo(() => [
    textColumn<MaterialItem>('code', t('materials.code'), true, 'font-medium tabular-nums'),
    textColumn<MaterialItem>('name', t('materials.name'), true),
    boolColumn<MaterialItem>('batchControlled', t('materials.batchControlled')),
    enumColumn<MaterialItem>('labelType', t('materials.labelType'), 'labelType'),
    enumColumn<MaterialItem>('defaultUom', t('materials.defaultUom'), 'uom'),
    statusColumn<MaterialItem>('status', t('materials.status')),
    dateTimeColumn<MaterialItem>('createdAt', t('common.createdAt')),
  ], [t])

  return (
    <MasterListPage<MaterialItem>
      resource="materials"
      titleKey="materials.title"
      columns={columns}
      listFn={apiListMaterials}
      commonSearchFields={['code', 'name', 'status']}
      keyword
      defaultSort={[{ field: 'code', dir: 'asc' }]}
      createPermission="action.material.create"
      updatePermission="action.material.edit"
      deletePermission="action.material.delete"
      deleteFn={(row) => apiDeleteMaterial(row.id)}
      importExport={{ moduleCode: 'materials', permission: 'action.import' }}
      emptyTextKey="materials.empty"
      renderForm={({ open, editing, onOpenChange, onSaved }) => (
        <MaterialFormDialog
          open={open}
          editing={editing}
          onOpenChange={onOpenChange}
          onSaved={onSaved}
        />
      )}
    />
  )
}
