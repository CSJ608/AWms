/**
 * SearchPanel —— 通用筛选区（SearchField[] 元数据驱动）：
 * 常用字段默认展开（2-3 个），其余“高级筛选”收起（通用列表页规格）；查询/重置。
 */
import { ChevronDown, ChevronUp } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { FieldMeta, ListQuery } from '@/api/types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { buildListQuery } from './filter-dsl'
import type { SearchValues } from './filter-dsl'
import { SearchField } from './SearchField'

export interface SearchPanelProps {
  resource: string
  fields?: FieldMeta[]
  loading?: boolean
  /** 默认展开的字段（其余进高级筛选） */
  commonFields?: string[]
  /** 是否显示 keyword 快捷搜索框（匹配 code/name/searchCode） */
  keyword?: boolean
  onSearch: (query: ListQuery) => void
  onReset: () => void
}

export function SearchPanel({
  resource, fields = [], loading, commonFields = [], keyword = false, onSearch, onReset,
}: SearchPanelProps) {
  const { t } = useTranslation()
  const [values, setValues] = useState<SearchValues>({})
  const [kw, setKw] = useState('')
  const [advancedOpen, setAdvancedOpen] = useState(false)

  const common = fields.filter((f) => commonFields.includes(f.field))
  const advanced = fields.filter((f) => !commonFields.includes(f.field))

  const search = () => {
    onSearch(buildListQuery({ resource, values, fields, keyword: kw }))
  }

  const reset = () => {
    setValues({})
    setKw('')
    onReset()
  }

  return (
    <div className="space-y-2.5 rounded-lg border bg-card p-3">
      <div className="flex flex-wrap items-center gap-2">
        {keyword && (
          <div className="flex items-center gap-1.5">
            <span className="w-20 shrink-0 text-xs text-muted-foreground">{t('filter.keyword')}</span>
            <Input
              value={kw}
              onChange={(e) => setKw(e.target.value)}
              placeholder={t('filter.keywordPlaceholder')}
              className="h-8 w-52"
              data-testid="search-keyword"
            />
          </div>
        )}
        <Button size="sm" onClick={search} data-testid="search-submit">
          {t('common.query')}
        </Button>
        <Button size="sm" variant="outline" onClick={reset} data-testid="search-reset">
          {t('common.reset')}
        </Button>
        {advanced.length > 0 && (
          <Button
            size="sm"
            variant="ghost"
            className="gap-1 text-muted-foreground"
            onClick={() => setAdvancedOpen((v) => !v)}
            data-testid="search-advanced-toggle"
          >
            {advancedOpen ? t('common.collapse') : t('common.advanced')}
            {advancedOpen ? <ChevronUp className="size-3.5" data-icon /> : <ChevronDown className="size-3.5" data-icon />}
          </Button>
        )}
      </div>

      {loading && fields.length === 0 ? (
        <p className="text-xs text-muted-foreground">{t('common.loading')}</p>
      ) : (
        <>
          {common.length > 0 && (
            <div className="grid grid-cols-1 gap-2 md:grid-cols-2 xl:grid-cols-3">
              {common.map((meta) => (
                <SearchField
                  key={meta.field}
                  meta={meta}
                  value={values[meta.field]}
                  onChange={(c) => setValues((prev) => ({ ...prev, [meta.field]: c }))}
                />
              ))}
            </div>
          )}
          {advancedOpen && advanced.length > 0 && (
            <div className="grid grid-cols-1 gap-2 border-t pt-2 md:grid-cols-2 xl:grid-cols-3" data-testid="search-advanced">
              {advanced.map((meta) => (
                <SearchField
                  key={meta.field}
                  meta={meta}
                  value={values[meta.field]}
                  onChange={(c) => setValues((prev) => ({ ...prev, [meta.field]: c }))}
                />
              ))}
            </div>
          )}
        </>
      )}
    </div>
  )
}
