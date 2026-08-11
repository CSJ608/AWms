/**
 * 通用列辅助 —— 状态徽章 / 枚举 / 数量（等宽数字）/ 日期（通用列表页规格 + 视觉规范）。
 * 枚举单元格：labels.ts 给出 i18n key → t() 翻译；未知值透传原码（契约扩展不炸）。
 */
import type { ColumnDef } from '@tanstack/react-table'
import { Check, X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui/badge'
import { enumLabelKey, statusVariant } from '@/platform/labels'
import { formatDate, formatDateTime, formatQuantity } from '@/platform/format'

/** 枚举值单元格（翻译 key；未知值透传原码） */
function EnumValueCell({ enumName, value }: { enumName: string; value: string | null | undefined }) {
  const { t } = useTranslation()
  const key = enumLabelKey(enumName, value)
  return <span>{key ? t(key) : (value ?? '-')}</span>
}

/** 状态徽章单元格（语义色：ENABLED/ACTIVE → success，其余 neutral；status/batchStatus 双枚举回退） */
function StatusBadgeCell({ value }: { value: string | null | undefined }) {
  const { t } = useTranslation()
  const key = enumLabelKey('status', value) ?? enumLabelKey('batchStatus', value)
  return (
    <Badge variant={statusVariant(value) as 'success' | 'neutral'} data-testid="badge-status">
      {key ? t(key) : (value ?? '-')}
    </Badge>
  )
}

/** 语义色徽章列 */
export function statusColumn<T>(accessor: keyof T & string, headerKey: string, sortable = false): ColumnDef<T> {
  return {
    id: accessor,
    accessorFn: (row) => row[accessor] as string | null | undefined,
    header: headerKey,
    meta: { sortable },
    cell: ({ row }) => <StatusBadgeCell value={row.original[accessor] as string | null | undefined} />,
  }
}

/** 通用枚举列 */
export function enumColumn<T>(accessor: keyof T & string, headerKey: string, enumName: string, sortable = false): ColumnDef<T> {
  return {
    id: accessor,
    accessorFn: (row) => row[accessor] as string | null | undefined,
    header: headerKey,
    meta: { sortable },
    cell: ({ row }) => <EnumValueCell enumName={enumName} value={row.original[accessor] as string | null | undefined} />,
  }
}

/** 布尔列（✓ / ✗，如批控） */
export function boolColumn<T>(accessor: keyof T & string, headerKey: string, sortable = false): ColumnDef<T> {
  return {
    id: accessor,
    accessorFn: (row) => row[accessor] as boolean,
    header: headerKey,
    meta: { sortable },
    cell: ({ row }) => {
      const v = row.original[accessor]
      return v
        ? <Check className="size-4 text-success" data-icon aria-label="true" />
        : <X className="size-4 text-muted-foreground" data-icon aria-label="false" />
    },
  }
}

/** 数量列（decimal 字符串去尾零 + 等宽数字，契约 2.3） */
export function quantityColumn<T>(accessor: keyof T & string, headerKey: string, sortable = false): ColumnDef<T> {
  return {
    id: accessor,
    accessorFn: (row) => row[accessor] as string | number | null | undefined,
    header: headerKey,
    meta: { sortable, align: 'right' },
    cell: ({ row }) => {
      const v = row.original[accessor] as string | number | null | undefined
      return <span className="tabular-nums">{formatQuantity(v)}</span>
    },
  }
}

/** 日期时间列（ISO UTC → 本地） */
export function dateTimeColumn<T>(accessor: keyof T & string, headerKey: string, sortable = false): ColumnDef<T> {
  return {
    id: accessor,
    accessorFn: (row) => row[accessor] as string | null | undefined,
    header: headerKey,
    meta: { sortable },
    cell: ({ row }) => {
      const v = row.original[accessor] as string | null | undefined
      return <span className="tabular-nums">{formatDateTime(v)}</span>
    },
  }
}

/** 纯日期列 */
export function dateColumn<T>(accessor: keyof T & string, headerKey: string, sortable = false): ColumnDef<T> {
  return {
    id: accessor,
    accessorFn: (row) => row[accessor] as string | null | undefined,
    header: headerKey,
    meta: { sortable },
    cell: ({ row }) => {
      const v = row.original[accessor] as string | null | undefined
      return <span className="tabular-nums">{formatDate(v)}</span>
    },
  }
}

/** 文本列 */
export function textColumn<T>(accessor: keyof T & string, headerKey: string, sortable = false, className?: string): ColumnDef<T> {
  return {
    id: accessor,
    accessorFn: (row) => String(row[accessor] ?? '-'),
    header: headerKey,
    meta: { sortable },
    cell: ({ row }) => <span className={className}>{String(row.original[accessor] ?? '-')}</span>,
  }
}
