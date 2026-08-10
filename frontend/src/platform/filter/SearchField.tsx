/**
 * SearchField —— 运行时元数据（FieldMeta）驱动的单个筛选控件（通用规范 2.10）：
 * type 推导控件：string→文本框；enum→下拉；bool→开关/下拉；number/decimal→数字输入（可区间）；
 * date/datetime→日期选择器（可区间）；ref→引用选择器。
 */
import { useTranslation } from 'react-i18next'
import type { FieldMeta, FilterOp } from '@/api/types'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select'
import { DEFAULT_OP } from './filter-dsl'
import type { SearchCondition } from './filter-dsl'
import { ReferencePicker } from '@/platform/picker/ReferencePicker'

export interface SearchFieldProps {
  meta: FieldMeta
  value?: SearchCondition
  onChange: (cond: SearchCondition) => void
  /** 紧凑模式（高级筛选区）：隐藏操作符选择，用默认操作符 */
  compact?: boolean
}

export function SearchField({ meta, value, onChange, compact }: SearchFieldProps) {
  const { t } = useTranslation()
  const op = value?.op ?? defaultOp(meta)
  const val = value?.value

  const setOp = (nextOp: FilterOp) => {
    // 操作符切换时清空值（避免类型不匹配的值残留在 DSL 里）
    onChange({ op: nextOp, value: nextOp === 'isNull' || nextOp === 'isNotNull' ? null : defaultValueFor(meta, nextOp) })
  }

  const setVal = (nextVal: SearchCondition['value']) => {
    onChange({ op, value: nextVal })
  }

  const label = t(meta.labelKey)

  return (
    <div className="flex items-center gap-1.5" data-testid={`sf-${meta.field}`}>
      <Label className="w-20 shrink-0 truncate text-xs text-muted-foreground" htmlFor={`sf-${meta.field}`}>
        {label}
      </Label>
      {!compact && meta.operators.length > 1 && (
        <Select value={op} onValueChange={(v) => setOp(v as FilterOp)}>
          <SelectTrigger className="h-8 w-20 shrink-0" data-testid={`sf-${meta.field}-op`}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {meta.operators.map((o) => (
              <SelectItem key={o} value={o}>{o}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      )}
      <div className="min-w-36 flex-1">
        <FieldControl meta={meta} op={op} value={val} onChange={setVal} />
      </div>
    </div>
  )
}

function defaultOp(meta: FieldMeta): FilterOp {
  const d = DEFAULT_OP[meta.type]
  return meta.operators.includes(d) ? d : meta.operators[0]
}

function defaultValueFor(_meta: FieldMeta, op: FilterOp): SearchCondition['value'] {
  if (op === 'between') return ['', '']
  if (op === 'isNull' || op === 'isNotNull') return null
  return ''
}

function FieldControl({
  meta, op, value, onChange,
}: {
  meta: FieldMeta
  op: FilterOp
  value: SearchCondition['value'] | undefined
  onChange: (v: SearchCondition['value']) => void
}) {
  const { t } = useTranslation()
  const id = `sf-${meta.field}`

  if (op === 'isNull' || op === 'isNotNull') {
    return <div className="h-8" />
  }

  // 区间：两个输入
  if (op === 'between') {
    const pair = Array.isArray(value) ? value : ['', '']
    const cls = inputCls(meta)
    return (
      <div className="flex items-center gap-1">
        <Input
          id={id}
          className={cls}
          type={inputType(meta)}
          value={String(pair[0] ?? '')}
          placeholder={t('filter.datePlaceholder')}
          onChange={(e) => onChange([e.target.value, String(pair[1] ?? '')])}
        />
        <span className="text-muted-foreground">~</span>
        <Input
          className={cls}
          type={inputType(meta)}
          value={String(pair[1] ?? '')}
          placeholder={t('filter.datePlaceholder')}
          onChange={(e) => onChange([String(pair[0] ?? ''), e.target.value])}
        />
      </div>
    )
  }

  switch (meta.type) {
    case 'enum': {
      const opts = meta.options ?? []
      return (
        <Select value={value ? String(value) : ''} onValueChange={(v) => onChange(v)}>
          <SelectTrigger className="h-8 w-full" id={id} data-testid={`sf-${meta.field}-select`}>
            <SelectValue placeholder={t('filter.selectPlaceholder', { label: t(meta.labelKey) })} />
          </SelectTrigger>
          <SelectContent>
            {opts.map((o) => (
              <SelectItem key={o.value} value={o.value}>{t(o.labelKey)}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      )
    }
    case 'bool': {
      return (
        <Select value={value === undefined || value === '' ? '' : value ? 'true' : 'false'} onValueChange={(v) => onChange(v === 'true')}>
          <SelectTrigger className="h-8 w-full" id={id} data-testid={`sf-${meta.field}-select`}>
            <SelectValue placeholder={t('filter.selectPlaceholder', { label: t(meta.labelKey) })} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="true">{t('common.yes')}</SelectItem>
            <SelectItem value="false">{t('common.no')}</SelectItem>
          </SelectContent>
        </Select>
      )
    }
    case 'ref': {
      return (
        <ReferencePicker
          resource={meta.refResource ?? 'materials'}
          value={value ? String(value) : null}
          onChange={(v) => onChange(v)}
          placeholder={t('filter.selectPlaceholder', { label: t(meta.labelKey) })}
        />
      )
    }
    default: {
      return (
        <Input
          id={id}
          className={inputCls(meta)}
          type={inputType(meta)}
          step={meta.type === 'decimal' ? '0.0001' : undefined}
          value={value === null || value === undefined ? '' : String(value)}
          placeholder={t('filter.placeholder', { label: t(meta.labelKey) })}
          onChange={(e) => onChange(e.target.value)}
          data-testid={`sf-${meta.field}-input`}
        />
      )
    }
  }
}

function inputType(meta: FieldMeta): string {
  switch (meta.type) {
    case 'number':
    case 'decimal': return 'number'
    case 'date': return 'date'
    case 'datetime': return 'datetime-local'
    default: return 'text'
  }
}

function inputCls(meta: FieldMeta): string {
  if (meta.type === 'date' || meta.type === 'datetime') return 'h-8 w-full'
  return 'h-8 w-full tabular-nums'
}
