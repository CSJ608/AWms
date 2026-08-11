/**
 * 仓库 新建/编辑 Dialog（仓库库位契约 v0.2：code 只读；mgmtMode 仅新建时可设）。
 */
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useEffect, useMemo } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { z } from 'zod'
import { apiCreateWarehouse, apiUpdateWarehouse } from '@/api'
import type { WarehouseItem } from '@/api/types'
import { MATERIAL_STATUSES, WAREHOUSE_MGMT_MODES } from '@/api/types'
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
import { genIdempotencyKey } from '@/platform/format'

export interface WarehouseFormDialogProps {
  open: boolean
  editing: WarehouseItem | null
  onOpenChange: (open: boolean) => void
  onSaved: () => void
}

export function WarehouseFormDialog({ open, editing, onOpenChange, onSaved }: WarehouseFormDialogProps) {
  const { t } = useTranslation()

  const schema = useMemo(() => z.object({
    code: z.string().trim().min(1, t('warehouses.codeRequired')).max(64),
    name: z.string().trim().min(1, t('warehouses.nameRequired')).max(128),
    searchCode: z.string().trim().max(32),
    status: z.enum(MATERIAL_STATUSES),
    mgmtMode: z.enum(WAREHOUSE_MGMT_MODES),
  }), [t])

  type FormValues = z.infer<typeof schema>

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { code: '', name: '', searchCode: '', status: 'ENABLED', mgmtMode: 'MANUAL' },
  })

  // 打开时按 新建/编辑 重置为对应初值（验收⑥：新建不保留上次输入；编辑回填不串）
  useEffect(() => {
    if (!open) return
    form.reset(editing
      ? {
          code: editing.code,
          name: editing.name,
          searchCode: editing.searchCode ?? '',
          status: editing.status,
          mgmtMode: editing.mgmtMode,
        }
      : { code: '', name: '', searchCode: '', status: 'ENABLED', mgmtMode: 'MANUAL' })
  }, [open, editing, form])

  const mutation = useMutation({
    mutationFn: (v: FormValues): Promise<unknown> => {
      if (editing) {
        return apiUpdateWarehouse(editing.id, {
          name: v.name, searchCode: v.searchCode || null, status: v.status,
        })
      }
      return apiCreateWarehouse(
        { code: v.code, name: v.name, searchCode: v.searchCode || null, status: v.status, mgmtMode: v.mgmtMode },
        genIdempotencyKey(),
      )
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
          <DialogTitle>{editing ? t('warehouses.editTitle') : t('warehouses.createTitle')}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit((v) => mutation.mutate(v))} className="space-y-4" data-testid="warehouse-form">
            <FormField
              control={form.control}
              name="code"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('warehouses.code')}</FormLabel>
                  <FormControl>
                    <Input {...field} disabled={!!editing} data-testid="f-code" />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('warehouses.name')}</FormLabel>
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
                  <FormLabel>{t('warehouses.searchCode')}</FormLabel>
                  <FormControl>
                    <Input {...field} data-testid="f-searchCode" />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <div className="grid grid-cols-2 gap-3">
              {!editing && (
                <FormField
                  control={form.control}
                  name="mgmtMode"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{t('warehouses.mgmtMode')}</FormLabel>
                      <FormControl>
                        <Select value={field.value} onValueChange={field.onChange}>
                          <SelectTrigger data-testid="f-mgmtMode"><SelectValue /></SelectTrigger>
                          <SelectContent>
                            {WAREHOUSE_MGMT_MODES.map((v) => (
                              <SelectItem key={v} value={v}>{t(`enums.mgmtMode.${v.toLowerCase()}`)}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              )}
              <FormField
                control={form.control}
                name="status"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('warehouses.status')}</FormLabel>
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
            </div>
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
