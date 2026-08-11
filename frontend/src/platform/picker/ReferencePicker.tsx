/**
 * 通用 ReferencePicker —— ref 字段共用（通用规范 2.10）：
 * ① 快捷搜索：keyword 防抖调列表接口（pageSize=10），下拉候选；
 * ② 完整选择弹窗：标准列表（分页/筛选/排序，复用字段元数据与 filter DSL）。
 */
import { useQuery } from '@tanstack/react-query'
import type { ColumnDef } from '@tanstack/react-table'
import { ChevronsUpDown, ExternalLink, Search, X } from 'lucide-react'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { apiMetaFields } from '@/api'
import type { FieldMeta, ListQuery, SortSpec } from '@/api/types'
import { Button } from '@/components/ui/button'
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Skeleton } from '@/components/ui/skeleton'
import { buildListQuery } from '@/platform/filter/filter-dsl'
import type { SearchValues } from '@/platform/filter/filter-dsl'
import { SearchField } from '@/platform/filter/SearchField'
import { DataTable } from '@/platform/table/DataTable'
import { textColumn } from '@/platform/table/columns'
import { REF_RESOURCES } from './ref-resources'

export interface ReferencePickerProps {
  resource: string
  value: string | null | undefined
  onChange: (value: string | null) => void
  disabled?: boolean
  placeholder?: string
  className?: string
}

interface Candidate {
  id: string
  label: string
}

const RESOURCE_TITLE_KEYS: Record<string, string> = {
  materials: 'nav.material',
  warehouses: 'nav.warehouse',
  sources: 'nav.source',
  batches: 'nav.batch',
}

export function ReferencePicker({
  resource, value, onChange, disabled, placeholder, className,
}: ReferencePickerProps) {
  const { t } = useTranslation()
  const config = REF_RESOURCES[resource]
  const [open, setOpen] = useState(false)
  const [keyword, setKeyword] = useState('')
  const [dialogOpen, setDialogOpen] = useState(false)
  const [candidates, setCandidates] = useState<Candidate[]>([])
  const [searching, setSearching] = useState(false)
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const [labelCache, setLabelCache] = useState<Record<string, string>>({})

  const labelQuery = useQuery({
    queryKey: ['ref-label', resource, value],
    queryFn: async () => {
      if (!value) return null
      if (labelCache[value]) return labelCache[value]
      const item = await config.lookupById(value)
      return item ? config.display(item) : null
    },
    enabled: !!value && !labelCache[value],
  })

  const currentLabel = value ? (labelCache[value] ?? labelQuery.data) : null

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current)
    if (!open || !keyword.trim()) {
      setCandidates([])
      return
    }
    setSearching(true)
    debounceRef.current = setTimeout(async () => {
      try {
        const items = await config.quickSearch(keyword.trim())
        const mapped = items.map((i) => ({ id: (i as { id: string }).id, label: config.display(i) }))
        setCandidates(mapped)
        setLabelCache((prev) => {
          const next = { ...prev }
          mapped.forEach((c) => { next[c.id] = c.label })
          return next
        })
      } finally {
        setSearching(false)
      }
    }, 300)
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, keyword])

  const pick = (id: string, label: string) => {
    setLabelCache((prev) => ({ ...prev, [id]: label }))
    onChange(id)
    setOpen(false)
    setDialogOpen(false)
  }

  if (!config) return <span className="text-sm text-destructive">unknown ref resource: {resource}</span>

  return (
    <div className={className}>
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button
            type="button"
            variant="outline"
            role="combobox"
            disabled={disabled}
            className="w-full justify-between font-normal"
            data-testid={`ref-picker-${resource}`}
          >
            <span className="truncate text-left text-sm">
              {currentLabel ?? (placeholder ?? t('picker.searchPlaceholder'))}
            </span>
            <span className="flex shrink-0 items-center gap-1">
              {value && (
                <X
                  className="size-3.5 text-muted-foreground hover:text-destructive"
                  data-icon
                  onClick={(e) => {
                    e.stopPropagation()
                    onChange(null)
                  }}
                  aria-label={t('common.reset')}
                />
              )}
              <ChevronsUpDown className="size-3.5 text-muted-foreground" data-icon />
            </span>
          </Button>
        </PopoverTrigger>
        <PopoverContent className="w-[--radix-popover-trigger-width] p-2" align="start">
          <div className="relative">
            <Search className="absolute top-1/2 left-2 size-3.5 -translate-y-1/2 text-muted-foreground" data-icon />
            <Input
              autoFocus
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              placeholder={t('picker.searchPlaceholder')}
              className="pl-7"
              data-testid="ref-quick-search"
            />
          </div>
          <ScrollArea className="mt-2 max-h-56">
            {searching ? (
              <div className="space-y-1.5 p-1">
                {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-7 w-full" />)}
              </div>
            ) : candidates.length === 0 ? (
              <p className="p-2 text-center text-sm text-muted-foreground">{t('picker.noResult')}</p>
            ) : (
              <div className="space-y-0.5">
                {candidates.map((c) => (
                  <button
                    key={c.id}
                    type="button"
                    className="w-full rounded-md px-2 py-1.5 text-left text-sm hover:bg-accent"
                    onClick={() => pick(c.id, c.label)}
                  >
                    {c.label}
                  </button>
                ))}
              </div>
            )}
          </ScrollArea>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="mt-1 w-full justify-start gap-1 text-primary"
            onClick={() => { setOpen(false); setDialogOpen(true) }}
            data-testid="ref-open-full"
          >
            <ExternalLink className="size-3.5" data-icon />
            {t('picker.fullList')}
          </Button>
        </PopoverContent>
      </Popover>

      <RefPickerDialog
        resource={resource}
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        onPick={pick}
        onCacheLabels={(labels) => setLabelCache((prev) => ({ ...prev, ...labels }))}
      />
    </div>
  )
}

/** 完整选择弹窗：标准列表（分页 + SearchField 筛选） */
function RefPickerDialog({
  resource, open, onOpenChange, onPick, onCacheLabels,
}: {
  resource: string
  open: boolean
  onOpenChange: (o: boolean) => void
  onPick: (id: string, label: string) => void
  onCacheLabels: (labels: Record<string, string>) => void
}) {
  const { t } = useTranslation()
  const config = REF_RESOURCES[resource]
  const [query, setQuery] = useState<ListQuery>({ page: 1, pageSize: 10 })
  const [sort, setSort] = useState<SortSpec[]>([])

  const metaQuery = useQuery({
    queryKey: ['meta', resource],
    queryFn: () => apiMetaFields(resource),
    enabled: open,
  })
  const listQuery = useQuery({
    queryKey: ['ref-list', resource, query, sort, open],
    queryFn: async () => {
      const q = { ...query, sort: sort.length > 0 ? sort : undefined }
      const res = await config.listQuery(q)
      onCacheLabels(Object.fromEntries(res.items.map((i) => [(i as { id: string }).id, config.display(i)])))
      return res
    },
    enabled: open,
  })

  const fields = useMemo(() => metaQuery.data ?? [], [metaQuery.data])
  const commonFields = useMemo(() => fields.slice(0, 3).map((f) => f.field), [fields])

  const handleSearch = (q: ListQuery) => {
    setQuery((prev) => ({ ...prev, ...q, page: 1 }))
  }

  const rows = useMemo(
    () => (listQuery.data?.items ?? []).map((i) => ({ id: (i as { id: string }).id, label: config.display(i) })),
    [listQuery.data, config],
  )

  interface PickerRow { id: string; label: string }

  const columns = useMemo<ColumnDef<PickerRow>[]>(() => [
    textColumn<PickerRow>('label', t(`nav.${resource === 'materials' ? 'material' : resource === 'warehouses' ? 'warehouse' : resource === 'sources' ? 'source' : 'batch'}`) as string),
    {
      id: 'pick',
      header: '',
      meta: { align: 'right' },
      cell: ({ row }) => (
        <Button
          size="sm"
          variant="outline"
          onClick={() => onPick(row.original.id, row.original.label)}
          data-testid="ref-pick-row"
        >
          {t('common.confirm')}
        </Button>
      ),
    },
  ], [t, onPick, resource])

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>{t('picker.fullList')} · {t(RESOURCE_TITLE_KEYS[resource] ?? 'nav.material')}</DialogTitle>
        </DialogHeader>
        <SearchPanelInline
          resource={resource}
          fields={fields}
          commonFields={commonFields}
          onSearch={handleSearch}
          onReset={() => setQuery({ page: 1, pageSize: 10 })}
        />
        <DataTable
          columns={columns}
          data={rows}
          total={listQuery.data?.total ?? 0}
          page={query.page ?? 1}
          pageSize={query.pageSize ?? 10}
          onPageChange={(page) => setQuery((prev) => ({ ...prev, page }))}
          sort={sort}
          onSortChange={setSort}
          loading={listQuery.isLoading || metaQuery.isLoading}
          error={listQuery.error ? (listQuery.error as Error).message : undefined}
          onRetry={() => listQuery.refetch()}
          emptyText={t('picker.noResult')}
        />
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            {t('common.cancel')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/** 弹窗内嵌筛选（复用运行时元数据 + filter DSL） */
function SearchPanelInline({
  resource, fields, commonFields, onSearch, onReset,
}: {
  resource: string
  fields: FieldMeta[]
  commonFields: string[]
  onSearch: (q: ListQuery) => void
  onReset: () => void
}) {
  const { t } = useTranslation()
  const [values, setValues] = useState<SearchValues>({})
  const [keyword, setKeyword] = useState('')

  const search = () => {
    onSearch(buildListQuery({ resource, values, fields, keyword }))
  }

  return (
    <div className="space-y-2 rounded-lg border bg-muted/30 p-3">
      <div className="flex flex-wrap items-center gap-2">
        <Input
          value={keyword}
          onChange={(e) => setKeyword(e.target.value)}
          placeholder={t('filter.keywordPlaceholder')}
          className="w-52"
          data-testid="ref-dialog-keyword"
        />
        <Button size="sm" onClick={search} data-testid="ref-dialog-search">{t('common.query')}</Button>
        <Button
          size="sm"
          variant="outline"
          onClick={() => { setValues({}); setKeyword(''); onReset() }}
        >
          {t('common.reset')}
        </Button>
      </div>
      <div className="grid grid-cols-2 gap-2 md:grid-cols-3">
        {fields.map((meta) => (
          <SearchField
            key={meta.field}
            meta={meta}
            value={values[meta.field]}
            onChange={(c) => setValues((prev) => ({ ...prev, [meta.field]: c }))}
            compact={!commonFields.includes(meta.field)}
          />
        ))}
      </div>
    </div>
  )
}
