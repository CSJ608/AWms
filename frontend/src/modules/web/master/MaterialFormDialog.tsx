/**
 * 物料 新建/编辑 Dialog —— RHF + zod（校验失败 inline）；编码创建后不可改（契约 v0.3）；
 * 数量 decimal 字符串提交；Idempotency-Key 按用户动作粒度（重试沿用同一 key）。
 */
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useEffect, useMemo } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { z } from 'zod'
import { apiCreateMaterial, apiUpdateMaterial } from '@/api'
import type { MaterialItem } from '@/api/types'
import { LABEL_TYPES, MATERIAL_STATUSES, UOMS } from '@/api/types'
import { Button } from '@/components/ui/button'
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import {
  Form, FormControl, FormField, FormItem, FormLabel, FormMessage,
} from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select'
import { Switch } from '@/components/ui/switch'
import { genIdempotencyKey } from '@/platform/format'

export interface MaterialFormDialogProps {
  open: boolean
  editing: MaterialItem | null
  onOpenChange: (open: boolean) => void
  onSaved: () => void
}

const DEFAULT_VALUES = {
  code: '',
  name: '',
  searchCode: '',
  batchControlled: false,
  labelType: 'NONE',
  defaultUom: 'CT',
  defaultQtyPerLabel: '',
  status: 'ENABLED',
} as const

export function MaterialFormDialog({ open, editing, onOpenChange, onSaved }: MaterialFormDialogProps) {
  const { t } = useTranslation()

  const schema = useMemo(() => z.object({
    code: z.string().trim().min(1, t('materials.codeRequired')).max(64, t('materials.codeLen')),
    name: z.string().trim().min(1, t('materials.nameRequired')).max(128, t('materials.nameLen')),
    searchCode: z.string().trim().max(32, t('materials.searchCodeLen')),
    batchControlled: z.boolean(),
    labelType: z.enum(LABEL_TYPES),
    defaultUom: z.enum(UOMS),
    defaultQtyPerLabel: z.string().refine(
      (v) => v === '' || (Number.isFinite(Number(v)) && Number(v) > 0),
      t('materials.qtyPositive'),
    ),
    status: z.enum(MATERIAL_STATUSES),
  }), [t])

  type FormValues = z.infer<typeof schema>

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: DEFAULT_VALUES,
  })

  // 打开时按 新建/编辑 重置为对应初值（验收⑥：新建不保留上次输入；编辑回填不串；
  // 用显式 reset 而非 values prop——values 对内容相同的新对象做深比较不触发 reset，
  // 且对话框关闭时内容不卸载，表单状态会跨次打开残留）
  useEffect(() => {
    if (!open) return
    form.reset(editing
      ? {
          code: editing.code,
          name: editing.name,
          searchCode: editing.searchCode ?? '',
          batchControlled: editing.batchControlled,
          labelType: editing.labelType,
          defaultUom: editing.defaultUom,
          defaultQtyPerLabel: editing.defaultQtyPerLabel ?? '',
          status: editing.status,
        }
      : { ...DEFAULT_VALUES })
  }, [open, editing, form])

  const mutation = useMutation({
    mutationFn: (v: FormValues): Promise<unknown> => {
      const base = {
        name: v.name,
        searchCode: v.searchCode || null,
        batchControlled: v.batchControlled,
        labelType: v.labelType,
        defaultUom: v.defaultUom,
        defaultQtyPerLabel: v.defaultQtyPerLabel || null,
      }
      if (editing) {
        return apiUpdateMaterial(editing.id, { ...base, status: v.status })
      }
      // 幂等键按“用户动作”粒度：重试沿用同一 key（评审 A-13）
      return apiCreateMaterial({ ...base, code: v.code, status: v.status }, genIdempotencyKey())
    },
    onSuccess: () => {
      toast.success(t('common.saveSuccess'))
      onSaved()
      onOpenChange(false)
    },
    onError: (e) => toast.error((e as Error).message),
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{editing ? t('materials.editTitle') : t('materials.createTitle')}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit((v) => mutation.mutate(v))} className="space-y-4" data-testid="material-form">
            <FormField
              control={form.control}
              name="code"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('materials.code')}</FormLabel>
                  <FormControl>
                    <Input {...field} disabled={!!editing} data-testid="f-code" />
                  </FormControl>
                  {editing ? (
                    <p className="text-xs text-muted-foreground">{t('materials.codeReadonlyHint')}</p>
                  ) : (
                    <FormMessage />
                  )}
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('materials.name')}</FormLabel>
                  <FormControl>
                    <Input {...field} data-testid="f-name" />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="searchCode"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('materials.searchCode')}</FormLabel>
                  <FormControl>
                    <Input {...field} data-testid="f-searchCode" />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="batchControlled"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between rounded-lg border p-3">
                  <FormLabel className="cursor-pointer">{t('materials.batchControlled')}</FormLabel>
                  <FormControl>
                    <Switch checked={field.value} onCheckedChange={field.onChange} data-testid="f-batchControlled" />
                  </FormControl>
                </FormItem>
              )}
            />
            <div className="grid grid-cols-2 gap-3">
              <FormField
                control={form.control}
                name="labelType"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('materials.labelType')}</FormLabel>
                    <FormControl>
                      <Select value={field.value} onValueChange={field.onChange}>
                        <SelectTrigger data-testid="f-labelType"><SelectValue /></SelectTrigger>
                        <SelectContent>
                          {LABEL_TYPES.map((v) => (
                            <SelectItem key={v} value={v}>{t(`enums.labelType.${v.toLowerCase()}`)}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="defaultUom"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('materials.defaultUom')}</FormLabel>
                    <FormControl>
                      <Select value={field.value} onValueChange={field.onChange}>
                        <SelectTrigger data-testid="f-defaultUom"><SelectValue /></SelectTrigger>
                        <SelectContent>
                          {UOMS.map((v) => (
                            <SelectItem key={v} value={v}>{t(`enums.uom.${v}`)}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>
            <FormField
              control={form.control}
              name="defaultQtyPerLabel"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('materials.defaultQtyPerLabel')}</FormLabel>
                  <FormControl>
                    <Input {...field} type="number" step="0.0001" className="tabular-nums" data-testid="f-defaultQtyPerLabel" />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="status"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('materials.status')}</FormLabel>
                  <FormControl>
                    <Select value={field.value} onValueChange={field.onChange}>
                      <SelectTrigger data-testid="f-status"><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {MATERIAL_STATUSES.map((v) => (
                          <SelectItem key={v} value={v}>{t(`enums.status.${v.toLowerCase()}`)}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
                {t('common.cancel')}
              </Button>
              <Button type="submit" disabled={mutation.isPending} data-testid="form-submit">
                {t('common.save')}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  )
}
