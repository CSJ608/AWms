/**
 * 通用主数据列表页 —— 物料/仓库/库位/来源/批次复用（基础数据规格 v1.1 + 通用列表页规格）：
 * SearchPanel（元数据驱动）+ DataTable（分页/排序）+ 新建/编辑 Dialog + 删除 AlertDialog +
 * 导入导出弹窗；按钮按 action 权限显隐；空态/加载骨架/错误条；操作列右对齐。
 */
import { useQuery, useQueryClient } from '@tanstack/react-query'
import type { ColumnDef } from '@tanstack/react-table'
import { Plus, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { apiMetaFields } from '@/api'
import type { ListQuery, PageResult, SortSpec } from '@/api/types'
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle, AlertDialogTrigger } from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/platform/auth/auth-context'
import { SearchPanel } from '@/platform/filter/SearchPanel'
import { ImportExportDialog } from '@/platform/import-export/ImportExportDialog'
import { DataTable } from '@/platform/table/DataTable'

export interface MasterListPageProps<T extends { id: string }> {
  /** 运行时元数据资源（GET /api/meta/fields/{resource}） */
  resource: string
  titleKey: string
  columns: ColumnDef<T>[]
  listFn: (q: ListQuery) => Promise<PageResult<T>>
  /** 默认展开的筛选字段 */
  commonSearchFields?: string[]
  keyword?: boolean
  defaultSort?: SortSpec[]
  /** 权限码：undefined=默认允许（未配置）；string=需持有；null=禁止（只读页） */
  createPermission?: string | null
  updatePermission?: string | null
  deletePermission?: string | null
  deleteFn?: (row: T) => Promise<void>
  importExport?: { moduleCode: string; permission?: string }
  emptyTextKey?: string
  renderForm: (ctx: {
    open: boolean
    editing: T | null
    onOpenChange: (o: boolean) => void
    onSaved: () => void
  }) => ReactNode
  /** 行内额外操作（如仓库行的“库位”入口） */
  rowExtraActions?: (row: T) => ReactNode
  extraToolbar?: ReactNode
}

export function MasterListPage<T extends { id: string }>({
  resource, titleKey, columns, listFn, commonSearchFields, keyword, defaultSort,
  createPermission, updatePermission, deletePermission, deleteFn, importExport,
  emptyTextKey, renderForm, rowExtraActions, extraToolbar,
}: MasterListPageProps<T>) {
  const { t } = useTranslation()
  const { hasPerm } = useAuth()
  const qc = useQueryClient()

  const [query, setQuery] = useState<ListQuery>({ page: 1, pageSize: 20, sort: defaultSort })
  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<T | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<T | null>(null)
  const [ieOpen, setIeOpen] = useState(false)
  const [deleting, setDeleting] = useState(false)

  const metaQuery = useQuery({
    queryKey: ['meta', resource],
    queryFn: () => apiMetaFields(resource),
  })
  const listQuery = useQuery({
    queryKey: [resource, query],
    queryFn: () => listFn(query),
  })

  const canCreate = createPermission === undefined || (createPermission !== null && hasPerm(createPermission))
  const canUpdate = updatePermission === undefined || (updatePermission !== null && hasPerm(updatePermission))
  const canDelete = deletePermission === undefined || (deletePermission !== null && hasPerm(deletePermission))
  const canImportExport = !importExport?.permission || hasPerm(importExport.permission)

  const refresh = () => {
    void qc.invalidateQueries({ queryKey: [resource] })
  }

  const handleSearch = (q: ListQuery) => {
    setQuery((prev) => ({ ...prev, ...q, page: 1 }))
  }

  const handleReset = () => {
    setQuery({ page: 1, pageSize: 20, sort: defaultSort })
  }

  const confirmDelete = async () => {
    if (!deleteTarget || !deleteFn) return
    setDeleting(true)
    try {
      await deleteFn(deleteTarget)
      toast.success(t('common.deleteSuccess'))
      refresh()
    } catch (e) {
      // 引用保护等错误：toast 后端 message，记录不消失（基础数据规格：状态矩阵）
      toast.error((e as Error).message)
    } finally {
      setDeleting(false)
      setDeleteTarget(null)
    }
  }

  // 操作列（查看/编辑/删除，右对齐；按权限与 rowExtraActions 显隐）
  const finalColumns = useMemo<ColumnDef<T>[]>(() => {
    const hasActions = canUpdate || canDelete || !!rowExtraActions
    if (!hasActions) return columns
    return [
      ...columns,
      {
        id: 'actions',
        header: t('common.actions'),
        meta: { align: 'right' },
        cell: ({ row }) => (
          <div className="flex items-center justify-end gap-1">
            {rowExtraActions?.(row.original)}
            {canUpdate && (
              <Button
                variant="ghost"
                size="sm"
                className="h-7 px-2 text-primary"
                onClick={() => { setEditing(row.original); setFormOpen(true) }}
                data-testid="btn-edit"
              >
                {t('common.edit')}
              </Button>
            )}
            {canDelete && (
              <Button
                variant="ghost"
                size="sm"
                className="h-7 px-2 text-destructive"
                onClick={() => setDeleteTarget(row.original)}
                data-testid="btn-delete"
              >
                {t('common.delete')}
              </Button>
            )}
          </div>
        ),
      },
    ]
  }, [columns, canUpdate, canDelete, rowExtraActions, t])

  const total = listQuery.data?.total ?? 0
  const filtered = listQuery.data?.items.length ?? 0

  return (
    <div className="space-y-3">
      {/* 标题 + 操作按钮（按权限显隐） */}
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-baseline gap-2">
          <h2 className="text-base font-semibold">{t(titleKey)}</h2>
          <span className="text-sm text-muted-foreground" data-testid="list-total">
            {t('common.total', { total })} · {t('common.filtered', { count: filtered })}
          </span>
        </div>
        <div className="flex items-center gap-2">
          {extraToolbar}
          {importExport && canImportExport && (
            <Button variant="outline" size="sm" onClick={() => setIeOpen(true)} data-testid="btn-import-export">
              {t('common.import')} / {t('common.export')}
            </Button>
          )}
          {canCreate && (
            <Button size="sm" onClick={() => { setEditing(null); setFormOpen(true) }} data-testid="btn-create">
              <Plus className="size-3.5" data-icon />
              {t('common.new')}
            </Button>
          )}
        </div>
      </div>

      {/* 筛选区（SearchField[] 元数据驱动） */}
      <SearchPanel
        resource={resource}
        fields={metaQuery.data}
        loading={metaQuery.isLoading}
        commonFields={commonSearchFields}
        keyword={keyword}
        onSearch={handleSearch}
        onReset={handleReset}
      />

      {/* 数据表 */}
      <DataTable
        columns={finalColumns}
        data={listQuery.data?.items ?? []}
        total={total}
        page={query.page ?? 1}
        pageSize={query.pageSize ?? 20}
        onPageChange={(page) => setQuery((prev) => ({ ...prev, page }))}
        sort={query.sort}
        onSortChange={(sort) => setQuery((prev) => ({ ...prev, sort, page: 1 }))}
        loading={listQuery.isLoading}
        error={listQuery.error ? (listQuery.error as Error).message : undefined}
        onRetry={() => listQuery.refetch()}
        emptyText={emptyTextKey ? t(emptyTextKey) : undefined}
      />

      {/* 新建/编辑 Dialog（由页面渲染具体表单） */}
      {renderForm({
        open: formOpen,
        editing,
        onOpenChange: setFormOpen,
        onSaved: refresh,
      })}

      {/* 删除确认（AlertDialog 二次确认） */}
      <AlertDialog open={!!deleteTarget} onOpenChange={(o) => !o && setDeleteTarget(null)}>
        <AlertDialogTrigger asChild>
          <span className="hidden" />
        </AlertDialogTrigger>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('common.deleteConfirmTitle')}</AlertDialogTitle>
            <AlertDialogDescription>
              {t('common.deleteConfirmDesc', { name: deleteTarget ? labelOf(deleteTarget) : '' })}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleting}>{t('common.cancel')}</AlertDialogCancel>
            <AlertDialogAction
              disabled={deleting}
              onClick={(e) => { e.preventDefault(); void confirmDelete() }}
              data-testid="confirm-delete"
            >
              <Trash2 className="size-3.5" data-icon />
              {t('common.delete')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* 导入导出弹窗（导出当前筛选结果） */}
      {importExport && (
        <ImportExportDialog
          moduleCode={importExport.moduleCode}
          open={ieOpen}
          onOpenChange={setIeOpen}
          listFilter={query.filter}
          listSort={query.sort}
        />
      )}
    </div>
  )
}

/** 行内 label（删除确认文案） */
function labelOf(row: { id: string }): string {
  const r = row as Record<string, unknown>
  return String(r.code ?? r.name ?? r.batchNo ?? r.id)
}
