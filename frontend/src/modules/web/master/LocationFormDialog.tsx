/**
 * 库位 新建/编辑 Dialog（仓库库位契约 v0.2：code 仓内唯一只读；type 必填）。
 */
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useEffect, useMemo } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { z } from 'zod'
import { apiCreateLocation, apiUpdateLocation } from '@/api'
import type { LocationItem } from '@/api/types'
import { LOCATION_TYPES, MATERIAL_STATUSES } from '@/api/types'
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

export interface LocationFormDialogProps {
  warehouseId: string
  open: boolean
  editing: LocationItem | null
  onOpenChange: (open: boolean) => void
  onSaved: () => void
}

export function LocationFormDialog({ warehouseId, open, editing, onOpenChange, onSaved }: LocationFormDialogProps) {
  const { t } = useTranslation()

  const schema = useMemo(() => z.object({
    code: z.string().trim().min(1, t('locations.codeRequired')).max(64),
    searchCode: z.string().trim().max(32),
    type: z.enum(LOCATION_TYPES, { message: t('locations.typeRequired') }),
    status: z.enum(MATERIAL_STATUSES),
  }), [t])

  type FormValues = z.infer<typeof schema>

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { code: '', searchCode: '', type: 'STAGING', status: 'ENABLED' },
  })

  // 打开时按 新建/编辑 重置为对应初值（验收⑥：新建不保留上次输入；编辑回填不串）
  useEffect(() => {
    if (!open) return
    form.reset(editing
      ? {
          code: editing.code,
          searchCode: editing.searchCode ?? '',
          type: editing.type,
          status: editing.status,
        }
      : { code: '', searchCode: '', type: 'STAGING', status: 'ENABLED' })
  }, [open, editing, form])

  const mutation = useMutation({
    mutationFn: (v: FormValues): Promise<unknown> => {
      if (editing) {
        return apiUpdateLocation(editing.id, {
          type: v.type, searchCode: v.searchCode || null, status: v.status,
        })
      }
      return apiCreateLocation(
        warehouseId,
        { code: v.code, searchCode: v.searchCode || null, type: v.type, status: v.status },
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
          <DialogTitle>{editing ? t('locations.editTitle') : t('locations.createTitle')}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit((v) => mutation.mutate(v))} className="space-y-4" data-testid="location-form">
            <FormField
              control={form.control}
              name="code"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('locations.code')}</FormLabel>
                  <FormControl>
                    <Input {...field} disabled={!!editing} data-testid="f-code" />
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
                  <FormLabel>{t('locations.searchCode')}</FormLabel>
                  <FormControl>
                    <Input {...field} data-testid="f-searchCode" />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <div className="grid grid-cols-2 gap-3">
              <FormField
                control={form.control}
                name="type"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('locations.type')}</FormLabel>
                    <FormControl>
                      <Select value={field.value} onValueChange={field.onChange}>
                        <SelectTrigger data-testid="f-type"><SelectValue /></SelectTrigger>
                        <SelectContent>
                          {LOCATION_TYPES.map((v) => (
                            <SelectItem key={v} value={v}>{t(`enums.locationType.${v.toLowerCase()}`)}</SelectItem>
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
                name="status"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('locations.status')}</FormLabel>
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
