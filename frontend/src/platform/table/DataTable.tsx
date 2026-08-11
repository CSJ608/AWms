/**
 * 通用 DataTable —— 列定义驱动 + 服务端分页 + 排序 + 加载骨架/空态/错误条（通用列表页规格）。
 */
import type { SortingState } from '@tanstack/react-table'
import {
  flexRender, getCoreRowModel, useReactTable,
} from '@tanstack/react-table'
import type { ColumnDef } from '@tanstack/react-table'
import { ChevronDown, ChevronUp, ChevronsUpDown } from 'lucide-react'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import type { SortSpec } from '@/api/types'

export interface DataTableProps<T> {
  columns: ColumnDef<T>[]
  data: T[]
  total: number
  page: number
  pageSize: number
  onPageChange: (page: number) => void
  sort?: SortSpec[]
  onSortChange?: (sort: SortSpec[]) => void
  loading?: boolean
  error?: string | null
  onRetry?: () => void
  emptyText?: string
}

export function DataTable<T>({
  columns, data, total, page, pageSize, onPageChange, sort, onSortChange, loading, error, onRetry, emptyText,
}: DataTableProps<T>) {
  const { t } = useTranslation()

  const sorting: SortingState = useMemo(
    () => (sort ?? []).map((s) => ({ id: s.field, desc: s.dir === 'desc' })),
    [sort],
  )

  const table = useReactTable({
    data,
    columns,
    state: { sorting },
    getCoreRowModel: getCoreRowModel(),
    manualPagination: true,
    manualSorting: true,
    pageCount: pageSize > 0 ? Math.max(1, Math.ceil(total / pageSize)) : 1,
  })

  const pages = pageSize > 0 ? Math.max(1, Math.ceil(total / pageSize)) : 1

  const toggleSort = (field: string) => {
    if (!onSortChange) return
    const cur = sort?.find((s) => s.field === field)
    if (!cur) onSortChange([{ field, dir: 'asc' }])
    else if (cur.dir === 'asc') onSortChange([{ field, dir: 'desc' }])
    else onSortChange([])
  }

  return (
    <div className="w-full">
      <div className="rounded-lg border bg-card">
        <Table>
          <TableHeader>
            {table.getHeaderGroups().map((headerGroup) => (
              <TableRow key={headerGroup.id} className="hover:bg-transparent">
                {headerGroup.headers.map((header) => {
                  const meta = header.column.columnDef.meta as { sortable?: boolean; align?: 'right' } | undefined
                  const sortable = meta?.sortable ?? false
                  const cur = sort?.find((s) => s.field === header.column.id)
                  return (
                    <TableHead
                      key={header.id}
                      className={cn('h-10', meta?.align === 'right' && 'text-right', sortable && 'cursor-pointer select-none')}
                      onClick={sortable ? () => toggleSort(header.column.id) : undefined}
                      data-testid={`th-${header.column.id}`}
                    >
                      <span className={cn('inline-flex items-center gap-1', meta?.align === 'right' && 'flex-row-reverse')}>
                        {header.isPlaceholder
                          ? null
                          : flexRender(header.column.columnDef.header, header.getContext())}
                        {sortable && (cur
                          ? (cur.dir === 'asc' ? <ChevronUp className="size-3.5" data-icon /> : <ChevronDown className="size-3.5" data-icon />)
                          : <ChevronsUpDown className="size-3.5 text-muted-foreground" data-icon />)}
                      </span>
                    </TableHead>
                  )
                })}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody>
            {loading ? (
              Array.from({ length: Math.min(pageSize || 10, 10) }).map((_, i) => (
                <TableRow key={`sk-${i}`}>
                  {columns.map((c) => (
                    <TableCell key={`${String((c as { id?: string }).id ?? (c as { accessorKey?: string }).accessorKey ?? i)}-${i}`} className="py-2.5">
                      <Skeleton className="h-4 w-24" />
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : error ? (
              <TableRow>
                <TableCell colSpan={columns.length} className="h-24 text-center">
                  <div className="flex flex-col items-center gap-2 text-muted-foreground">
                    <span>{error}</span>
                    {onRetry && (
                      <Button variant="outline" size="sm" onClick={onRetry}>
                        {t('common.retry')}
                      </Button>
                    )}
                  </div>
                </TableCell>
              </TableRow>
            ) : data.length === 0 ? (
              <TableRow>
                <TableCell colSpan={columns.length} className="h-24 text-center text-muted-foreground">
                  {emptyText ?? t('common.empty')}
                </TableCell>
              </TableRow>
            ) : (
              table.getRowModel().rows.map((row) => (
                <TableRow key={row.id} data-state={row.getIsSelected() && 'selected'}>
                  {row.getVisibleCells().map((cell) => (
                    <TableCell key={cell.id} className={cn('py-2', (cell.column.columnDef.meta as { align?: string } | undefined)?.align === 'right' && 'text-right')}>
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {/* 分页（通用列表页规格：第 x / y 页 · 每页 n 条） */}
      {!loading && !error && total > 0 && (
        <div className="mt-3 flex items-center justify-between text-sm text-muted-foreground">
          <span>
            {t('common.pageInfo', { page, pages, pageSize })}
          </span>
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={page <= 1}
              onClick={() => onPageChange(page - 1)}
            >
              {t('common.prevPage')}
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={page >= pages}
              onClick={() => onPageChange(page + 1)}
            >
              {t('common.nextPage')}
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}
