/**
 * 来源 新建/编辑 Dialog（来源契约 v0.2：type/code 只读，仅新建时可设）。
 */
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useMemo } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { z } from 'zod'
import { apiCreateSource, apiUpdateSource } from '@/api'
import type { SourceItem } from '@/api/types'
import { MATERIAL_STATUSES, SOURCE_TYPES } from '@/api/types'
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

export interface SourceFormDialogProps {
  open: boolean
  editing: SourceItem | null
  onOpenChange: (open: boolean) => void
  onSaved: () => void
}

export function SourceFormDialog({ open, editing, onOpenChange, onSaved }: SourceFormDialogProps) {
  const { t } = useTranslation()

  const schema = useMemo(() => z.object({
    type: z.enum(SOURCE_TYPES, { message: t('sources.typeRequired') }),
    code: z.string().trim().min(1, t('sources.codeRequired')).max(64),
    name: z.string().trim().min(1, t('sources.nameRequired')).max(128),
    searchCode: z.string().trim().max(32),
    status: z.enum(MATERIAL_STATUSES),
  }), [t])

  type FormValues = z.infer<typeof schema>

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    values: useMemo<FormValues>(() => editing
      ? {
          type: editing.type,
          code: editing.code,
          name: editing.name,
          searchCode: editing.searchCode ?? '',
          status: editing.status,
        }
      : { type: 'SUPPLIER', code: '', name: '', searchCode: '', status: 'ENABLED' }, [editing, open]),
  })

  const mutation = useMutation({
    mutationFn: (v: FormValues): Promise<unknown> => {
      if (editing) {
        return apiUpdateSource(editing.id, {
          name: v.name, searchCode: v.searchCode || null, status: v.status,
        })
      }
      return apiCreateSource(
        { type: v.type, code: v.code, name: v.name, searchCode: v.searchCode || null, status: v.status },
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
          <DialogTitle>{editing ? t('sources.editTitle') : t('sources.createTitle')}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit((v) => mutation.mutate(v))} className="space-y-4" data-testid="source-form">
            <div className="grid grid-cols-2 gap-3">
              <FormField
                control={form.control}
                name="type"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('sources.type')}</FormLabel>
                    <FormControl>
                      <Select value={field.value} onValueChange={field.onChange} disabled={!!editing}>
                        <SelectTrigger data-testid="f-type"><SelectValue /></SelectTrigger>
                        <SelectContent>
                          {SOURCE_TYPES.map((v) => (
                            <SelectItem key={v} value={v}>{t(`enums.sourceType.${v.toLowerCase()}`)}</SelectItem>
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
                name="code"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('sources.code')}</FormLabel>
                    <FormControl>
                      <Input {...field} disabled={!!editing} data-testid="f-code" />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>
            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('sources.name')}</FormLabel>
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
                  <FormLabel>{t('sources.searchCode')}</FormLabel>
                  <FormControl>
                    <Input {...field} data-testid="f-searchCode" />
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
                  <FormLabel>{t('sources.status')}</FormLabel>
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
